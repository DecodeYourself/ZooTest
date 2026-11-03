using System;
using System.Threading;
using UnityEngine;
using Zoo.Content;

namespace Zoo.Simulation
{
    /// <summary>Owns spawn cadence, weighted species selection and placement; not the live population.</summary>
    public sealed class SpawnService
    {
        readonly AnimalFactory factory;
        LevelDefinition level;
        float remaining;
        float totalWeight;

        public SpawnService(AnimalFactory factory) { this.factory = factory; }
        public void Configure(LevelDefinition definition)
        {
            level = definition;
            totalWeight = 0;
            foreach (var entry in level.spawnTable) totalWeight += entry.weight;
            remaining = NextDelay();
        }

        public AnimalActor Tick(float deltaTime, ContentCatalog catalog, Rect bounds, CancellationToken cancellationToken)
        {
            if (level == null) throw new InvalidOperationException("Configure spawns before ticking");
            cancellationToken.ThrowIfCancellationRequested();
            if ((remaining -= deltaTime) > 0) return null;
            remaining = NextDelay();
            return Spawn(catalog, bounds, cancellationToken);
        }

        public AnimalActor Spawn(ContentCatalog catalog, Rect bounds, CancellationToken cancellationToken)
        {
            if (level == null) throw new InvalidOperationException("Configure spawns before spawning");
            cancellationToken.ThrowIfCancellationRequested();
            var table = level.spawnTable;
            var pick = UnityEngine.Random.value * totalWeight;
            var id = table[table.Length - 1].animalId;
            foreach (var entry in table) if ((pick -= entry.weight) <= 0) { id = entry.animalId; break; }
            var definition = catalog.Animal(id);
            return factory.Create(definition, catalog.View(definition.viewId), FindPosition(bounds), cancellationToken);
        }

        static Vector3 FindPosition(Rect bounds)
        {
            Vector3 position = default;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                position = new Vector3(UnityEngine.Random.Range(bounds.xMin + 1, bounds.xMax - 1), 0.5f,
                    UnityEngine.Random.Range(bounds.yMin + 1, bounds.yMax - 1));
                if (!Physics.CheckSphere(position, 0.6f)) break;
            }
            return position;
        }
        float NextDelay() => UnityEngine.Random.Range(level.spawnMinSeconds, level.spawnMaxSeconds);
    }
}
