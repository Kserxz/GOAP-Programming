namespace AI
{
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