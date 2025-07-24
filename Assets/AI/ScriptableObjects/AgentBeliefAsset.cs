using UnityEngine;
using System;

[CreateAssetMenu(menuName = "GOAP/Belief")]
public class AgentBeliefAsset : ScriptableObject
{
    public string BeliefName;

    // В редакторе нельзя задать делегаты, используем заглушку
    public AgentBelief CreateBelief()
    {
        return new AgentBelief.Builder(BeliefName)
            .WithCondition(() => false)
            .Build();
    }

    // ВНИМАНИЕ: Не используйте ассеты для runtime-убеждений (AgentIdle, AgentMoving и т.д.)
    // Для них используйте только программное создание через BeliefFactory
}
