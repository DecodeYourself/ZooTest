using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Zoo.Content;

namespace Zoo.Presentation
{
    /// <summary>Owns reusable fallback instances and shared materials for one world's lifetime.</summary>
    public sealed class FallbackViewPool : IDisposable
    {
        public sealed class Lease : IDisposable
        {
            readonly FallbackViewPool owner;
            internal readonly (PrimitiveType, Color) Key;
            internal CancellationTokenRegistration Registration;
            internal bool Returned;
            public GameObject Instance { get; }
            internal Lease(FallbackViewPool owner, GameObject instance, (PrimitiveType, Color) key)
            { this.owner = owner; Instance = instance; Key = key; }
            public void Dispose() => owner.Return(this);
        }

        readonly Dictionary<(PrimitiveType, Color), Stack<GameObject>> inactive = new();
        readonly Dictionary<Color, Material> materials = new();
        readonly HashSet<Lease> active = new();
        readonly int maxInactivePerKey;
        Transform storage;
        bool disposed;

        public FallbackViewPool(int maxInactivePerKey = 32)
        {
            if (maxInactivePerKey < 0) throw new ArgumentOutOfRangeException(nameof(maxInactivePerKey));
            this.maxInactivePerKey = maxInactivePerKey;
        }

        public Lease Rent(ViewDefinition view, Transform parent, CancellationToken ownerToken)
        {
            ownerToken.ThrowIfCancellationRequested();
            if (disposed) throw new ObjectDisposedException(nameof(FallbackViewPool));
            ColorUtility.TryParseHtmlString(view.color, out var color);
            var shape = view.fallback == "sphere" ? PrimitiveType.Sphere : PrimitiveType.Capsule;
            var key = (shape, color);
            if (!inactive.TryGetValue(key, out var available)) inactive.Add(key, available = new Stack<GameObject>());
            GameObject instance = null;
            while (available.Count > 0 && !instance) instance = available.Pop();
            if (!instance)
            {
                instance = GameObject.CreatePrimitive(shape);
                var collider = instance.GetComponent<Collider>();
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
                if (!materials.TryGetValue(color, out var material))
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
                    materials.Add(color, material);
                }
                instance.GetComponent<Renderer>().sharedMaterial = material;
            }
            instance.name = "Fallback " + view.id;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = new Vector3(view.scale[0], view.scale[1], view.scale[2]);
            instance.SetActive(true);
            var lease = new Lease(this, instance, key);
            active.Add(lease);
            lease.Registration = ownerToken.Register(state => ((Lease)state).Dispose(), lease);
            if (lease.Returned) lease.Registration.Dispose();
            return lease;
        }

        void Return(Lease lease)
        {
            if (lease.Returned) return;
            lease.Returned = true;
            lease.Registration.Dispose();
            active.Remove(lease);
            var instance = lease.Instance;
            if (!instance) return;
            var available = inactive[lease.Key];
            if (disposed || available.Count >= maxInactivePerKey) { UnityEngine.Object.Destroy(instance); return; }
            if (!storage)
            {
                var root = new GameObject("Zoo fallback pool");
                root.SetActive(false);
                storage = root.transform;
            }
            instance.SetActive(false);
            instance.transform.SetParent(storage, false);
            available.Push(instance);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var lease in new List<Lease>(active)) lease.Dispose();
            foreach (var available in inactive.Values)
                while (available.Count > 0)
                {
                    var instance = available.Pop();
                    if (instance) UnityEngine.Object.Destroy(instance);
                }
            inactive.Clear();
            if (storage) UnityEngine.Object.Destroy(storage.gameObject);
            foreach (var material in materials.Values) if (material) UnityEngine.Object.Destroy(material);
            materials.Clear();
        }
    }
}
