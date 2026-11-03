using System;
using System.IO;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zoo.Content;
using Zoo.Simulation;

namespace Zoo.Tests
{
    public sealed class ContentAndInteractionTests
    {
        static string Read(string name) => File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Zoo", name + ".json"));
        [Test] public void ShippedContentHasBothMovementStrategies()
        {
            var catalog = ContentCatalog.Parse(Read("animals"), Read("interactions"), Read("level"));
            Assert.That(catalog.Animal("frog").movement.kind, Is.EqualTo("jump"));
            Assert.That(catalog.Animal("snake").movement.kind, Is.EqualTo("linear"));
        }
        [Test] public void InvalidCrossReferenceIsRejected()
        {
            Assert.Throws<ArgumentException>(() => ContentCatalog.Parse(Read("animals"), Read("interactions"), Read("level").Replace("frog", "missing")));
        }
        [Test] public void UnknownMovementIsRejected()
        {
            Assert.Throws<ArgumentException>(() => ContentCatalog.Parse(Read("animals").Replace("jump", "teleport"), Read("interactions"), Read("level")));
        }
        [Test] public void IncompleteInteractionMatrixIsRejected()
        {
            Assert.Throws<ArgumentException>(() => ContentCatalog.Parse(Read("animals"), "{\"schemaVersion\":1,\"rules\":[]}", Read("level")));
        }
        [UnityTest] public IEnumerator RejectedReloadPreservesActiveCatalog() => UniTask.ToCoroutine(async () =>
        {
            bool invalid = false;
            var service = new ContentService((path, token) => UniTask.FromResult(invalid && path.EndsWith("level.json")
                ? "{\"schemaVersion\":1}" : File.ReadAllText(path)));
            await service.ReloadAsync(CancellationToken.None);
            Assert.That(service.ApplyPending(), Is.True);
            var previous = service.Current;
            invalid = true;
            await service.ReloadAsync(CancellationToken.None);
            Assert.That(service.ApplyPending(), Is.False);
            Assert.That(service.Current, Is.SameAs(previous));
        });
        [Test] public void JumpCoversConfiguredDistanceAcrossPartialPhysicsSteps()
        {
            var jump = new JumpMovement(new MovementDefinition { intervalSeconds = 1.2f, durationSeconds = 0.35f, distance = 2 });
            float distance = 0;
            for (int i = 0; i < 60; i++) distance += jump.Speed(0.02f) * 0.02f;
            Assert.That(distance, Is.EqualTo(2).Within(0.001f));
        }
        [UnityTest] public IEnumerator ValidReloadPublishesOnlyAtExplicitBoundary() => UniTask.ToCoroutine(async () =>
        {
            string version = "prototype-1";
            var service = new ContentService((path, token) => UniTask.FromResult(File.ReadAllText(path).Replace("prototype-1", version)));
            await service.ReloadAsync(CancellationToken.None);
            Assert.That(service.Current, Is.Null);
            service.ApplyPending();
            var oldDefinition = service.Current.Animal("frog");
            version = "prototype-2";
            await service.ReloadAsync(CancellationToken.None);
            Assert.That(service.Current.Version, Is.EqualTo("prototype-1"));
            service.ApplyPending();
            Assert.That(service.Current.Version, Is.EqualTo("prototype-2"));
            Assert.That(service.Current.Animal("frog"), Is.Not.SameAs(oldDefinition));
            Assert.That(oldDefinition.id, Is.EqualTo("frog"));
        });
        [TestCase(1, "prey", 2, "predator", 2)]
        [TestCase(2, "predator", 1, "prey", 2)]
        [TestCase(5, "predator", 3, "predator", 3)]
        [TestCase(1, "prey", 2, "prey", 0)]
        public void ContactWinnerIsIndependentOfCallbackOrder(int a, string roleA, int b, string roleB, int expected)
        {
            var catalog = ContentCatalog.Parse(Read("animals"), Read("interactions"), Read("level"));
            var rule = catalog.Rule(roleA, roleB);
            Assert.That(InteractionPolicy.Winner(rule, a, roleA, b, roleB), Is.EqualTo(expected));
            Assert.That(InteractionPolicy.Winner(rule, b, roleB, a, roleA), Is.EqualTo(expected));
        }
    }
}
