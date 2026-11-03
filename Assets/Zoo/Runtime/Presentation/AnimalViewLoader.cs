using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zoo.Content;
using Zoo.Simulation;

namespace Zoo.Presentation
{
    public sealed class AnimalViewLoader : IDisposable
    {
        readonly Dictionary<string, AsyncOperationHandle<GameObject>> prefabs = new();
        readonly CancellationTokenSource lifetime = new();
        readonly Func<string, AsyncOperationHandle<GameObject>> load;
        readonly FallbackViewPool fallbacks = new();
        bool disposed;
        public AnimalViewLoader(Func<string, AsyncOperationHandle<GameObject>> load = null)
        {
            this.load = load ?? (address => Addressables.LoadAssetAsync<GameObject>(address));
        }

        public async UniTask PreloadAsync(string address, CancellationToken cancellationToken)
            => await GetPrefabAsync(address, cancellationToken);

        async UniTask<GameObject> GetPrefabAsync(string address, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (disposed) throw new ObjectDisposedException(nameof(AnimalViewLoader));
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Missing visual address");
            if (!prefabs.TryGetValue(address, out var handle))
            {
                handle = load(address);
                prefabs.Add(address, handle);
            }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
            try
            {
                // Each caller gets its own awaiter. Cancelling one caller never releases the shared handle.
                var prefab = await handle.ToUniTask(cancellationToken: linked.Token, cancelImmediately: true, autoReleaseWhenCanceled: false);
                linked.Token.ThrowIfCancellationRequested();
                if (!prefab) throw new InvalidOperationException("Missing prefab: " + address);
                return prefab;
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                if (prefabs.TryGetValue(address, out var current) && current.Equals(handle))
                {
                    prefabs.Remove(address);
                    if (handle.IsValid()) Addressables.Release(handle);
                }
                throw;
            }
        }

        public async UniTask AttachAsync(AnimalActor actor, ViewDefinition view, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (disposed) throw new ObjectDisposedException(nameof(AnimalViewLoader));
            if (!actor || !actor.IsAlive) return;
            // A ready prefab needs no temporary geometry or material allocation.
            if (!string.IsNullOrEmpty(view.address) && prefabs.TryGetValue(view.address, out var ready)
                && ready.IsValid() && ready.IsDone && ready.Status == AsyncOperationStatus.Succeeded && ready.Result)
            {
                UnityEngine.Object.Instantiate(ready.Result, actor.VisualRoot, false);
                return;
            }
            var fallback = fallbacks.Rent(view, actor.VisualRoot, actor.LifetimeToken);
            if (string.IsNullOrEmpty(view.address)) return;
            try
            {
                var prefab = await GetPrefabAsync(view.address, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (disposed || !actor || !actor.IsAlive || !prefab) return;
                UnityEngine.Object.Instantiate(prefab, actor.VisualRoot, false);
                fallback.Dispose();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { Debug.LogWarning("Visual fallback for " + view.id + ": " + error.Message); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            fallbacks.Dispose();
            foreach (var handle in prefabs.Values) if (handle.IsValid()) Addressables.Release(handle);
            prefabs.Clear();
            lifetime.Dispose();
        }

    }
}
