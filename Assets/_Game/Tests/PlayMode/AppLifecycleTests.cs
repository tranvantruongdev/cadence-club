using System.Collections;
using NUnit.Framework;
using Template.Infra;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace CadenceClub.PlayModeTests
{
    /// <summary>
    /// The Android back button is Escape on the keyboard. Back fires once per press, read from the input event, so the
    /// very first event of a keyboard that press created still counts (on Android that first press used to be lost).
    /// </summary>
    public class AppLifecycleTests
    {
        [UnityTest]
        public IEnumerator Back_fires_once_per_press_including_a_new_keyboard_first_event()
        {
            var host = new GameObject("Lifecycle under test").AddComponent<AppLifecycle>();
            int lifecycles = Object.FindObjectsByType<AppLifecycle>(FindObjectsSortMode.None).Length; // the game's own may run too
            int backs = 0;
            void Count() => backs++;
            AppLifecycle.BackPressed += Count;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); // the keyboard's first event is a press
                yield return null;
                yield return null;
                Assert.AreEqual(lifecycles, backs, "the first press is a Back");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                Assert.AreEqual(lifecycles, backs, "holding the key is still one press");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                Assert.AreEqual(2 * lifecycles, backs, "release and press again is a second Back");
            }
            finally
            {
                AppLifecycle.BackPressed -= Count;
                InputSystem.RemoveDevice(keyboard);
                Object.Destroy(host.gameObject);
            }
        }
    }
}
