using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Zoo.Content;

namespace Zoo.Editor
{
    public static class Release2Builder
    {
        public static string VerificationStatus { get; private set; }

        [MenuItem("Zoo/Content/Verify CDN release-2 download")]
        public static async void VerifyDownload()
        {
            if (!EditorApplication.isPlaying) { VerificationStatus = "Enter Play Mode before verifying downloads"; return; }
            VerificationStatus = "Downloading";
            var visuals = new Zoo.Presentation.AnimalViewLoader();
            try
            {
                var release = await new RemoteReleaseLoader().LoadAsync("http://127.0.0.1:8080/release-2/release.json", visuals);
                if (release.Animal("caracal").movement.kind != "jump" || release.Animal("caracal").foodRole != "predator")
                    throw new InvalidOperationException("Incorrect caracal definition");
                VerificationStatus = "PASS: " + release.Version + "; remote JSON and caracal prefab downloaded";
            }
            catch (Exception error) { VerificationStatus = "FAIL: " + error; Debug.LogError(VerificationStatus); }
            finally { visuals.Dispose(); }
        }

        [MenuItem("Zoo/Content/Build CDN release-2")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Build releases outside Play Mode");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new InvalidOperationException("Select StandaloneWindows64 first");
            const string source = "Assets/Zoo/Releases/release-2/";
            ContentCatalog.Parse(File.ReadAllText(source + "animals.json"), File.ReadAllText(source + "interactions.json"), File.ReadAllText(source + "level.json"));
            AssetDatabase.Refresh();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var profiles = settings.profileSettings;
            var profile = profiles.GetProfileId("CDN release-2");
            if (string.IsNullOrEmpty(profile)) profile = profiles.AddProfile("CDN release-2", settings.activeProfileId);
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../cdn_server/release-2")).Replace('\\', '/');
            Directory.CreateDirectory(root);
            profiles.SetValue(profile, "Remote.BuildPath", root + "/[BuildTarget]");
            profiles.SetValue(profile, "Remote.LoadPath", "http://127.0.0.1:8080/release-2/[BuildTarget]");
            settings.activeProfileId = profile;
            settings.BuildRemoteCatalog = true;
            settings.UniqueBundleIds = true;
            settings.OverridePlayerVersion = "release-2";
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, "Remote.BuildPath");
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, "Remote.LoadPath");
            var group = settings.FindGroup("Release 2") ?? settings.CreateGroup("Release 2", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
            schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;
            Add(settings, group, "Assets/Zoo/Visuals/caracal-v1.prefab", "animals/caracal/v1");
            foreach (var name in new[] { "animals", "interactions", "level" })
                Add(settings, group, source + name + ".json", "configs/release-2/" + name);
            for (int i = 0; i < settings.DataBuilders.Count; i++)
                if (settings.DataBuilders[i].GetType().Name == "BuildScriptPackedPlayMode") settings.ActivePlayModeDataBuilderIndex = i;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
            var catalogs = Directory.GetFiles(root, "catalog*.bin", SearchOption.AllDirectories);
            if (catalogs.Length != 1) throw new InvalidOperationException("Expected one binary catalog in release-2");
            var relative = catalogs[0].Substring(root.Length).Replace('\\', '/');
            var manifest = new ReleaseManifest
            {
                schemaVersion = 1, releaseId = "release-2", platform = "StandaloneWindows64",
                catalogUrl = "http://127.0.0.1:8080/release-2" + relative,
                animalsAddress = "configs/release-2/animals", interactionsAddress = "configs/release-2/interactions", levelAddress = "configs/release-2/level"
            };
            // Publish manifest last: clients must never observe a release before its bundles exist.
            File.WriteAllText(Path.Combine(root, "release.json"), JsonUtility.ToJson(manifest, true));
            var serverRoot = Directory.GetParent(root).FullName;
            foreach (var name in new[] { "cdn_server.py", "start_local_cdn.cmd", "README.txt" })
                File.Copy(Path.Combine("Tools/LocalCdn", name), Path.Combine(serverRoot, name), true);
            Debug.Log("CDN release-2 published: " + root);
        }

        static void Add(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) throw new FileNotFoundException("Missing release asset", path);
            settings.CreateOrMoveEntry(guid, group).address = address;
        }
    }
}
