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
}
