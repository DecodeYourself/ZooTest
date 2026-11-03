using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zoo.Content;
using Zoo.Simulation;

namespace Zoo.Tests
{
    public sealed class ContactLifecycleTests
    {
        readonly List<GameObject> objects = new();
        PhysicsMaterial material;
        AnimalActor Make(int id, string role, InteractionResolver resolver, MovementDefinition movement = null)
        {
            var go = new GameObject("Test animal");
            go.transform.position = new Vector3(1000 + id * 10, 0, 1000);
            objects.Add(go);
            var actor = go.AddComponent<AnimalActor>();
            actor.Initialize(id, new AnimalDefinition { id = "test", foodRole = role,
                movement = movement ?? new MovementDefinition { kind = "linear", speed = 1 } }, resolver, material);
            return actor;
        }
        static ContentCatalog Catalog()
        {
            var root = Path.Combine(Application.streamingAssetsPath, "Zoo");
            return ContentCatalog.Parse(File.ReadAllText(Path.Combine(root, "animals.json")),
                File.ReadAllText(Path.Combine(root, "interactions.json")), File.ReadAllText(Path.Combine(root, "level.json")));
        }
        [UnityTest] public IEnumerator DuplicateAndMultiContactCountVictimOnce()
        {
            material = new PhysicsMaterial();
            var resolver = new InteractionResolver();
            var prey = Make(1, "prey", resolver);
            var predator = Make(2, "predator", resolver);
            var otherPredator = Make(3, "predator", resolver);
            int meals = 0;
            resolver.Eaten += _ => meals++;
            resolver.Enqueue(prey, predator);
            resolver.Enqueue(predator, prey);
            resolver.Enqueue(prey, otherPredator);
            resolver.Resolve(Catalog());
            Assert.That(prey.IsAlive, Is.False);
            Assert.That(prey.TryDie(), Is.False);
            Assert.That(resolver.DeadPrey, Is.EqualTo(1));
            Assert.That(meals, Is.EqualTo(1));
            resolver.Enqueue(predator, otherPredator);
            resolver.Enqueue(otherPredator, predator);
            resolver.Resolve(Catalog());
            Assert.That(predator.IsAlive, Is.True);
            Assert.That(otherPredator.IsAlive, Is.False);
            Assert.That(resolver.DeadPredators, Is.EqualTo(1));
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var go in objects) if (go) Object.Destroy(go);
            objects.Clear();
            if (material) Object.Destroy(material);
            yield return null;
        }

        [UnityTest] public IEnumerator PresentationMovesBetweenPhysicsSteps()
        {
            var oldStep = Time.fixedDeltaTime;
            var oldRate = Application.targetFrameRate;
            var randomState = Random.state;
            try
            {
                // A deliberately coarse step makes missing presentation interpolation observable.
                Time.fixedDeltaTime = 0.1f;
                Application.targetFrameRate = 120;
                Random.InitState(42);
                material = new PhysicsMaterial();
                var actor = Make(1, "prey", new InteractionResolver(), new MovementDefinition
                { kind = "jump", distance = 2, durationSeconds = 1, intervalSeconds = 1 });
                actor.gameObject.AddComponent<MovementTestDriver>().Actor = actor;
                bool heightChangedBetweenTicks = false;
                bool positionChangedBetweenTicks = false;
                yield return new WaitForFixedUpdate();
                var lastTick = Time.fixedTime;
                var lastHeight = actor.VisualRoot.localPosition.y;
                var lastPosition = actor.transform.position;
                for (int frame = 0; frame < 90; frame++)
                {
                    yield return null;
                    if (Time.fixedTime == lastTick)
                    {
                        heightChangedBetweenTicks |= Mathf.Abs(actor.VisualRoot.localPosition.y - lastHeight) > 0.00001f;
                        positionChangedBetweenTicks |= Vector3.Distance(actor.transform.position, lastPosition) > 0.00001f;
                    }
                    lastTick = Time.fixedTime;
                    lastHeight = actor.VisualRoot.localPosition.y;
                    lastPosition = actor.transform.position;
                    if (heightChangedBetweenTicks && positionChangedBetweenTicks) break;
                }
                Assert.That(heightChangedBetweenTicks, Is.True, "Jump visual must animate between physics steps.");
                Assert.That(positionChangedBetweenTicks, Is.True, "Body presentation must interpolate between physics steps.");
            }
            finally
            {
                Time.fixedDeltaTime = oldStep;
                Application.targetFrameRate = oldRate;
                Random.state = randomState;
            }
        }
    }

    public sealed class MovementTestDriver : MonoBehaviour
    {
        public AnimalActor Actor;
        void FixedUpdate() => Actor.Tick(Time.fixedDeltaTime, new Rect(-2000, -2000, 4000, 4000));
    }
}
