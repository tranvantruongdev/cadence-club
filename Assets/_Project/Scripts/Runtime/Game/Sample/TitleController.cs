using Cysharp.Threading.Tasks;
using Template.Core.Save;
using Template.Core.Settings;
using Template.Game.Flow;
using Template.Infra;
using Template.Infra.Settings;
using Template.UI;
using UnityEngine;

namespace Template.Game.Sample
{
    /// <summary>Sample title screen: best score, Play, and a Settings popup wired through MVP.</summary>
    public sealed class TitleController : MonoBehaviour
    {
        private ScreenStack _stack;
        private SettingsPanelView _settingsView;
        private SettingsPresenter _settingsPresenter;

        private void Start()
        {
            if (!BootGuard.EnsureBooted())
            {
                return;
            }

            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas("Title UI");
            _stack = canvas.gameObject.AddComponent<ScreenStack>();
            _stack.RootBackPressed += Application.Quit;
            var safe = UiFactory.CreateSafeArea(canvas.transform);

            var save = Services.Get<SaveService>();
            UiFactory.CreateText(safe, Application.productName, 92, new Vector2(0, 520), new Vector2(1000, 240));
            UiFactory.CreateText(safe, $"Best: {save.Data.bestScore}", 56, new Vector2(0, 300), new Vector2(900, 100));
            UiFactory.CreateButton(safe, "Play", new Vector2(0, -120), new Vector2(560, 170),
                () => Services.Get<GameFlow>().GoToAsync(AppState.Game).Forget());
            UiFactory.CreateButton(safe, "Settings", new Vector2(0, -340), new Vector2(560, 150), () => OpenSettings().Forget());

            _settingsView = SettingsPanelView.Create(safe);
            _settingsView.CloseRequested += () => CloseSettings().Forget();
        }

        private async UniTaskVoid OpenSettings()
        {
            var settings = Services.Get<SettingsService>();
            _settingsPresenter = new SettingsPresenter(settings.Current);
            _settingsPresenter.SettingsChanged += _ => settings.Apply();
            _settingsPresenter.Attach(_settingsView);
            await _stack.PushAsync(_settingsView);
        }

        private async UniTaskVoid CloseSettings()
        {
            _settingsPresenter?.Dispose();
            _settingsPresenter = null;
            Services.Get<SettingsService>().Commit();
            await _stack.PopAsync();
        }

        private void OnDestroy()
        {
            _settingsPresenter?.Dispose();
        }
    }
}
