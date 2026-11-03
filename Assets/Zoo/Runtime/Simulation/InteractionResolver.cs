using System;
using System.Collections.Generic;
using Zoo.Content;

namespace Zoo.Simulation
{
    public static class InteractionPolicy
    {
        // Return 0 for physical contact only; otherwise the winning instance ID.
        public static int Winner(InteractionRule rule, int idA, string roleA, int idB, string roleB)
        {
            if (rule.action == "bounce") return 0;
            return rule.winner == "predator" ? (roleA == "predator" ? idA : idB) : Math.Min(idA, idB);
        }
    }

    public sealed class InteractionResolver
    {
        readonly SortedDictionary<(int, int), (AnimalActor, AnimalActor)> contacts = new();
        public int DeadPrey { get; private set; }
        public int DeadPredators { get; private set; }
        public event Action<AnimalActor> Eaten;

        public void Enqueue(AnimalActor a, AnimalActor b)
        {
            if (a == b || !a.IsAlive || !b.IsAlive) return;
            if (a.Id > b.Id) (a, b) = (b, a);
            contacts[(a.Id, b.Id)] = (a, b);
        }

        public void Resolve(ContentCatalog catalog)
        {
            // Sorted pair order and immediate death flags make multi-contact outcomes stable.
            foreach (var pair in contacts.Values)
            {
                var (a, b) = pair;
                if (!a || !b || !a.IsAlive || !b.IsAlive) continue;
                var winner = InteractionPolicy.Winner(catalog.Rule(a.Definition.foodRole, b.Definition.foodRole), a.Id, a.Definition.foodRole, b.Id, b.Definition.foodRole);
                if (winner == 0) continue;
                var survivor = winner == a.Id ? a : b;
                var victim = winner == a.Id ? b : a;
                if (!victim.TryDie()) continue;
                if (victim.Definition.foodRole == "prey") DeadPrey++; else DeadPredators++;
                Eaten?.Invoke(survivor);
            }
            contacts.Clear();
        }
    }
}
