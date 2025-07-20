using System;
using System.Collections.Generic;
using System.Linq;
using AI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

[RequireComponent(typeof(NavMeshAgent))]
// [RequireComponent(typeof(AnimationController))]

public class GoapAgent : MonoBehaviour
{
    [Header("Sensors")]
    [SerializeField] Sensor chaseSensor;
    [SerializeField] Sensor attackSensor;

    [Header("Known Locations")]
    [SerializeField] Transform restinPosition;
    [SerializeField] Transform kitchen;
    [SerializeField] Transform garage;
    [SerializeField] Transform bedroom;
    [SerializeField] Transform bathroom;
    [FormerlySerializedAs("health")] [Header("Stats")]
    public float Health = 100;
    [FormerlySerializedAs("stamina")] public float Stamina = 100;
    public AgentGoal CurrentGoal;
    public ActionPlan actionPlan;
    public AgentAction currentAction;
    public Dictionary<string, AgentBelief> beliefs;
    public HashSet<AgentAction> actions;
    public HashSet<AgentGoal> goals;

    private IGoapPlanner gPlanner;
    public NavMeshAgent NavMeshAgent;
    //    AnimationController animations;
    public Rigidbody Rigidbody;
    private CountdownTimer statsTimer;
    private GameObject target;
    private Vector3 destination;
    private AgentGoal lastGoal;

    private void OnValidate()
    {
        NavMeshAgent = GetComponent<NavMeshAgent>();
        Rigidbody = GetComponent<Rigidbody>();
    }

    void Awake()
    {
        // animations = GetComponent<AnimationController>();
        
        Rigidbody.freezeRotation = true;

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

        factory.AddBelief("AgentIdle", () => !NavMeshAgent.hasPath);
        factory.AddBelief("AgentMoving", () => NavMeshAgent.hasPath);
    }

    void SetupActions()
    {
        actions = new HashSet<AgentAction>();

        actions.Add(new Builder("Relax")
        .WithStrategy(new IdleStrategy(5))
        .AddEffect(beliefs["Nothing"])
        .Build());


        actions.Add(new Builder("Wander Around")
        .WithStrategy(new WanderStrategy(NavMeshAgent, 10))
        .AddEffect(beliefs["AgentMoving"])
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
        Stamina += InRangeOf(restinPosition.position, 3f) ? 20 : -10;
        Health += InRangeOf(kitchen.position, 3f) ? 20 : -5;
        Stamina = Mathf.Clamp(Stamina, 0, 100);
        Health = Mathf.Clamp(Health, 0, 100);
    }

    bool InRangeOf(Vector3 pos, float range) => Vector3.Distance(transform.position, pos) < range;

    void OnEnable() => chaseSensor.OnTargetChanged += HandleTargetChanged;
    void OnDisable() => chaseSensor.OnTargetChanged -= HandleTargetChanged;

    void HandleTargetChanged()
    {
        Debug.Log("Target changed, clearing current action and goal");
        // Заставляем планировщик пересчитать план
        currentAction = null;
        CurrentGoal = null;
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
                NavMeshAgent.ResetPath();

                CurrentGoal = actionPlan.AgentGoal;
                currentAction = actionPlan.Actions.Pop();
                currentAction.InitializeAgentAction();
                Debug.Log($"Goal: {CurrentGoal.Name} with {actionPlan.Actions.Count} actions in plan");
                Debug.Log($"Popped action: {currentAction.Name}");
            }
        }

        // Если есть текущее действие, выполняем его
        if (actionPlan != null && currentAction != null)
        {
            currentAction.UpdateAgentAction(Time.deltaTime);

            if (currentAction.Complete)
            {
                Debug.Log($"{currentAction.Name} complete");
                currentAction.Stop();
                currentAction = null;

                if (actionPlan.Actions.Count == 0)
                {
                    Debug.Log("Plan complete");
                    lastGoal = CurrentGoal;
                    CurrentGoal = null;
                }
            }
        }

    }

    void CalculatePlan()
    {
        var priorityLevel = CurrentGoal?.Priority ?? 0;

        HashSet<AgentGoal> goalsToCheck = goals;

        // Если у нас есть текущая цель, проверяем только цели с более высоким приоритетом
        if (CurrentGoal != null)
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