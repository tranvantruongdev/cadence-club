using CadenceClub.Art;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Template.Core.Save;
using Template.Core.Settings;
using Template.Feel;
using Template.Game.Flow;
using Template.Infra;
using Template.Infra.Settings;
using Template.UI;
using TMPro;
using UnityEngine;

namespace CadenceClub
{
    /// <summary>Title screen: the logo over a row of club pieces, the next level, Play, and the shared settings popup.</summary>
    public sealed class ClubTitleController : MonoBehaviour
    {
        private ScreenStack _stack;
        private SettingsPanelView _settingsView;
        private SettingsPresenter _settingsPresenter;
        private RectTransform _play;
        private RectTransform _pieces;
        private bool _leaving;

        private void Start()
        {
            if (!BootGuard.EnsureBooted())
            {
                return;
            }

            var camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.11f, 0.18f);

            var theme = UiTheme.Current;
            int saved = Services.Get<SaveService>().Data.level;
            bool allDone = saved > Levels.Count;
            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas("Title UI");
            _stack = canvas.gameObject.AddComponent<ScreenStack>();
            _stack.RootBackPressed += Application.Quit;
            var safe = UiFactory.CreateSafeArea(canvas.transform);

            var logo = UiFactory.CreateText(safe, "Cadence\nClub", 168, Vector2.zero, new Vector2(1000, 420), TextAlignmentOptions.Center, UiFont.Display);
            UiFactory.Place(logo, new Vector2(0.5f, 1f), new Vector2(0f, -420f));
            logo.color = theme.paper;
            logo.lineSpacing = -28f;
            if (theme.displayShadow != null)
            {
                logo.fontSharedMaterial = theme.displayShadow;
            }

            var tagline = UiFactory.CreateText(safe, "Match parts, win races, save the old club.", 42, Vector2.zero, new Vector2(980, 70));
            UiFactory.Place(tagline, new Vector2(0.5f, 1f), new Vector2(0f, -700f));
            tagline.color = new Color(1f, 1f, 1f, 0.75f);

            // The five pieces of the first levels: wheel, jersey, gem, bell, nut.
            _pieces = UiFactory.CreateRect("Pieces", safe);
            _pieces.sizeDelta = new Vector2(760f, 140f);
            UiFactory.Place(_pieces, new Vector2(0.5f, 1f), new Vector2(0f, -900f));
            for (int i = 0; i < 5; i++)
            {
                var piece = UiFactory.CreateImage(_pieces, PieceArt.Piece(i), new Vector2((i - 2) * 150f, 0f), new Vector2(124f, 124f), Color.white);
                if (!JuiceFx.ReduceMotion)
                {
                    Tween.LocalPositionY(piece.transform, 14f, 0.9f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, startDelay: i * 0.12f);
                }
            }

            var gear = UiFactory.CreateIconButton(safe, theme.iconSettings, Vector2.zero, 104, () => OpenSettings().Forget(), ButtonStyle.Glass, "Settings");
            UiFactory.Place(gear, new Vector2(1f, 1f), new Vector2(-92f, -92f));

            var chip = UiFactory.CreateRect("Progress", safe);
            chip.sizeDelta = new Vector2(560f, 84f);
            UiFactory.Place(chip, new Vector2(0.5f, 0f), new Vector2(0f, 560f));
            UiFactory.CreateRounded(chip, Vector2.zero, chip.sizeDelta, new Color(0f, 0f, 0f, 0.32f), 42);
            UiFactory.CreateImage(chip, allDone ? theme.iconTrophy : theme.iconStar, new Vector2(-215f, 0f), new Vector2(52f, 52f), theme.accent);
            UiFactory.CreateText(chip, allDone ? $"All {Levels.Count} levels won" : $"Level {saved} of {Levels.Count}", 44,
                new Vector2(30f, 3f), new Vector2(440f, 84f)).color = theme.textOnDark;

            // The breathing lives on a container so it doesn't fight the button's own press animation.
            _play = UiFactory.CreateRect("Play", safe);
            _play.sizeDelta = new Vector2(620f, 160f);
            UiFactory.Place(_play, new Vector2(0.5f, 0f), new Vector2(0f, 360f));
            UiFactory.CreateButton(_play, allDone ? "Play again" : $"Play level {saved}", Vector2.zero, new Vector2(620f, 160f), StartGame,
                ButtonStyle.Primary, theme.iconPlay);
            if (!JuiceFx.ReduceMotion)
            {
                Tween.Scale(_play, 1.04f, 1f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, startDelay: 0.6f);
            }

            _settingsView = SettingsPanelView.Create(canvas.transform);
            _settingsView.CloseRequested += () => CloseSettings().Forget();
        }

        private void StartGame()
        {
            if (_leaving || _stack == null || _stack.Count > 0)
            {
                return;
            }

            _leaving = true;
            Services.Get<GameFlow>().GoToAsync(AppState.Game).Forget();
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
            if (_play != null)
            {
                Tween.StopAll(_play);
            }

            if (_pieces != null)
            {
                foreach (Transform piece in _pieces)
                {
                    Tween.StopAll(piece);
                }
            }
        }
    }
}
