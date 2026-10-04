using System;
using Template.Core.Save;
using Template.Feel;
using Template.Infra;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

namespace Template.Game.Debugging
{
    /// <summary>
    /// FPS counter plus a small cheat panel, in the editor and development builds only.
    /// Toggle with a four-finger tap on a phone or F1 on a keyboard.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private bool _visible;
        private bool _wasFourFingers;
        private float _fps;
        private float _timer;
        private int _frames;

        public static void CreateIfDevelopment()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                return;
            }

            var go = new GameObject("[Debug]");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugOverlay>();
        }

        private void Update()
        {
            _frames++;
            _timer += Time.unscaledDeltaTime;
            if (_timer >= 0.5f)
            {
                _fps = _frames / _timer;
                _frames = 0;
                _timer = 0f;
            }

            bool fourFingers = CountTouches() >= 4;
            if (fourFingers && !_wasFourFingers)
            {
                _visible = !_visible;
            }

            _wasFourFingers = fourFingers;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.dpi / 160f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.Label($"{_fps:0} fps");
            if (!_visible)
            {
                return;
            }

            GUILayout.BeginVertical("box", GUILayout.Width(260));
            GUILayout.Label($"Memory {Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)} MB · GC {GC.CollectionCount(0)}");

            if (Services.TryGet<SaveService>(out var save))
            {
                GUILayout.Label($"Best {save.Data.bestScore} · Runs {save.Data.totalRuns} · from {save.LoadedFrom}");
                if (GUILayout.Button("Reset save"))
                {
                    save.ResetAll();
                }
            }

            JuiceFx.ReduceMotion = GUILayout.Toggle(JuiceFx.ReduceMotion, "Reduce motion");
            GUILayout.Label($"Time scale {Time.timeScale:0.00}");
            Time.timeScale = GUILayout.HorizontalSlider(Time.timeScale, 0f, 2f);
            GUILayout.EndVertical();
        }

        private static int CountTouches()
        {
            var screen = Touchscreen.current;
            if (screen == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var touch in screen.touches)
            {
                if (touch.press.isPressed)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
