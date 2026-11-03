using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Zoo.Content;
using Zoo.Simulation;

namespace Zoo.Tests
{
    public sealed class DashMovementTests
    {
        static MovementDefinition Definition(float speed = 6, float interval = 2, float duration = 0.3f)
            => new() { kind = "dash", speed = speed, intervalSeconds = interval, durationSeconds = duration };

        static ContentCatalog Catalog(MovementDefinition movement)
        {
            var root = Path.Combine(Application.streamingAssetsPath, "Zoo");
            var animals = JsonUtility.FromJson<AnimalFile>(File.ReadAllText(Path.Combine(root, "animals.json")));
            animals.animals[0].movement = movement;
            return new ContentCatalog(animals,
                JsonUtility.FromJson<InteractionFile>(File.ReadAllText(Path.Combine(root, "interactions.json"))),
                JsonUtility.FromJson<LevelDefinition>(File.ReadAllText(Path.Combine(root, "level.json"))));
        }

        [Test] public void ValidDashConfigurationCreatesStrategy()
        {
            var catalog = Catalog(Definition());
            Assert.That(MovementFactory.Create(catalog.Animal("frog").movement), Is.TypeOf<DashMovement>());
        }

        [Test] public void DashMovesThenRestsAndStartsNextCycle()
        {
            var dash = new DashMovement(Definition(6, 2, 0.5f));
            Assert.That(dash.Speed(0.25f), Is.EqualTo(6));
            Assert.That(dash.Speed(0.25f), Is.EqualTo(6));
            Assert.That(dash.Speed(1.5f), Is.Zero);
            Assert.That(dash.Speed(0.25f), Is.EqualTo(6));
            Assert.That(dash.VisualHeight, Is.Zero);
        }

        [TestCase(0.02f, 100)]
        [TestCase(0.125f, 16)]
        [TestCase(0.4f, 5)]
        [TestCase(2f, 1)]
        public void CycleDistanceDoesNotDependOnPhysicsStep(float step, int steps)
        {
            var dash = new DashMovement(Definition());
            float distance = 0;
            for (int i = 0; i < steps; i++) distance += dash.Speed(step) * step;
            Assert.That(distance, Is.EqualTo(1.8f).Within(0.0001f));
        }

        [Test] public void OneStepCanCrossMultipleCycles()
        {
            var dash = new DashMovement(Definition(6, 2, 0.5f));
            Assert.That(dash.Speed(4.25f) * 4.25f, Is.EqualTo(7.5f).Within(0.0001f));
        }

        [Test] public void StrategiesDoNotSharePhase()
        {
            var definition = Definition(6, 2, 0.5f);
            var a = MovementFactory.Create(definition);
            var b = MovementFactory.Create(definition);
            a.Speed(0.5f);
            Assert.That(a.Speed(0.25f), Is.Zero);
            Assert.That(b.Speed(0.25f), Is.EqualTo(6));
        }

        [Test] public void ZeroAndNegativeStepsDoNotAdvancePhase()
        {
            var dash = new DashMovement(Definition(6, 2, 0.5f));
            Assert.That(dash.Speed(0), Is.Zero);
            Assert.That(dash.Speed(-1), Is.Zero);
            Assert.That(dash.Speed(0.5f), Is.EqualTo(6));
        }

        [Test] public void FullDurationProducesContinuousSpeed()
        {
            var dash = new DashMovement(Definition(6, 2, 2));
            for (int i = 0; i < 30; i++) Assert.That(dash.Speed(0.3f), Is.EqualTo(6).Within(0.0001f));
        }

        [TestCase(0, 2, 0.3f)]
        [TestCase(-1, 2, 0.3f)]
        [TestCase(float.NaN, 2, 0.3f)]
        [TestCase(float.PositiveInfinity, 2, 0.3f)]
        [TestCase(6, 0, 0.3f)]
        [TestCase(6, -1, 0.3f)]
        [TestCase(6, float.NaN, 0.3f)]
        [TestCase(6, float.PositiveInfinity, 0.3f)]
        [TestCase(6, 2, 0)]
        [TestCase(6, 2, -1)]
        [TestCase(6, 2, float.NaN)]
        [TestCase(6, 2, float.PositiveInfinity)]
        [TestCase(6, 2, 3)]
        public void InvalidDashConfigurationIsRejected(float speed, float interval, float duration)
            => Assert.Throws<ArgumentException>(() => Catalog(Definition(speed, interval, duration)));
    }
}
