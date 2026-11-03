using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Zoo.Content;
using Zoo.Presentation;
using Zoo.Simulation;

namespace Zoo
{
    public sealed class ZooBootstrap : MonoBehaviour
    {
        readonly ContentService content = new();
        readonly InteractionResolver interactions = new();
        readonly List<AnimalActor> animals = new();
        AnimalViewLoader views;
        ZooHud hud;
        Camera worldCamera;
        PhysicsMaterial contactMaterial;
        float spawnTimer;
        int nextId;
        bool initialized;
        bool updatingVisuals;
        string visualStatus = "";
        public int AliveCount => animals.Count;
        public int DeadPrey => interactions.DeadPrey;
        public int DeadPredators => interactions.DeadPredators;
        public string ContentVersion => content.Current?.Version;

        async void Start()
        {
            worldCamera = Camera.main;
            views = new AnimalViewLoader();
            contactMaterial = new PhysicsMaterial("Animal contacts")
            { bounciness = 0.85f, dynamicFriction = 0, staticFriction = 0, bounceCombine = PhysicsMaterialCombine.Maximum };
            hud = new GameObject("Zoo HUD", typeof(RectTransform)).AddComponent<ZooHud>();
            hud.Initialize(worldCamera, () => _ = content.ReloadAsync(), () => _ = UpdateVisualsAsync());
            interactions.Eaten += hud.ShowTasty;
            if (!FindFirstObjectByType<EventSystem>())
            {
                var events = new GameObject("EventSystem", typeof(EventSystem));
                events.AddComponent<InputSystemUIInputModule>();
            }
            await content.ReloadAsync();
        }

        void FixedUpdate()
        {
            if (content.ApplyPending())
            {
                worldCamera.orthographicSize = content.Current.Level.cameraHalfHeight;
                spawnTimer = NextSpawnDelay();
                if (!initialized)
                {
                    initialized = true;
                    for (int i = 0; i < content.Current.Level.initialPopulation; i++) Spawn();
                }
            }
            if (!initialized) return;
            interactions.Resolve(content.Current);
            animals.RemoveAll(a => !a || !a.IsAlive);
            var bounds = VisibleBounds();
            foreach (var animal in animals) animal.Tick(Time.fixedDeltaTime, bounds);
            if ((spawnTimer -= Time.fixedDeltaTime) <= 0)
            { Spawn(); spawnTimer = NextSpawnDelay(); }
        }

        void Update()
        {
            if (hud) hud.Refresh(interactions.DeadPrey, interactions.DeadPredators, animals.Count,
                content.Status + (string.IsNullOrEmpty(visualStatus) ? "" : "\n" + visualStatus));
        }

        Rect VisibleBounds()
        {
            var half = worldCamera.orthographicSize;
            var width = half * worldCamera.aspect;
            var center = worldCamera.transform.position;
            return new Rect(center.x - width, center.z - half, width * 2, half * 2);
        }

        float NextSpawnDelay() => UnityEngine.Random.Range(content.Current.Level.spawnMinSeconds, content.Current.Level.spawnMaxSeconds);

        void Spawn()
        {
            var catalog = content.Current;
            var table = catalog.Level.spawnTable;
            float total = 0;
            foreach (var entry in table) total += entry.weight;
            float pick = UnityEngine.Random.value * total;
            var id = table[table.Length - 1].animalId;
            foreach (var entry in table) { if ((pick -= entry.weight) <= 0) { id = entry.animalId; break; } }
            var definition = catalog.Animal(id);
            var bounds = VisibleBounds();
            Vector3 position = Vector3.zero;
            // Prefer a free position to avoid spawning directly inside another animal.
            for (int attempt = 0; attempt < 20; attempt++)
            {
                position = new Vector3(UnityEngine.Random.Range(bounds.xMin + 1, bounds.xMax - 1), 0.5f,
                    UnityEngine.Random.Range(bounds.yMin + 1, bounds.yMax - 1));
                if (!Physics.CheckSphere(position, 0.6f)) break;
            }
            var go = new GameObject(id + " #" + (nextId + 1));
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var actor = go.AddComponent<AnimalActor>();
            actor.Initialize(++nextId, definition, interactions, contactMaterial);
            animals.Add(actor);
            _ = views.AttachAsync(actor, catalog.View(definition.viewId));
        }

        async Task UpdateVisualsAsync()
        {
            if (updatingVisuals) return;
            updatingVisuals = true;
            visualStatus = "Checking visual catalog...";
            try
            {
                var check = Addressables.CheckForCatalogUpdates(false);
                try
                {
                    var catalogs = await check.Task;
                    if (catalogs.Count == 0) { visualStatus = "Visual catalog is current"; return; }
                    var update = Addressables.UpdateCatalogs(catalogs, false);
                    try { await update.Task; }
                    finally { if (update.IsValid()) Addressables.Release(update); }
                    visualStatus = "Visual catalog updated; reload JSON to select new versioned addresses";
                }
                finally { if (check.IsValid()) Addressables.Release(check); }
            }
            catch (Exception error) { visualStatus = "Visual update failed: " + error.Message; Debug.LogWarning(visualStatus); }
            finally { updatingVisuals = false; }
        }

        void OnDestroy()
        {
            if (hud) interactions.Eaten -= hud.ShowTasty;
            views?.Dispose();
            if (contactMaterial) Destroy(contactMaterial);
        }
    }
}
