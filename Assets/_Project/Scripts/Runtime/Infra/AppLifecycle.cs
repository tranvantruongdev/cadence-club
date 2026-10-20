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

        private bool _backPending;

        private void OnEnable() => InputSystem.onEvent += OnInputEvent;

        private void OnDisable() => InputSystem.onEvent -= OnInputEvent;

        // Polling Keyboard.current.escapeKey.wasPressedThisFrame missed most first presses after launch on Android
        // (the key event reached the app but no press was seen; QA 2026-10-19, T-1). Read the press from the event
        // itself instead: listeners run before the event is applied, so "not pressed yet" is the key's current state.
        private void OnInputEvent(UnityEngine.InputSystem.LowLevel.InputEventPtr eventPtr, InputDevice device)
        {
            if (device is Keyboard keyboard
                && (eventPtr.IsA<UnityEngine.InputSystem.LowLevel.StateEvent>() || eventPtr.IsA<UnityEngine.InputSystem.LowLevel.DeltaStateEvent>())
                && keyboard.escapeKey.ReadValueFromEvent(eventPtr, out float value)
                && value >= keyboard.escapeKey.pressPointOrDefault
                && !keyboard.escapeKey.isPressed)
            {
                _backPending = true;
            }
        }

        private void Update()
        {
            if (_backPending)
            {
                _backPending = false;
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
