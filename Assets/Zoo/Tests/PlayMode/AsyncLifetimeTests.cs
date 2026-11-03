using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Zoo.Content;
using Zoo.Presentation;
using Zoo.Simulation;

namespace Zoo.Tests
{
    public sealed class AsyncLifetimeTests
    {
        sealed class PendingPrefab : AsyncOperationBase<GameObject>
        {
            protected override void Execute() { }
            public void Finish(GameObject result) => Complete(result, true, (Exception)null);
        }

        [UnityTest] public IEnumerator OneCancelledWaiterDoesNotCancelSharedLoad() => UniTask.ToCoroutine(async () =>
        {
            var prefab = new GameObject("Shared test prefab");
            var operation = new PendingPrefab();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            int loads = 0;
            using var loader = new AnimalViewLoader(_ => { loads++; return handle; });
            using var cancellation = new CancellationTokenSource();
            try
            {
                var first = loader.PreloadAsync("shared", cancellation.Token).SuppressCancellationThrow();
                var second = loader.PreloadAsync("shared", CancellationToken.None);
                cancellation.Cancel();
                Assert.That(await first, Is.True);
                operation.Finish(prefab);
                await second;
                await loader.PreloadAsync("shared", CancellationToken.None);
                Assert.That(loads, Is.EqualTo(1));
                Assert.That(handle.IsValid(), Is.True);
                loader.Dispose();
                Assert.That(handle.IsValid(), Is.False);
            }
            finally { UnityEngine.Object.Destroy(prefab); }
        });

        [UnityTest] public IEnumerator DisposingLoaderCancelsPendingWaitAndReleasesHandle() => UniTask.ToCoroutine(async () =>
        {
            var prefab = new GameObject("Late test prefab");
            var operation = new PendingPrefab();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            using var loader = new AnimalViewLoader(_ => handle);
            try
            {
                var waiting = loader.PreloadAsync("late", CancellationToken.None).SuppressCancellationThrow();
                loader.Dispose();
                Assert.That(await waiting, Is.True);
                operation.Finish(prefab);
                await UniTask.Yield();
                Assert.That(handle.IsValid(), Is.False);
            }
            finally { UnityEngine.Object.Destroy(prefab); }
        });

        [UnityTest] public IEnumerator ActorDeathCancelsAttachmentBeforeLatePrefabArrives() => UniTask.ToCoroutine(async () =>
        {
            var go = new GameObject("Dying test actor");
            var prefab = new GameObject("Late actor visual");
            var material = new PhysicsMaterial();
            var operation = new PendingPrefab();
            var handle = Addressables.ResourceManager.StartOperation(operation, default);
            using var loader = new AnimalViewLoader(_ => handle);
            try
            {
                var actor = go.AddComponent<AnimalActor>();
                actor.Initialize(1, new AnimalDefinition { id = "test", foodRole = "prey",
                    movement = new MovementDefinition { kind = "linear", speed = 1 } }, new InteractionResolver(), material);
                var view = new ViewDefinition { id = "test", address = "late", fallback = "sphere", color = "#FFFFFF", scale = new[] { 1f, 1f, 1f } };
                var attachment = loader.AttachAsync(actor, view, actor.LifetimeToken).SuppressCancellationThrow();
                actor.TryDie();
                Assert.That(await attachment, Is.True);
                Assert.That(actor.LifetimeToken.IsCancellationRequested, Is.True);
                operation.Finish(prefab);
                await UniTask.Yield();
            }
            finally
            {
                if (go) UnityEngine.Object.Destroy(go);
                UnityEngine.Object.Destroy(prefab);
                UnityEngine.Object.Destroy(material);
            }
        });

        [UnityTest] public IEnumerator CancelledRemoteLoadDoesNotStartRequest() => UniTask.ToCoroutine(async () =>
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            using var loader = new RemoteReleaseLoader();
            using var visuals = new AnimalViewLoader();
            var result = await loader.LoadAsync("invalid-url", visuals, source.Token).SuppressCancellationThrow();
            Assert.That(result.IsCanceled, Is.True);
        });
    }
}
