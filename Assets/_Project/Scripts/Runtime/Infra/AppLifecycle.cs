using System;
using Template.Core.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Template.Infra
{
    /// <summary>
    /// Android lifecycle glue: saves when the app goes to the background or quits, and turns the
    /// Android back button (Escape in the Input System) into one event the UI can react to.
    /// </summary>
    public sealed class AppLifecycle : MonoBehaviour
    {
        public static event Action BackPressed;
        public static event Action<bool> PauseChanged;

        public static AppLifecycle Create()
        {
            var go = new GameObject("[Lifecycle]");
            DontDestroyOnLoad(go);
            return go.AddComponent<AppLifecycle>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                BackPressed?.Invoke();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            PauseChanged?.Invoke(paused);
            if (paused)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit() => SaveNow();

        private static void SaveNow()
        {
            if (Services.TryGet<SaveService>(out var save))
            {
                save.SaveIfDirty();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            BackPressed = null;
            PauseChanged = null;
        }
    }
}
