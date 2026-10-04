using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace CadenceClub.PlayModeTests
{
    /// <summary>
    /// Renders the main camera plus the overlay UI into a PNG (or TGA). Overlay canvases are moved into camera
    /// space for the shot and lifted above every sprite, which is how an overlay draws on screen.
    /// Does nothing under -nographics.
    /// </summary>
    public static class CameraShot
    {
        public static string Folder(string name) => Path.Combine(Application.dataPath, "..", "Logs", name);

        public static bool Available => Camera.main != null && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        public static void Save(string path, int width = 540, int height = 960)
        {
            var camera = Camera.main;
            if (!Available)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = new RenderMode[canvases.Length];
            var orders = new int[canvases.Length];
            var factors = new float[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                orders[i] = canvases[i].sortingOrder;
                factors[i] = canvases[i].scaleFactor;
                if (canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 1f;
                    canvases[i].sortingOrder = 10000 + orders[i];
                }

                // CanvasScaler only updates once a frame from the screen size; scale for this capture's size now,
                // the way a phone or tablet of that shape would.
                var scaler = canvases[i].GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null && scaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    var reference = scaler.referenceResolution;
                    float logWidth = Mathf.Log(width / reference.x, 2f);
                    float logHeight = Mathf.Log(height / reference.y, 2f);
                    canvases[i].scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
                }
            }

            var texture = RenderTexture.GetTemporary(width, height, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = texture;
            // Warm-up render: scripts that fit the camera to its aspect right before rendering (LevelController)
            // settle first; then the camera-space canvases lay out for the settled camera, and the real shot follows.
            camera.Render();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            camera.targetTexture = previous;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(texture);
            // .tga for frame sequences: cheap to encode, and file-protection tools that guard .png
            // (and block programs reading hundreds of them) leave it alone.
            bool tga = path.EndsWith(".tga", System.StringComparison.OrdinalIgnoreCase);
            File.WriteAllBytes(path, tga ? image.EncodeToTGA() : image.EncodeToPNG());
            Object.Destroy(image);

            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = modes[i];
                canvases[i].sortingOrder = orders[i];
                canvases[i].scaleFactor = factors[i];
            }
        }
    }
}
