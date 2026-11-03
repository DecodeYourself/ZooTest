using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    public sealed class RemoteReleaseLoader
    {
        // Keep loaded catalogs available for future visual loads. Versioned URLs are immutable.
        readonly Dictionary<string, IResourceLocator> catalogs = new();

        public async Task<ContentCatalog> LoadAsync(string manifestUrl, AnimalViewLoader visuals)
        {
            using var request = UnityWebRequest.Get(manifestUrl + "?t=" + DateTime.UtcNow.Ticks);
            request.timeout = 20;
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success) throw new InvalidOperationException(request.error);
            var manifest = JsonUtility.FromJson<ReleaseManifest>(request.downloadHandler.text);
            if (manifest == null || manifest.schemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.releaseId)
                || !Uri.TryCreate(manifest.catalogUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new ArgumentException("Invalid release manifest");
            if (manifest.platform != "StandaloneWindows64" || Application.platform != RuntimePlatform.WindowsEditor
                && Application.platform != RuntimePlatform.WindowsPlayer)
                throw new ArgumentException("This release is built for StandaloneWindows64");

            if (!catalogs.TryGetValue(manifest.catalogUrl, out var locator))
            {
                var handle = Addressables.LoadContentCatalogAsync(manifest.catalogUrl, false, "_" + manifest.releaseId);
                try
                {
                    locator = await handle.Task;
                    if (handle.Status != AsyncOperationStatus.Succeeded || locator == null)
                        throw new InvalidOperationException("Remote catalog failed to load", handle.OperationException);
                }
                finally { if (handle.IsValid()) Addressables.Release(handle); }
                catalogs.Add(manifest.catalogUrl, locator);
            }
            var animals = await ReadConfig(locator, manifest.animalsAddress);
            var interactions = await ReadConfig(locator, manifest.interactionsAddress);
            var level = await ReadConfig(locator, manifest.levelAddress);
            var candidate = ContentCatalog.Parse(animals, interactions, level);
            if (candidate.Version != manifest.releaseId) throw new ArgumentException("Release/config version mismatch");
            var data = JsonUtility.FromJson<AnimalFile>(animals);
            // Activation is transactional: every referenced visual must load before new definitions go live.
            foreach (var view in data.views) await visuals.PreloadAsync(view.address);
            return candidate;
        }

        static async Task<string> ReadConfig(IResourceLocator locator, string address)
        {
            if (string.IsNullOrWhiteSpace(address) || !locator.Locate(address, typeof(TextAsset), out var locations)
                || locations.Count != 1) throw new ArgumentException("Missing/ambiguous release config: " + address);
            var handle = Addressables.LoadAssetAsync<TextAsset>(locations[0]);
            try
            {
                var asset = await handle.Task;
                if (!asset) throw new InvalidOperationException("Config failed to load: " + address, handle.OperationException);
                return asset.text;
            }
            finally { if (handle.IsValid()) Addressables.Release(handle); }
        }
    }
}
