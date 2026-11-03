using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Zoo.Content;

namespace Zoo.Editor
{
    public static class ZooProjectSetup
    {
        [MenuItem("Zoo/Set up prototype scene and visuals")]
        public static void Setup()
        {
            PlayerSettings.runInBackground = true;
            const string scenePath = "Assets/Zoo/Scenes/Zoo.unity";
            Directory.CreateDirectory("Assets/Zoo/Scenes");
            Directory.CreateDirectory("Assets/Zoo/Visuals");
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.UniqueBundleIds = true;
            settings.BuildRemoteCatalog = false; // Default profile works offline without a content server.
            var profile = settings.profileSettings;
            var remote = profile.GetProfileId("RemoteLocalhost");
            if (string.IsNullOrEmpty(remote)) remote = profile.AddProfile("RemoteLocalhost", settings.activeProfileId);
            profile.SetValue(remote, "Remote.BuildPath", "ServerData/[BuildTarget]");
            profile.SetValue(remote, "Remote.LoadPath", "http://127.0.0.1:8080/[BuildTarget]");
            var group = settings.FindGroup("Animal Visuals") ?? settings.CreateGroup("Animal Visuals", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, "Local.BuildPath");
            schema.LoadPath.SetVariableByName(settings, "Local.LoadPath");
            var source = JsonUtility.FromJson<AnimalFile>(File.ReadAllText("Assets/StreamingAssets/Zoo/animals.json"));
            foreach (var view in source.views)
            {
                var path = "Assets/Zoo/Visuals/" + view.id + ".prefab";
                if (!File.Exists(path)) CreateVisual(view, path);
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).address = view.address;
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            // Do not overwrite an existing authored scene when rerunning setup.
            if (File.Exists(scenePath)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(new Vector3(0, 25, 0), Quaternion.Euler(90, 0, 0));
            var cam = camera.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.07f, 0.065f);
            var light = new GameObject("Sun", typeof(Light));
            light.transform.rotation = Quaternion.Euler(55, -25, 0);
            light.GetComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 1.5f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Zoo ground";
            ground.transform.localScale = Vector3.one * 20;
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMaterial.color = new Color(0.065f, 0.15f, 0.12f);
            AssetDatabase.CreateAsset(groundMaterial, "Assets/Zoo/Visuals/Ground.mat");
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            new GameObject("Zoo").AddComponent<ZooBootstrap>();
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
        }

        static void CreateVisual(ViewDefinition view, string path)
        {
            var go = GameObject.CreatePrimitive(view.fallback == "sphere" ? PrimitiveType.Sphere : PrimitiveType.Capsule);
            go.name = view.id;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(view.scale[0], view.scale[1], view.scale[2]);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ColorUtility.TryParseHtmlString(view.color, out var color);
            material.color = color;
            AssetDatabase.CreateAsset(material, "Assets/Zoo/Visuals/" + view.id + ".mat");
            go.GetComponent<Renderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        [MenuItem("Zoo/Content/Use remote localhost profile")]
        public static void UseRemote()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.activeProfileId = settings.profileSettings.GetProfileId("RemoteLocalhost");
            settings.BuildRemoteCatalog = true;
            var schema = settings.FindGroup("Animal Visuals").GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
            schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Zoo/Content/Build Addressables")]
        public static void BuildContent() => AddressableAssetSettings.BuildPlayerContent();
    }
}
