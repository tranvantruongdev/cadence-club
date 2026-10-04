using System.Linq;
using CadenceClub.Art;
using CadenceClub.Core;
using CadenceClub.UI;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Template.Core.Settings;
using Template.Feel;
using Template.Game.Flow;
using Template.Infra;
using Template.Infra.Device;
using Template.Infra.Settings;
using Template.UI;
using TMPro;
using UnityEngine;

namespace CadenceClub
{
    /// <summary>
    /// Home: lives, coins and gems on top; the area being restored, with its six tasks (each costs ★ from wins);
    /// Play level N, which opens the level's start popup (goals, squad). Finishing an area tells its story beat.
    /// </summary>
    public sealed class HomeController : MonoBehaviour
    {
        private ScreenStack _stack;
        private SettingsPanelView _settingsView;
        private SettingsPresenter _settingsPresenter;
        private LevelStartScreen _levelStart;
        private StoryScreen _story;
        private RevealScreen _reveal;
        private RecruitScreen _recruit;
        private RidersScreen _riders;
        private RectTransform _safe;
        private RectTransform _area;
        private RectTransform _play;
        private TextMeshProUGUI _lives;
        private TextMeshProUGUI _coins;
        private TextMeshProUGUI _gems;
        private float _refresh;
        private bool _leaving;

        private void Start()
        {
            if (!BootGuard.EnsureBooted())
            {
                return;
            }

            var camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ClubUi.Night;

            var theme = UiTheme.Current;
            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas("Home UI");
            _stack = canvas.gameObject.AddComponent<ScreenStack>();
            _stack.RootBackPressed += Application.Quit;
            _safe = UiFactory.CreateSafeArea(canvas.transform);

            _lives = ClubUi.Pill(_safe, PieceArt.Heart, "", new Vector2(0f, 1f), new Vector2(160f, -90f), 260f);
            _coins = ClubUi.Pill(_safe, PieceArt.Coin, "", new Vector2(0f, 1f), new Vector2(440f, -90f), 260f);
            _gems = ClubUi.Pill(_safe, PieceArt.Gem, "", new Vector2(0f, 1f), new Vector2(720f, -90f), 260f);
            var gear = UiFactory.CreateIconButton(_safe, theme.iconSettings, Vector2.zero, 96, () => OpenSettings().Forget(), ButtonStyle.Glass, "Settings");
            UiFactory.Place(gear, new Vector2(1f, 1f), new Vector2(-80f, -90f));

            BuildArea(null);
            BuildPlay();

            _settingsView = SettingsPanelView.Create(canvas.transform);
            _settingsView.CloseRequested += () => CloseSettings().Forget();
            var picker = SquadPickerScreen.Create(canvas.transform, _stack);
            _levelStart = LevelStartScreen.Create(canvas.transform, _stack, picker);
            _levelStart.PlayPressed += StartLevel;
            _story = StoryScreen.Create(canvas.transform, _stack);
            _reveal = RevealScreen.Create(canvas.transform, _stack);
            _recruit = RecruitScreen.Create(canvas.transform, _stack, _reveal);
            _riders = RidersScreen.Create(canvas.transform, _stack);
            BuildNav();
            RefreshWallet();
            if (Club.PendingReveals.Count > 0)
            {
                RevealGifts().Forget();
            }
        }

        /// <summary>The free rider gets the full reveal the first time Home opens after the gift.</summary>
        private async UniTaskVoid RevealGifts()
        {
            var gifts = Club.PendingReveals.ToList();
            Club.PendingReveals.Clear();
            await UniTask.Delay(400);
            await _reveal.PlayAsync(gifts);
        }

        private void BuildNav()
        {
            var theme = UiTheme.Current;
            var md = Club.Master;
            var riders = UiFactory.CreateButton(_safe, "Riders", Vector2.zero, new Vector2(420f, 130f), () => _riders.OpenAsync().Forget(),
                ButtonStyle.Secondary, theme.iconTrophy);
            UiFactory.Place(riders, new Vector2(0.5f, 0f), new Vector2(-225f, 430f));
            bool open = Club.Data.RecruitUnlocked(md);
            var recruit = UiFactory.CreateButton(_safe, open ? "Recruit" : $"Recruit · Lv {md.Int("recruit_after_level") + 1}", Vector2.zero,
                new Vector2(420f, 130f), () => OpenRecruit(open), open ? ButtonStyle.Primary : ButtonStyle.Secondary, theme.iconStar);
            UiFactory.Place(recruit, new Vector2(0.5f, 0f), new Vector2(225f, 430f));
        }

        private void OpenRecruit(bool open)
        {
            if (open)
            {
                _recruit.OpenAsync().Forget();
            }
            else
            {
                JuiceFx.Punch(_play, 0.1f, 0.3f); // locked: point at Play
            }
        }

        private void Update()
        {
            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0f && _lives != null)
            {
                _refresh = 0.5f;
                RefreshWallet(); // the lives timer ticks
            }
        }

        private void RefreshWallet()
        {
            var club = Club.Data;
            var md = Club.Master;
            int lives = club.Lives(md, Club.Now);
            var next = club.NextLifeIn(md, Club.Now);
            _lives.text = lives >= md.Int("lives_max") ? $"{lives}" : $"{lives} · {next.Minutes}:{next.Seconds:00}";
            _coins.text = club.coins.ToString("N0");
            _gems.text = club.gems.ToString("N0");
        }

