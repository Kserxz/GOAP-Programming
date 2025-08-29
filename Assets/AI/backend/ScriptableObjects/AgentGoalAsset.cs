using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "GOAP/Goal")]
public class AgentGoalAsset : ScriptableObject
{
    public string GoalName;
    public float Priority = 1.0f;

    [Header("Desired Effects (Belief Names)")]
    public string[] DesiredEffects;

    public AgentGoal CreateGoal(Dictionary<string, AgentBelief> beliefs)
    {
        var builder = new AgentGoal.Builder(GoalName)
            .WithPriority(Priority);

        if (DesiredEffects != null)
        {
            foreach (var effect in DesiredEffects)
            {
                if (!string.IsNullOrEmpty(effect) && beliefs.TryGetValue(effect, out var belief))
                    builder.WithDesiredEffect(belief);
            }
        }

        return builder.Build();
    }
}
