using System;

namespace Template.UI
{
    /// <summary>
    /// UI moments that deserve a sound or a haptic tick. UI code raises them; the game decides what they
    /// sound like (see <c>GameBootstrap</c>), so screens never depend on the audio system.
    /// </summary>
    public static class UiFeedback
    {
        /// <summary>A button went down under the finger.</summary>
        public static event Action Pressed;

        /// <summary>A panel or popup started opening.</summary>
        public static event Action Opened;

        /// <summary>A panel or popup started closing.</summary>
        public static event Action Closed;

        public static void RaisePressed() => Pressed?.Invoke();

        public static void RaiseOpened() => Opened?.Invoke();

        public static void RaiseClosed() => Closed?.Invoke();
    }
}
