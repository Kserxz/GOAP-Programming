using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
// [RequireComponent(typeof(AnimationController))]

public class GoapAgent : MonoBehaviour
{
    [Header("Sensors")]
    [SerializeField] Sensor chaseSensor;
    [SerializeField] Sensor attackSensor;

    [Header("Known Locations")]
    [SerializeField] Transform restingPosition;
    [SerializeField] Transform kitchenPosition;
    [SerializeField] Transform garagePosition;
    [SerializeField] Transform bedroomPosition;
    [SerializeField] Transform officePosition;
    [SerializeField] Transform bathroomPosition;
    [SerializeField] Transform trainingPosition;

    [Header("GOAP Assets")]
    [SerializeField] AgentActionAsset[] actionAssets;
    [SerializeField] AgentGoalAsset[] goalAssets;

    NavMeshAgent navMeshAgent;
    // AnimationController animations;
    Rigidbody rb;

    [Header("Stats")]
    public float health = 100;
    public float stamina = 100;
    public float studyDesire = 100;
    public float trainingDesire = 100;      
    public float repairCarDesire = 100;     

    CountdownTimer statsTimer;

    GameObject target;
    Vector3 destination;

    AgentGoal lastGoal;
    public AgentGoal currentGoal;
    public ActionPlan actionPlan;
    public AgentAction currentAction;

    public Dictionary<string, AgentBelief> beliefs;
    public HashSet<AgentAction> actions;
    public HashSet<AgentGoal> goals;

    Dictionary<string, Transform> locations;

