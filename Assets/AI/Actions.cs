using System.Collections.Generic;

namespace AI
{
    public class AgentAction
    {
        public string Name { get; }
        public float Cost { get; set; }

        public HashSet<AgentBelief> Preconditions { get; } = new();
        public HashSet<AgentBelief> Effects { get; } = new();

        public IActionStrategy Strategy;
        public bool Complete => Strategy.Complete;

        public AgentAction(string name)
        {
            Name = name;
        }

        public void InitializeAgentAction() => Strategy.Start();

        public void UpdateAgentAction(float deltaTime)
        {
            // Проверка, может ли быть выполнено действие и обновление стратегии
            if (Strategy.CanPerform)
            {
                Strategy.Update(deltaTime);
            }

            // Если стратегия ещё идёт, то выйти из функции
            if (!Strategy.Complete) return;

            // Если стратегия завершилась, проверить эффекты совершённого действия
            foreach (var effect in Effects)
            {
                effect.Evaluate();
            }
        }

        public void Stop() => Strategy.Stop();
    }
}