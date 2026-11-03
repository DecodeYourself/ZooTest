using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Zoo.Content;
using Zoo.Presentation;

namespace Zoo.Simulation
{
    /// <summary>Applies content at physics boundaries and owns the active population.</summary>
    public sealed class ZooWorld : IDisposable
    {
        readonly ContentService content;
        readonly InteractionResolver interactions;
        readonly SpawnService spawns;
        readonly CameraArena arena;
        readonly List<AnimalActor> animals = new();
        bool initialized;
        bool disposed;
        public int AliveCount => animals.Count;
        public int DeadPrey => interactions.DeadPrey;
        public int DeadPredators => interactions.DeadPredators;
        public string ContentVersion => content.Current?.Version;

        public ZooWorld(ContentService content, InteractionResolver interactions, SpawnService spawns, CameraArena arena)
        { this.content = content; this.interactions = interactions; this.spawns = spawns; this.arena = arena; }
        public void Tick(float deltaTime, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (disposed) throw new ObjectDisposedException(nameof(ZooWorld));
            if (content.ApplyPending())
            {
                var level = content.Current.Level;
                arena.SetHalfHeight(level.cameraHalfHeight);
                spawns.Configure(level);
                if (!initialized)
                {
                    initialized = true;
                    for (int i = 0; i < level.initialPopulation; i++)
                        animals.Add(spawns.Spawn(content.Current, arena.Bounds, cancellationToken));
                }
            }
            if (!initialized) return;
            interactions.Resolve(content.Current);
            animals.RemoveAll(a => !a || !a.IsAlive);
            var bounds = arena.Bounds;
            foreach (var animal in animals) animal.Tick(deltaTime, bounds);
            var spawned = spawns.Tick(deltaTime, content.Current, bounds, cancellationToken);
            if (spawned) animals.Add(spawned);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var actor in animals) if (actor && actor.IsAlive) actor.TryDie();
            animals.Clear();
        }
    }
}
