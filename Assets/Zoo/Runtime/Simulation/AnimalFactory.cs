using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zoo.Content;
using Zoo.Presentation;

namespace Zoo.Simulation
{
    public sealed class AnimalFactory : IDisposable
    {
        readonly Transform parent;
        readonly InteractionResolver interactions;
        readonly AnimalViewLoader views;
        readonly PhysicsMaterial contactMaterial;
        int nextId;
        bool disposed;

        public AnimalFactory(Transform parent, InteractionResolver interactions, AnimalViewLoader views)
        {
            this.parent = parent;
            this.interactions = interactions;
            this.views = views;
            contactMaterial = new PhysicsMaterial("Animal contacts")
            { bounciness = 0.85f, dynamicFriction = 0, staticFriction = 0, bounceCombine = PhysicsMaterialCombine.Maximum };
        }

        public AnimalActor Create(AnimalDefinition definition, ViewDefinition view, Vector3 position, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (disposed) throw new ObjectDisposedException(nameof(AnimalFactory));
            var go = new GameObject(definition.id + " #" + (nextId + 1));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            try
            {
                var actor = go.AddComponent<AnimalActor>();
                actor.Initialize(++nextId, definition, interactions, contactMaterial);
                AttachViewAsync(actor, view, cancellationToken).Forget();
                return actor;
            }
            catch { UnityEngine.Object.Destroy(go); throw; }
        }

        async UniTask AttachViewAsync(AnimalActor actor, ViewDefinition view, CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, actor.LifetimeToken);
            try { await views.AttachAsync(actor, view, linked.Token); }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (contactMaterial) UnityEngine.Object.Destroy(contactMaterial);
        }
    }
}
