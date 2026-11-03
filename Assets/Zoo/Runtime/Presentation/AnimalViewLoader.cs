using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zoo.Content;
using Zoo.Simulation;

namespace Zoo.Presentation
{
    public sealed class AnimalViewLoader
    {
        readonly Dictionary<string, Task<GameObject>> prefabs = new();
        readonly List<AsyncOperationHandle<GameObject>> handles = new();
        bool disposed;
        public async Task PreloadAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Missing visual address");
            if (!prefabs.TryGetValue(address, out var task))
            {
                task = LoadAsync(address);
                prefabs.Add(address, task);
            }
            try
            {
                if (!await task) throw new InvalidOperationException("Missing prefab: " + address);
            }
            catch { prefabs.Remove(address); throw; }
        }
        public async Task AttachAsync(AnimalActor actor, ViewDefinition view)
        {
            var fallback = MakeFallback(view);
            fallback.transform.SetParent(actor.VisualRoot, false);
            if (string.IsNullOrEmpty(view.address)) return;
            try
            {
                if (!prefabs.TryGetValue(view.address, out var task))
                {
                    task = LoadAsync(view.address);
                    prefabs.Add(view.address, task);
                }
                var prefab = await task;
                if (disposed || !actor || !actor.IsAlive || !prefab) return;
                UnityEngine.Object.Instantiate(prefab, actor.VisualRoot, false);
                UnityEngine.Object.Destroy(fallback);
            }
            catch (Exception error) { Debug.LogWarning("Visual fallback for " + view.id + ": " + error.Message); }
        }

        async Task<GameObject> LoadAsync(string address)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(address);
            handles.Add(handle);
            return await handle.Task;
        }

        public void Dispose()
        {
            disposed = true;
            foreach (var handle in handles) if (handle.IsValid()) Addressables.Release(handle);
            handles.Clear();
        }

        public static GameObject MakeFallback(ViewDefinition view)
        {
            var result = GameObject.CreatePrimitive(view.fallback == "sphere" ? PrimitiveType.Sphere : PrimitiveType.Capsule);
            result.name = "Fallback " + view.id;
            var collider = result.GetComponent<Collider>();
            collider.enabled = false;
            UnityEngine.Object.Destroy(collider);
            result.transform.localScale = new Vector3(view.scale[0], view.scale[1], view.scale[2]);
            var renderer = result.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ColorUtility.TryParseHtmlString(view.color, out var color);
            material.color = color;
            renderer.sharedMaterial = material;
            result.AddComponent<OwnedMaterial>().Value = material;
            return result;
        }
    }

    public sealed class OwnedMaterial : MonoBehaviour
    {
        public Material Value;
        void OnDestroy() { if (Value) Destroy(Value); }
    }
}
