using System;
using System.IO;
using Template.Game.Boot;
using Template.Game.Sample;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Template.EditorTools.Setup
{
    /// <summary>
    /// Reproducible project setup in code instead of clicking through Player Settings.
    /// Run once after creating a game from the template: Template → Apply Project Setup.
    /// Safe to re-run; it never overwrites existing scenes.
    /// </summary>
    public static class TemplateSetup
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";

        [MenuItem("Template/Apply Project Setup", priority = 0)]
        public static void ApplyAll()
        {
            TextSetup.EnsureEssentials();
            ApplyPlayerSettings();
            CreateScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Template] Project setup applied: TextMeshPro resources, player settings, scenes and build list.");
        }

        /// <summary>Batch-mode entry: Unity -batchmode -executeMethod Template.EditorTools.Setup.TemplateSetup.ApplyAllBatch</summary>
        public static void ApplyAllBatch()
        {
            try
            {
                ApplyAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Tran Van Truong";
            if (string.IsNullOrEmpty(PlayerSettings.productName) || PlayerSettings.productName == "unity-mobile-template")
            {
                PlayerSettings.productName = "Unity Mobile Template";
            }

            // Change per game, e.g. com.tranvantruong.cadenceclub. Must be unique on Google Play.
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.tranvantruong.template");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.tranvantruong.template");

            // Portrait phone game.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Android: IL2CPP + ARM64 is what Google Play requires.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25; // Android 7.1, Unity 6.3's minimum
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);

            // Windows build for recruiters on laptops: a phone-shaped resizable window, Mono backend
            // (builds on Linux CI runners without the Windows IL2CPP module).
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
        }

        public static void CreateScenes()
        {
            Directory.CreateDirectory(ScenesFolder);
            string boot = CreateScene("Boot", typeof(GameBootstrap));
            string title = CreateScene("Title", typeof(TitleController));
            string game = CreateScene("Game", typeof(SampleGameController));

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(boot, true),
                new EditorBuildSettingsScene(title, true),
                new EditorBuildSettingsScene(game, true),
            };
        }

        private static string CreateScene(string name, Type controller)
        {
            string path = $"{ScenesFolder}/{name}.unity";
            if (File.Exists(path))
            {
                return path;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.10f, 0.14f);
            cameraObject.AddComponent<AudioListener>();
            AddGlobalLight2D();

            new GameObject($"{name} Controller", controller);

            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        /// <summary>
        /// URP 2D draws sprites with lit materials, so a scene without a 2D light renders them black.
        /// Added through reflection so the template still compiles in projects without URP.
        /// </summary>
        private static void AddGlobalLight2D()
        {
            var lightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
            if (lightType == null)
            {
                return;
            }

            var light = new GameObject("Global Light 2D").AddComponent(lightType);
            var typeProperty = lightType.GetProperty("lightType");
            if (typeProperty != null && typeProperty.CanWrite)
            {
                typeProperty.SetValue(light, Enum.Parse(typeProperty.PropertyType, "Global"));
            }

            lightType.GetProperty("intensity")?.SetValue(light, 1f);
        }
    }
}
