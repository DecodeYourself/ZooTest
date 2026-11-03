using System.Collections;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zoo.Content;
using Zoo.Presentation;
using Zoo.Simulation;

namespace Zoo.Tests
{
    public sealed class ZooWorldTests
    {
        static ContentCatalog Catalog(string version, string species, int initial)
        {
            var root = Path.Combine(Application.streamingAssetsPath, "Zoo");
            var animals = JsonUtility.FromJson<AnimalFile>(File.ReadAllText(Path.Combine(root, "animals.json")));
            animals.contentVersion = version;
            foreach (var view in animals.views) view.address = "";
            var level = JsonUtility.FromJson<LevelDefinition>(File.ReadAllText(Path.Combine(root, "level.json")));
            level.initialPopulation = initial;
            level.spawnMinSeconds = level.spawnMaxSeconds = 1;
            level.spawnTable = new[] { new SpawnEntry { animalId = species, weight = 1 } };
            return new ContentCatalog(animals,
                JsonUtility.FromJson<InteractionFile>(File.ReadAllText(Path.Combine(root, "interactions.json"))), level);
        }

        [UnityTest] public IEnumerator ContentSwitchKeepsPopulationAndChangesFutureSpawns()
        {
            var parent = new GameObject("World test parent");
            var cameraObject = new GameObject("World test camera", typeof(Camera));
            cameraObject.transform.position = new Vector3(1000, 25, 1000);
            var content = new ContentService();
            var interactions = new InteractionResolver();
            using var views = new AnimalViewLoader();
            using var factory = new AnimalFactory(parent.transform, interactions, views);
            var spawns = new SpawnService(factory);
            using var world = new ZooWorld(content, interactions, spawns, new CameraArena(cameraObject.GetComponent<Camera>()));
            try
            {
                world.Tick(0.02f, CancellationToken.None);
                Assert.That(world.AliveCount, Is.Zero);
                content.Stage(Catalog("first", "frog", 2));
                Assert.That(world.ContentVersion, Is.Null);
                world.Tick(0.02f, CancellationToken.None);
                Assert.That(world.AliveCount, Is.EqualTo(2));
                var original = parent.GetComponentsInChildren<AnimalActor>();
                Assert.That(original[0].Definition.id, Is.EqualTo("frog"));
                content.Stage(Catalog("second", "snake", 10));
                world.Tick(0.02f, CancellationToken.None);
                Assert.That(world.ContentVersion, Is.EqualTo("second"));
                Assert.That(world.AliveCount, Is.EqualTo(2), "Reload must not recreate the initial population.");
                Assert.That(original[0].Definition.id, Is.EqualTo("frog"));
                world.Tick(1.01f, CancellationToken.None);
                Assert.That(world.AliveCount, Is.EqualTo(3));
                var current = parent.GetComponentsInChildren<AnimalActor>();
                Assert.That(current[2].Definition.id, Is.EqualTo("snake"));
                var bounds = new CameraArena(cameraObject.GetComponent<Camera>()).Bounds;
                Assert.That(bounds.Contains(new Vector2(current[2].transform.position.x, current[2].transform.position.z)), Is.True);
                world.Dispose();
                Assert.That(world.AliveCount, Is.Zero);
                Assert.That(world.DeadPrey, Is.Zero, "World shutdown is not a food-chain death.");
                Assert.That(original[0].IsAlive, Is.False);
                yield return null;
            }
            finally { Object.Destroy(parent); Object.Destroy(cameraObject); }
        }
    }
}
