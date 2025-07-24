using UnityEngine;

[CreateAssetMenu(menuName = "GOAP/Goal")]
public class AgentGoalAsset : ScriptableObject
{
    public string GoalName;
    public float Priority = 1.0f;

    public AgentBeliefAsset[] DesiredEffects;

    public AgentGoal CreateGoal()
    {
        var builder = new AgentGoal.Builder(GoalName)
            .WithPriority(Priority);

        foreach (var effect in DesiredEffects)
        {
            builder.WithDesiredEffect(effect.CreateBelief());
        }

        return builder.Build();
    }
}
