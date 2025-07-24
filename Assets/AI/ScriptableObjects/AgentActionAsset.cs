using UnityEngine;
using System.Collections.Generic;

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

    [Header("Preconditions (Belief Names)")]
    public string[] Preconditions;

    [Header("Effects (Belief Names)")]
    public string[] Effects;

    public AgentAction CreateAction(Dictionary<string, AgentBelief> beliefs, UnityEngine.AI.NavMeshAgent navMeshAgent)
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
                builder.WithStrategy(new MoveStrategy(navMeshAgent, () => MoveTarget ? MoveTarget.position : Vector3.zero));
                break;
            case ActionStrategyType.Wander:
                builder.WithStrategy(new WanderStrategy(navMeshAgent, WanderRadius));
                break;
        }

        // Preconditions
        if (Preconditions != null)
        {
            foreach (var pre in Preconditions)
            {
                if (!string.IsNullOrEmpty(pre) && beliefs.TryGetValue(pre, out var belief))
                    builder.AddPrecondition(belief);
            }
        }

        // Effects
        if (Effects != null)
        {
            foreach (var effect in Effects)
            {
                if (!string.IsNullOrEmpty(effect) && beliefs.TryGetValue(effect, out var belief))
                    builder.AddEffect(belief);
            }
        }

        return builder.Build();
    }
}
