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

    NavMeshAgent navMeshAgent;
    //    AnimationController animations;
    Rigidbody rb;

    [Header("Stats")]
    public float health = 100;
    public float stamina = 100;

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

    IGoapPlanner gPlanner;

    [Header("GOAP Assets")]
    [SerializeField] AgentBeliefAsset[] beliefAssets;
    [SerializeField] AgentActionAsset[] actionAssets;
    [SerializeField] AgentGoalAsset[] goalAssets;

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
        SetupBeliefs();
        SetupActions();
        SetupGoals();
    }

    void SetupBeliefs()
    {
        beliefs = new Dictionary<string, AgentBelief>();
        BeliefFactory factory = new BeliefFactory(this, beliefs);

        // Из ScriptableObject-ассетов
        if (beliefAssets != null)
        {
            foreach (var asset in beliefAssets)
            {
                if (asset != null && !beliefs.ContainsKey(asset.BeliefName))
                    beliefs.Add(asset.BeliefName, asset.CreateBelief());
            }
        }

        // Программные убеждения (сенсоры, локации и т.д.)
        // Добавлять только если их нет в beliefs
        void AddIfNotExists(string key, System.Func<bool> cond)
        {
            if (!beliefs.ContainsKey(key))
                factory.AddBelief(key, cond);
        }

        AddIfNotExists("Nothing", () => false);
        AddIfNotExists("AgentIdle", () => !navMeshAgent.hasPath);
        AddIfNotExists("AgentMoving", () => navMeshAgent.hasPath);
        AddIfNotExists("AgentHealthLow", () => health < 20);
        AddIfNotExists("AgentIsHealthy", () => health >= 40);
        AddIfNotExists("AgentStaminaLow", () => stamina < 20);
        AddIfNotExists("AgentIsRested", () => stamina >= 40);

        factory.AddLocationBelief("AgentInOffice", 3f, officePosition);
        factory.AddLocationBelief("AgentInKitchen", 3f, kitchenPosition);
        factory.AddLocationBelief("AgentAtRestingPosition", 3f, restingPosition);
        factory.AddLocationBelief("AgentInBathroom", 3f, bathroomPosition);

        factory.AddSensorBelief("SpiritInChaseRange", chaseSensor);
        factory.AddSensorBelief("SpiritInAttackRange", attackSensor);
        AddIfNotExists("AttackingSpirit", () => false);
    }

    void SetupActions()
    {
        actions = new HashSet<AgentAction>();

        if (actionAssets != null)
        {
            foreach (var asset in actionAssets)
            {
                if (asset != null)
                {
                    var action = asset.CreateAction();

                    // Подставляем NavMeshAgent в MoveStrategy/WanderStrategy
                    if (action.Strategy is MoveStrategy move)
                    {
                        typeof(MoveStrategy)
                            .GetField("agent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            .SetValue(move, navMeshAgent);
                    }
                    else if (action.Strategy is WanderStrategy wander)
                    {
                        typeof(WanderStrategy)
                            .GetField("agent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            .SetValue(wander, navMeshAgent);
                    }

                    actions.Add(action);
                }
            }
        }
    }

    void SetupGoals()
    {
        goals = new HashSet<AgentGoal>();

        // Только из ScriptableObject-ассетов
        if (goalAssets != null)
        {
            foreach (var asset in goalAssets)
            {
                if (asset != null)
                    goals.Add(asset.CreateGoal());
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

    // TODO Перенести в систему статистик
    void UpdateStats()
    {
        stamina += InRangeOf(restingPosition.position, 3f) ? 20 : -10;
        health += InRangeOf(kitchenPosition.position, 3f) ? 20 : -5;
        stamina = Mathf.Clamp(stamina, 0, 100);
        health = Mathf.Clamp(health, 0, 100);
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