using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using Zoo.Content;
using Zoo.Presentation;
using Zoo.Simulation;

namespace Zoo.Tests
{
    public sealed class FallbackPoolTests
    {
        static ViewDefinition View(string color = "#FFFFFF", float size = 1) => new()
        { id = "test", address = "", fallback = "sphere", color = color, scale = new[] { size, size, size } };

        [UnityTest] public IEnumerator ReturnedGeometryIsReusedAndReset()
        {
            var parent = new GameObject("Fallback test parent");
            using var pool = new FallbackViewPool();
            try
            {
                var first = pool.Rent(View(), parent.transform, CancellationToken.None);
                var instance = first.Instance;
                var material = instance.GetComponent<Renderer>().sharedMaterial;
                instance.transform.localPosition = Vector3.one * 10;
                instance.transform.localRotation = Quaternion.Euler(20, 30, 40);
                first.Dispose();
                first.Dispose();
                Assert.That(instance.activeInHierarchy, Is.False);
                var second = pool.Rent(View(size: 2), parent.transform, CancellationToken.None);
                Assert.That(second.Instance, Is.SameAs(instance));
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one * 2));
                Assert.That(instance.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(instance.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(instance.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(instance.activeInHierarchy, Is.True);
                second.Dispose();
                pool.Dispose();
                yield return null;
                Assert.That(instance == null, Is.True);
                Assert.That(material == null, Is.True);
            }
            finally { UnityEngine.Object.Destroy(parent); }
        }

        [UnityTest] public IEnumerator ActiveAnimalsShareMaterialButDifferentColorsStayIndependent()
        {
            var parent = new GameObject("Material test parent");
            using var pool = new FallbackViewPool();
            try
            {
                var a = pool.Rent(View(), parent.transform, CancellationToken.None);
                var b = pool.Rent(View(), parent.transform, CancellationToken.None);
                var c = pool.Rent(View("#FF0000"), parent.transform, CancellationToken.None);
                Assert.That(a.Instance, Is.Not.SameAs(b.Instance));
                Assert.That(a.Instance.GetComponent<Renderer>().sharedMaterial,
                    Is.SameAs(b.Instance.GetComponent<Renderer>().sharedMaterial));
                Assert.That(a.Instance.GetComponent<Renderer>().sharedMaterial,
                    Is.Not.SameAs(c.Instance.GetComponent<Renderer>().sharedMaterial));
                a.Dispose(); b.Dispose(); c.Dispose();
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(parent); }
        }

        [UnityTest] public IEnumerator OwnerCancellationReturnsFallbackBeforeParentDestruction()
        {
            var parent = new GameObject("Lease owner");
            using var pool = new FallbackViewPool();
            using var cancellation = new CancellationTokenSource();
            try
            {
                var lease = pool.Rent(View(), parent.transform, cancellation.Token);
                var instance = lease.Instance;
                cancellation.Cancel();
                UnityEngine.Object.Destroy(parent);
                yield return null;
                Assert.That(instance != null, Is.True);
                var reused = pool.Rent(View(), null, CancellationToken.None);
                Assert.That(reused.Instance, Is.SameAs(instance));
                reused.Dispose();
            }
            finally { if (parent) UnityEngine.Object.Destroy(parent); }
        }

        [UnityTest] public IEnumerator ReadyPrefabSkipsFallbackAndFactorySharesPhysicsMaterial() => UniTask.ToCoroutine(async () =>
        {
            var parent = new GameObject("Factory test parent");
            var prefab = new GameObject("Ready test prefab");
            var handle = Addressables.ResourceManager.CreateCompletedOperation(prefab, (string)null);
            using var views = new AnimalViewLoader(_ => handle);
            using var factory = new AnimalFactory(parent.transform, new InteractionResolver(), views);
            try
            {
                await views.PreloadAsync("ready", CancellationToken.None);
                var definition = new AnimalDefinition { id = "test", foodRole = "prey",
                    movement = new MovementDefinition { kind = "linear", speed = 1 } };
                var view = View(); view.address = "ready";
                var a = factory.Create(definition, view, new Vector3(1000, 0, 1000), CancellationToken.None);
                var b = factory.Create(definition, view, new Vector3(1010, 0, 1000), CancellationToken.None);
                Assert.That(b.Id, Is.GreaterThan(a.Id));
                Assert.That(a.VisualRoot.childCount, Is.EqualTo(1));
                Assert.That(a.VisualRoot.GetChild(0).name, Does.StartWith("Ready test prefab"));
                Assert.That(a.GetComponent<Collider>().sharedMaterial, Is.SameAs(b.GetComponent<Collider>().sharedMaterial));
                a.TryDie(); b.TryDie();
                await UniTask.Yield();
            }
            finally { UnityEngine.Object.Destroy(parent); UnityEngine.Object.Destroy(prefab); }
        });

        [UnityTest] public IEnumerator FactoryReusesFallbackAfterAnimalDeath()
        {
            var parent = new GameObject("Fallback factory parent");
            using var views = new AnimalViewLoader();
            using var factory = new AnimalFactory(parent.transform, new InteractionResolver(), views);
            try
            {
                var definition = new AnimalDefinition { id = "test", foodRole = "prey",
                    movement = new MovementDefinition { kind = "linear", speed = 1 } };
                var a = factory.Create(definition, View(), new Vector3(1000, 0, 1000), CancellationToken.None);
                var fallback = a.VisualRoot.GetChild(0).gameObject;
                a.TryDie();
                var b = factory.Create(definition, View(), new Vector3(1010, 0, 1000), CancellationToken.None);
                Assert.That(b.VisualRoot.GetChild(0).gameObject, Is.SameAs(fallback));
                b.TryDie();
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(parent); }
        }

        [UnityTest] public IEnumerator PoolBoundsInactiveInstances()
        {
            using var pool = new FallbackViewPool(maxInactivePerKey: 1);
            var a = pool.Rent(View(), null, CancellationToken.None);
            var b = pool.Rent(View(), null, CancellationToken.None);
            var kept = a.Instance;
            var overflow = b.Instance;
            a.Dispose(); b.Dispose();
            yield return null;
            Assert.That(kept != null, Is.True);
            Assert.That(overflow == null, Is.True);
        }
    }
}