        /// <summary>The current area: name, ★ to spend, progress, and the six task tiles. <paramref name="justBuilt"/> pops in.</summary>
        private void BuildArea(string justBuilt)
        {
            if (_area != null)
            {
                Destroy(_area.gameObject);
            }

            var theme = UiTheme.Current;
            var md = Club.Master;
            var club = Club.Data;
            var area = club.CurrentArea(md);
            var tasks = md.Tasks.Where(t => t.area == area.id).ToList();
            int built = tasks.Count(t => club.IsBuilt(t.id));

            _area = UiFactory.CreateCard(_safe, new Vector2(0f, 180f), new Vector2(1000f, 920f));
            UiFactory.CreateText(_area, $"{area.id} · {area.name}", 72, new Vector2(-90f, 370f), new Vector2(760f, 110f), TextAlignmentOptions.MidlineLeft,
                UiFont.Display).color = theme.ink;
            UiFactory.CreateImage(_area, theme.iconStar, new Vector2(330f, 372f), new Vector2(64f, 64f), theme.accentEdge);
            UiFactory.CreateText(_area, club.stars.ToString(), 60, new Vector2(410f, 372f), new Vector2(120f, 90f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateText(_area, $"{built} of {tasks.Count} restored", 38, new Vector2(-90f, 295f), new Vector2(760f, 60f), TextAlignmentOptions.MidlineLeft)
                .color = theme.muted;
            UiFactory.CreateRounded(_area, new Vector2(0f, 240f), new Vector2(880f, 22f), theme.paperEdge, 11);
            if (built > 0)
            {
                float width = 880f * built / tasks.Count;
                UiFactory.CreateRounded(_area, new Vector2(-440f + width * 0.5f, 240f), new Vector2(width, 22f), theme.accent, 11);
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                var tile = BuildTask(tasks[i], new Vector2(i % 2 == 0 ? -232f : 232f, 90f - (i / 2) * 220f));
                if (tasks[i].id == justBuilt && !JuiceFx.ReduceMotion)
                {
                    tile.localScale = Vector3.one * 0.6f;
                    Tween.Scale(tile, 1f, 0.45f, Ease.OutBack);
                }
            }
        }

        private RectTransform BuildTask(TaskDef task, Vector2 position)
        {
            var theme = UiTheme.Current;
            var club = Club.Data;
            bool built = club.IsBuilt(task.id);
            var tile = UiFactory.CreateRect(task.id, _area);
            tile.anchorMin = tile.anchorMax = new Vector2(0.5f, 0.5f);
            tile.anchoredPosition = position;
            tile.sizeDelta = new Vector2(440f, 200f);
            UiFactory.CreateRounded(tile, Vector2.zero, tile.sizeDelta, built ? Color.Lerp(theme.accent, Color.white, 0.55f) : theme.paperEdge, 28);
            if (built)
            {
                UiFactory.CreateImage(tile, theme.iconCheck, new Vector2(-160f, 0f), new Vector2(70f, 70f), new Color(0.25f, 0.62f, 0.33f));
                UiFactory.CreateText(tile, task.name, 40, new Vector2(40f, 0f), new Vector2(320f, 160f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                    .color = theme.ink;
            }
            else
            {
                UiFactory.CreateText(tile, task.name, 38, new Vector2(0f, 45f), new Vector2(400f, 70f), TextAlignmentOptions.Center, UiFont.Display)
                    .color = theme.muted;
                bool affordable = club.stars >= task.stars;
                UiFactory.CreateButton(tile, $"Build · {task.stars}", new Vector2(0f, -40f), new Vector2(250f, 84f), () => Build(task),
                    affordable ? ButtonStyle.Primary : ButtonStyle.Secondary, theme.iconStar);
            }

            return tile;
        }

        private void Build(TaskDef task)
        {
            var result = Club.Data.TryBuild(Club.Master, task.id);
            if (!result.built)
            {
                JuiceFx.Punch(_play, 0.1f, 0.3f); // no ★ yet: point at Play, where stars come from
                return;
            }

            Club.Save();
            Haptics.Medium();
            if (result.completedArea != null)
            {
                _story.OpenAsync(result.completedArea, result.gems).Forget();
            }

            BuildArea(task.id);
            RefreshWallet();
        }

        private void BuildPlay()
        {
            var theme = UiTheme.Current;
            int next = Levels.Next(Club.Data.level);
            bool allDone = Club.Data.level > Levels.Count;
            _play = UiFactory.CreateRect("Play", _safe);
            _play.sizeDelta = new Vector2(700f, 170f);
            UiFactory.Place(_play, new Vector2(0.5f, 0f), new Vector2(0f, 230f));
            UiFactory.CreateButton(_play, allDone ? $"Play level {Levels.Count} again" : $"Play level {next}", Vector2.zero, new Vector2(700f, 170f),
                () => _levelStart.OpenAsync(Levels.Load(next)).Forget(), ButtonStyle.Primary, theme.iconPlay);
            if (!JuiceFx.ReduceMotion)
            {
                Tween.Scale(_play, 1.04f, 1f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, startDelay: 0.6f);
            }
        }

        private void StartLevel()
        {
            if (_leaving)
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
        }
    }
}