    IGoapPlanner gPlanner;

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        // animations = GetComponent<AnimationController>();
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        gPlanner = new GoapPlanner();
    }

    void Start()
    {
        SetupTimers();
        SetupLocations();
        SetupBeliefs();
        SetupActions();
        SetupGoals();
    }

    void SetupLocations()
    {
        locations = new Dictionary<string, Transform>
        {
            { "restingPosition", restingPosition },
            { "kitchenPosition", kitchenPosition },
            { "garagePosition", garagePosition },
            { "bedroomPosition", bedroomPosition },
            { "officePosition", officePosition },
            { "bathroomPosition", bathroomPosition },
            { "trainingPosition", trainingPosition },
            { "repairCarPosition", garagePosition }
            // добавить другие при расширении
        };
    }

    void SetupBeliefs()
    {
        beliefs = new Dictionary<string, AgentBelief>();
        BeliefFactory factory = new BeliefFactory(this, beliefs);

        // Жёстко заданные убеждения
        factory.AddBelief("Nothing", () => false);
        factory.AddBelief("AgentHandsAreClean", () => false);
        factory.AddBelief("AgentIdle", () => !navMeshAgent.hasPath);
        factory.AddBelief("AgentMoving", () => navMeshAgent.hasPath);

        // Убеждения для здоровья и стамины
        factory.AddBelief("AgentHealthLow", () => health < 20);
        factory.AddBelief("AgentIsHealthy", () => health >= 40);
        factory.AddBelief("AgentStaminaLow", () => stamina < 20);
        factory.AddBelief("AgentIsRested", () => stamina >= 40);

        // Убеждения для обучения
        factory.AddBelief("AgentStudyDesireLow", () => studyDesire < 20);
        factory.AddBelief("AgentIsStudied", () => studyDesire >= 95);

        // убеждения для TrainingDesire
        factory.AddBelief("AgentTrainingDesireLow", () => trainingDesire < 20);
        factory.AddBelief("AgentIsTrained", () => trainingDesire >= 70);

        // убеждения для RepairCarDesire
        factory.AddBelief("AgentRepairCarDesireLow", () => repairCarDesire < 20);
        factory.AddBelief("AgentCarIsRepaired", () => repairCarDesire >= 70);

        factory.AddLocationBelief("AgentInOffice", 3f, officePosition);
        factory.AddLocationBelief("AgentInKitchen", 3f, kitchenPosition);
        factory.AddLocationBelief("AgentAtRestingPosition", 3f, restingPosition);
        factory.AddLocationBelief("AgentInBathroom", 3f, bathroomPosition);
        factory.AddLocationBelief("AgentInGym", 3f, trainingPosition);
        factory.AddLocationBelief("AgentInGarage", 3f, garagePosition);

        factory.AddSensorBelief("PlayerInChaseRange", chaseSensor);
        factory.AddSensorBelief("PlayerInAttackRange", attackSensor);
        factory.AddBelief("AttackingPlayer", () => false);
    }

    void SetupActions()
    {
        actions = new HashSet<AgentAction>();
        if (actionAssets != null)
        {
            foreach (var asset in actionAssets)
            {
                if (asset != null)
                    actions.Add(asset.CreateAction(beliefs, navMeshAgent, locations));
            }
        }
    }

    void SetupGoals()
    {
        goals = new HashSet<AgentGoal>();
        if (goalAssets != null)
        {
            foreach (var asset in goalAssets)
            {
                if (asset != null)
                    goals.Add(asset.CreateGoal(beliefs));
            }
        }
    }

    void SetupTimers()
    {
        statsTimer = new CountdownTimer(2f);
        statsTimer.OnTimerStop += () =>
        {
            UpdateStats();
            statsTimer.Start();
        };
        statsTimer.Start();
    }

    void UpdateStats()
    {
        stamina += InRangeOf(restingPosition.position, 3f) ? 100 : -5;
        health += InRangeOf(kitchenPosition.position, 3f) ? 50 : -5;
        studyDesire += InRangeOf(officePosition.position, 3f) ? 30 : -5;
        trainingDesire += InRangeOf(trainingPosition.position, 3f) ? 30 : -5;
        repairCarDesire += InRangeOf(garagePosition.position, 3f) ? 30 : -5;
        stamina = Mathf.Clamp(stamina, 0, 100);
        health = Mathf.Clamp(health, 0, 100);
        studyDesire = Mathf.Clamp(studyDesire, 0, 100);
        trainingDesire = Mathf.Clamp(trainingDesire, 0, 100);
        repairCarDesire = Mathf.Clamp(repairCarDesire, 0, 100);
    }

    bool InRangeOf(Vector3 pos, float range) => Vector3.Distance(transform.position, pos) < range;

    void OnEnable() => chaseSensor.OnTargetChanged += HandleTargetChanged;
    void OnDisable() => chaseSensor.OnTargetChanged -= HandleTargetChanged;

    void HandleTargetChanged()
    {
        Debug.Log("Target changed, clearing current action and goal");
        // Заставляем планировщик пересчитать план
        currentAction = null;
        currentGoal = null;
    }

    void Update()
    {
        statsTimer.Tick(Time.deltaTime);
        // animations.SetSpeed(navMeshAgent.velocity.magnitude);

        // Обновить план и текущее действие, если таковое имеется
        if (currentAction == null)
        {
            Debug.Log("Calculating any potential new plan");
            CalculatePlan();

            if (actionPlan != null && actionPlan.Actions.Count > 0)
            {
                navMeshAgent.ResetPath();

                currentGoal = actionPlan.AgentGoal;
                Debug.Log($"Goal: {currentGoal.Name} with {actionPlan.Actions.Count} actions in plan");
                currentAction = actionPlan.Actions.Pop();
                currentAction.Start();
                Debug.Log($"Popped action: {currentAction.Name}");
            }
        }

        // Если есть текущее действие, выполняем его
        if (actionPlan != null && currentAction != null)
        {
            currentAction.Update(Time.deltaTime);

            if (currentAction.Complete)
            {
                Debug.Log($"{currentAction.Name} complete");
                currentAction.Stop();
                currentAction = null;

                if (actionPlan.Actions.Count == 0)
                {
                    Debug.Log("Plan complete");
                    lastGoal = currentGoal;
                    currentGoal = null;
                }
            }
        }

    }

    void CalculatePlan()
    {
        var priorityLevel = currentGoal?.Priority ?? 0;

        HashSet<AgentGoal> goalsToCheck = goals;

        // Если у нас есть текущая цель, проверяем только цели с более высоким приоритетом
        if (currentGoal != null)
        {
            Debug.Log("Current goal exists, checking goals with higher priority");
            goalsToCheck = new HashSet<AgentGoal>(goals.Where(g => g.Priority > priorityLevel));
        }

        var potentialPlan = gPlanner.Plan(this, goalsToCheck, lastGoal);
        if (potentialPlan != null)
        {
            actionPlan = potentialPlan;
        }
    }
}