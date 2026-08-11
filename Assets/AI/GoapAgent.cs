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

        factory.AddBelief("Nothing", () => false);

        factory.AddBelief("AgentIdle", () => !navMeshAgent.hasPath);
        factory.AddBelief("AgentMoving", () => navMeshAgent.hasPath);
        factory.AddBelief("AgentHealthLow", () => health < 20);
        factory.AddBelief("AgentIsHealthy", () => health >= 40);
        factory.AddBelief("AgentStaminaLow", () => stamina < 20);
        factory.AddBelief("AgentIsRested", () => stamina >= 40);

        factory.AddLocationBelief("AgentInOffice", 3f, officePosition);
        factory.AddLocationBelief("AgentInKitchen", 3f, kitchenPosition);
        factory.AddLocationBelief("AgentAtRestingPosition", 3f, restingPosition);
        factory.AddLocationBelief("AgentInBathroom", 3f, bathroomPosition);

        factory.AddSensorBelief("SpiritInChaseRange", chaseSensor);
        factory.AddSensorBelief("SpiritInAttackRange", attackSensor);
        factory.AddBelief("AttackingSpirit", () => false); // Духа всегда можно атаковать, значение никогда не станет правдой
    }

    void SetupActions()
    {
        actions = new HashSet<AgentAction>();

        actions.Add(new AgentAction.Builder("Relax")
            .WithStrategy(new IdleStrategy(5))
            .AddEffect(beliefs["Nothing"])
            .Build());


        actions.Add(new AgentAction.Builder("Wander Around")
            .WithStrategy(new WanderStrategy(navMeshAgent, 10))
            .AddEffect(beliefs["AgentMoving"])
            .Build());

        actions.Add(new AgentAction.Builder("MoveToEatingPosition")
            .WithStrategy(new MoveStrategy(navMeshAgent, () => kitchenPosition.position))
            .AddEffect(beliefs["AgentInKitchen"])
            .Build());

        actions.Add(new AgentAction.Builder("Eat")
            .WithStrategy(new IdleStrategy(10)) // Позже заменить на команду
            .AddPrecondition(beliefs["AgentInKitchen"])
            .AddEffect(beliefs["AgentIsHealthy"])
            .Build());

        actions.Add(new AgentAction.Builder("MoveToBathroom")
            .WithStrategy(new MoveStrategy(navMeshAgent, () => bathroomPosition.position))
            .AddEffect(beliefs["AgentInBathroom"])
            .Build());

        actions.Add(new AgentAction.Builder("MoveFromBathroomToRestingPosition")
            .WithStrategy(new MoveStrategy(navMeshAgent, () => restingPosition.position))
            .WithCost(2)
            .AddPrecondition(beliefs["AgentInBathroom"])
            .AddEffect(beliefs["AgentAtRestingPosition"])
            .Build());

        actions.Add(new AgentAction.Builder("Rest")
            .WithStrategy(new IdleStrategy(5))
            .AddPrecondition(beliefs["AgentAtRestingPosition"])
            .AddEffect(beliefs["AgentIsRested"])
            .Build());

        actions.Add(new AgentAction.Builder("ChaseSpirit")
            .WithStrategy(new MoveStrategy(navMeshAgent, () => beliefs["SpiritInChaseRange"].Location))
            .AddPrecondition(beliefs["SpiritInChaseRange"])
            .AddEffect(beliefs["SpiritInAttackRange"])
            .Build());

        actions.Add(new AgentAction.Builder("SeekForSpirit")
            .WithStrategy(new IdleStrategy(1)) // заменить на команду и AttackPlayer(animations)
            .AddPrecondition(beliefs["SpiritInAttackRange"])
            .AddEffect(beliefs["AttackingSpirit"])
            .Build());
    }

    void SetupGoals()
    {
        goals = new HashSet<AgentGoal>();

        goals.Add(new AgentGoal.Builder("Chill Out")
            .WithPriority(1)
            .WithDesiredEffect(beliefs["Nothing"])
            .Build());

        goals.Add(new AgentGoal.Builder("Wander")
            .WithPriority(1)
            .WithDesiredEffect(beliefs["AgentMoving"])
            .Build());

        goals.Add(new AgentGoal.Builder("KeepHealthUp")
            .WithPriority(5)
            .WithDesiredEffect(beliefs["AgentIsHealthy"])
            .Build());

        goals.Add(new AgentGoal.Builder("KeepStaminaUp")
            .WithPriority(4)
            .WithDesiredEffect(beliefs["AgentIsRested"])
            .Build());
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