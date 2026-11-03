using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zoo.Presentation;

namespace Zoo.Content
{
    [Serializable] public sealed class ReleaseManifest
    {
        public int schemaVersion;
        public string releaseId;
        public string platform;
        public string catalogUrl;
        public string animalsAddress;
        public string interactionsAddress;
        public string levelAddress;
    }

    public sealed class RemoteReleaseLoader : IDisposable
    {
        // Keep loaded catalogs available for future visual loads. Versioned URLs are immutable.
        readonly Dictionary<string, IResourceLocator> catalogs = new();
        readonly CancellationTokenSource lifetime = new();
        readonly string providerSuffix = Guid.NewGuid().ToString("N");
        bool disposed;

        public async UniTask<ContentCatalog> LoadAsync(string manifestUrl, AnimalViewLoader visuals, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (disposed) throw new ObjectDisposedException(nameof(RemoteReleaseLoader));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
            var token = linked.Token;
            using var request = UnityWebRequest.Get(manifestUrl + "?t=" + DateTime.UtcNow.Ticks);
            request.timeout = 20;
            await request.SendWebRequest().WithCancellation(token, cancelImmediately: true);
            if (request.result != UnityWebRequest.Result.Success) throw new InvalidOperationException(request.error);
            var manifest = JsonUtility.FromJson<ReleaseManifest>(request.downloadHandler.text);
            if (manifest == null || manifest.schemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.releaseId)
                || !Uri.TryCreate(manifest.catalogUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new ArgumentException("Invalid release manifest");
            if (manifest.platform != "StandaloneWindows64" || Application.platform != RuntimePlatform.WindowsEditor
                && Application.platform != RuntimePlatform.WindowsPlayer)
                throw new ArgumentException("This release is built for StandaloneWindows64");

            bool added = false;
            IResourceLocator locator = null;
            try
            {
                if (!catalogs.TryGetValue(manifest.catalogUrl, out locator))
                {
                    var handle = Addressables.LoadContentCatalogAsync(manifest.catalogUrl, false, "_" + manifest.releaseId + "_" + providerSuffix);
                    bool adopted = false;
                    try
                    {
                        locator = await handle.ToUniTask(cancellationToken: token, cancelImmediately: true, autoReleaseWhenCanceled: false);
                        token.ThrowIfCancellationRequested();
                        if (locator == null) throw new InvalidOperationException("Remote catalog failed to load", handle.OperationException);
                        catalogs.Add(manifest.catalogUrl, locator);
                        added = adopted = true;
                    }
                    finally
                    {
                        // Addressables cannot reliably abort native downloads. If cancellation wins,
                        // also remove a catalog that registers itself when the native operation finishes.
                        if (!adopted && handle.IsValid())
                        {
                            if (handle.IsDone) RemoveLocator(handle);
                            else handle.Completed += RemoveLocator;
                        }
                        if (handle.IsValid()) Addressables.Release(handle);
                    }
                }
                var animals = await ReadConfig(locator, manifest.animalsAddress, token);
                var interactions = await ReadConfig(locator, manifest.interactionsAddress, token);
                var level = await ReadConfig(locator, manifest.levelAddress, token);
                token.ThrowIfCancellationRequested();
                var candidate = ContentCatalog.Parse(animals, interactions, level);
                if (candidate.Version != manifest.releaseId) throw new ArgumentException("Release/config version mismatch");
                var data = JsonUtility.FromJson<AnimalFile>(animals);
                foreach (var view in data.views) await visuals.PreloadAsync(view.address, token);
                token.ThrowIfCancellationRequested();
                return candidate;
            }
            catch
            {
                if (added)
                {
                    catalogs.Remove(manifest.catalogUrl);
                    Addressables.RemoveResourceLocator(locator);
                }
                throw;
            }
        }

        static void RemoveLocator(AsyncOperationHandle<IResourceLocator> handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                Addressables.RemoveResourceLocator(handle.Result);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            foreach (var locator in catalogs.Values) Addressables.RemoveResourceLocator(locator);
            catalogs.Clear();
            lifetime.Dispose();
        }

        static async UniTask<string> ReadConfig(IResourceLocator locator, string address, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(address) || !locator.Locate(address, typeof(TextAsset), out var locations)
                || locations.Count != 1) throw new ArgumentException("Missing/ambiguous release config: " + address);
            var handle = Addressables.LoadAssetAsync<TextAsset>(locations[0]);
            try
            {
                var asset = await handle.ToUniTask(cancellationToken: cancellationToken, cancelImmediately: true, autoReleaseWhenCanceled: false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!asset) throw new InvalidOperationException("Config failed to load: " + address, handle.OperationException);
                return asset.text;
            }
            finally { if (handle.IsValid()) Addressables.Release(handle); }
        }
    }
}
