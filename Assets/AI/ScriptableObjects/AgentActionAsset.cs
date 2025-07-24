using UnityEngine;

public enum ActionStrategyType
{
    Idle,
    Move,
    Wander
}

[CreateAssetMenu(menuName = "GOAP/Action")]
public class AgentActionAsset : ScriptableObject
{
    public string ActionName;
    public float Cost = 1.0f;

    [Header("Strategy")]
    public ActionStrategyType StrategyType = ActionStrategyType.Idle;
    public float IdleDuration = 1f;
    public float WanderRadius = 10f;
    public Transform MoveTarget;

    [Header("Preconditions")]
    public AgentBeliefAsset[] Preconditions;

    [Header("Effects")]
    public AgentBeliefAsset[] Effects;

    public AgentAction CreateAction()
    {
        var builder = new AgentAction.Builder(ActionName)
            .WithCost(Cost);

        // Стратегия
        switch (StrategyType)
        {
            case ActionStrategyType.Idle:
                builder.WithStrategy(new IdleStrategy(IdleDuration));
                break;
            case ActionStrategyType.Move:
                builder.WithStrategy(new MoveStrategy(
                    null, // NavMeshAgent будет подставлен в GoapAgent при создании
                    () => MoveTarget ? MoveTarget.position : Vector3.zero
                ));
                break;
            case ActionStrategyType.Wander:
                builder.WithStrategy(new WanderStrategy(
                    null, // NavMeshAgent будет подставлен в GoapAgent при создании
                    WanderRadius
                ));
                break;
        }

        foreach (var pre in Preconditions)
        {
            builder.AddPrecondition(pre.CreateBelief());
        }

        foreach (var effect in Effects)
        {
            builder.AddEffect(effect.CreateBelief());
        }

        return builder.Build();
    }
}
