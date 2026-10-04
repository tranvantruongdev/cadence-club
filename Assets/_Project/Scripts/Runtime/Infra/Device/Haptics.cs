using System;
using UnityEngine;

namespace Template.Infra.Device
{
    /// <summary>Short vibrations on Android (amplitude control on API 26+); silent elsewhere.</summary>
    public static class Haptics
    {
        public static bool Enabled { get; set; } = true;

        public static void Light() => Vibrate(15, 60);
        public static void Medium() => Vibrate(30, 140);
        public static void Heavy() => Vibrate(55, 255);

        public static void Vibrate(long milliseconds, int amplitude = -1)
        {
            if (!Enabled)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null)
                    {
                        return;
                    }

                    if (SdkInt >= 26)
                    {
                        int amp = amplitude <= 0 ? -1 : Mathf.Clamp(amplitude, 1, 255);
                        using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amp))
                        {
                            vibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        vibrator.Call("vibrate", milliseconds);
                    }
                }
            }
            catch (Exception)
            {
                // Referencing Handheld.Vibrate also makes Unity add the VIBRATE permission to the manifest.
                Handheld.Vibrate();
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int _sdkInt = -1;

        private static int SdkInt
        {
            get
            {
                if (_sdkInt < 0)
                {
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        _sdkInt = version.GetStatic<int>("SDK_INT");
                    }
                }

                return _sdkInt;
            }
        }
#endif
    }
}
