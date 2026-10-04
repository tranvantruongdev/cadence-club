using Cysharp.Threading.Tasks;
using Template.Core.Save;
using Template.Core.Time;
using Template.Game.Debugging;
using Template.Game.Flow;
using Template.Infra;
using Template.Infra.Audio;
using Template.Infra.Device;
using Template.Infra.Save;
using Template.Infra.Settings;
using Template.UI;
using UnityEngine;

namespace Template.Game.Boot
{
    /// <summary>
    /// Lives in the Boot scene (first in Build Settings): creates services, loads the save,
    /// applies settings, then hands over to the Title scene.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Start() => Run().Forget();

        private async UniTaskVoid Run()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            var save = new SaveService(new FileSaveStore(), new SaveCodec(SaveSchema.CreateMigrator()), Debug.LogWarning);
            save.Load();

            var audio = AudioService.Create();
            var settings = new SettingsService(save, audio);

            Services.Register(save);
            Services.Register(audio);
            Services.Register(settings);
            Services.Register<IClock>(new SystemClock());

            settings.Apply();
            WireUiFeedback(audio);
            AppLifecycle.Create();
            DebugOverlay.CreateIfDevelopment();

            var flow = GameFlow.Create();
            Services.Register(flow);
            await flow.GoToAsync(AppState.Title);
        }

        /// <summary>Short generated sounds and a light haptic tick, so every screen feels tactile with no audio files.</summary>
        private static void WireUiFeedback(AudioService audio)
        {
            var tap = ToneFactory.Blip("ui-tap", 1250f, 0.035f, 0.35f);
            var open = ToneFactory.Blip("ui-open", 720f, 0.06f, 0.22f);
            var close = ToneFactory.Blip("ui-close", 540f, 0.05f, 0.18f);
            UiFeedback.Pressed += () =>
            {
                audio.PlaySfx(tap);
                Haptics.Light();
            };
            UiFeedback.Opened += () => audio.PlaySfx(open);
            UiFeedback.Closed += () => audio.PlaySfx(close);
        }
    }
}
