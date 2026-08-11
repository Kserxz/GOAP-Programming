using System.Collections.Generic;

public class AgentAction
{
    public string Name { get; }
    public float Cost { get; private set; }

    public HashSet<AgentBelief> Preconditions { get; } = new();
    public HashSet<AgentBelief> Effects { get; } = new();

    public IActionStrategy Strategy { get; private set; }
    IActionStrategy strategy;
    public bool Complete => strategy.Complete;

    private AgentAction(string name)
    {
        Name = name;
    }

    public void Start() => strategy.Start();

    public void Update(float deltaTime)
    {
        // Проверка, может ли быть выполнено действие и обновление стратегии
        if (strategy.CanPerform)
        {
            strategy.Update(deltaTime);
        }

        // Если стратегия ещё идёт, то выйти из функции
        if (!strategy.Complete) return;

        // Если стратегия завершилась, проверить эффекты совершённого действия
        foreach (var effect in Effects)
        {
            effect.Evaluate();
        }
    }

    public void Stop() => strategy.Stop();

    public class Builder
    {
        readonly AgentAction action;

        public Builder(string name)
        {
            action = new AgentAction(name)
            {
                Cost = 1
            };
        }

        public Builder WithCost(float cost)
        {
            action.Cost = cost;
            return this;
        }

        public Builder WithStrategy(IActionStrategy strategy)
        {
            action.Strategy = strategy;
            action.strategy = strategy; // <--- добавьте эту строку
            return this;
        }

        public Builder AddPrecondition(AgentBelief precondition)
        {
            action.Preconditions.Add(precondition);
            return this;
        }

        public Builder AddEffect(AgentBelief effect)
        {
            action.Effects.Add(effect);
            return this;
        }

        public AgentAction Build()
        {
            return action;
        }
    }
    
}