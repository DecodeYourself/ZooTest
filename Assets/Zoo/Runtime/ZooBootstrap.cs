using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Zoo.Content;
using Zoo.Presentation;
using Zoo.Simulation;

namespace Zoo
{
    /// <summary>Composition root and Unity lifecycle bridge; game rules live in injected services.</summary>
    public sealed class ZooBootstrap : MonoBehaviour
    {
        [SerializeField] string releaseManifestUrl = "http://127.0.0.1:8080/release-2/release.json";
        CancellationTokenSource lifetime;
        AnimalViewLoader views;
        AnimalFactory factory;
        RemoteReleaseLoader remote;
        InteractionResolver interactions;
        ZooContentController contentCommands;
        ZooWorld world;
        ZooHud hud;

        public int AliveCount => world?.AliveCount ?? 0;
        public int DeadPrey => world?.DeadPrey ?? 0;
        public int DeadPredators => world?.DeadPredators ?? 0;
        public string ContentVersion => world?.ContentVersion;

        void Awake()
        {
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, Application.exitCancellationToken);
            try
            {
                var camera = Camera.main;
                var arena = new CameraArena(camera);
                var content = new ContentService();
                interactions = new InteractionResolver();
                views = new AnimalViewLoader();
                remote = new RemoteReleaseLoader();
                factory = new AnimalFactory(transform, interactions, views);
                world = new ZooWorld(content, interactions, new SpawnService(factory), arena);
                contentCommands = new ZooContentController(content, remote, views, releaseManifestUrl);
                hud = new GameObject("Zoo HUD", typeof(RectTransform)).AddComponent<ZooHud>();
                hud.transform.SetParent(transform, false);
                hud.Initialize(camera, () => contentCommands.ReloadLocalAsync(lifetime.Token).Forget(),
                    () => contentCommands.UpdateRemoteAsync(lifetime.Token).Forget());
                interactions.Eaten += hud.ShowTasty;
                if (!FindFirstObjectByType<EventSystem>())
                {
                    var events = new GameObject("EventSystem", typeof(EventSystem));
                    events.transform.SetParent(transform, false);
                    events.AddComponent<InputSystemUIInputModule>();
                }
            }
            catch (Exception error) { enabled = false; Debug.LogException(error); }
        }

        void Start() => contentCommands.ReloadLocalAsync(lifetime.Token).Forget();
        void FixedUpdate()
        {
            if (!lifetime.IsCancellationRequested) world.Tick(Time.fixedDeltaTime, lifetime.Token);
        }
        void Update() => hud.Refresh(DeadPrey, DeadPredators, AliveCount, contentCommands.Status);

        void OnDestroy()
        {
            lifetime?.Cancel();
            if (hud && interactions != null) interactions.Eaten -= hud.ShowTasty;
            world?.Dispose();
            factory?.Dispose();
            views?.Dispose();
            remote?.Dispose();
            lifetime?.Dispose();
        }
    }
}
