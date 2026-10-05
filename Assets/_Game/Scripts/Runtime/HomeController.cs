using System.Linq;
using CadenceClub.Art;
using CadenceClub.Audio;
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
        private DailyLoginScreen _daily;
        private ShopScreen _shop;
        private RectTransform _safe;
        private RectTransform _area;
        private RectTransform _play;
        private TextMeshProUGUI _lives;
        private TextMeshProUGUI _coins;
        private TextMeshProUGUI _gems;
        private float _refresh;
        private bool _leaving;
        private string _builtIn; // the language Home's labels were built in

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
            _builtIn = Loc.Language;
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
            _daily = DailyLoginScreen.Create(canvas.transform, _stack);
            _shop = ShopScreen.Create(canvas.transform, _stack);
            BuildNav();
            RefreshWallet();
            ClubAudio.Music(ClubAudio.HomeMusic);
            Welcome().Forget();
        }

        /// <summary>
        /// On arrival: a rider granted outside Recruit (the free rider) gets the full reveal, then the daily gift
        /// opens if today's isn't claimed yet.
        /// </summary>
        private async UniTaskVoid Welcome()
        {
            await UniTask.Delay(400);
            if (Club.PendingReveals.Count > 0)
            {
                var gifts = Club.PendingReveals.ToList();
                Club.PendingReveals.Clear();
                await _reveal.PlayAsync(gifts);
            }

            if (this != null && Club.Data.CanClaimDaily(Club.Today))
            {
                await _daily.OpenAsync();
            }
        }

        private void BuildNav()
        {
            var theme = UiTheme.Current;
            var md = Club.Master;
            var riders = UiFactory.CreateButton(_safe, Loc.T("Riders"), Vector2.zero, new Vector2(300f, 130f), () => _riders.OpenAsync().Forget(),
                ButtonStyle.Secondary, theme.iconTrophy);
            UiFactory.Place(riders, new Vector2(0.5f, 0f), new Vector2(-320f, 430f));
            bool open = Club.Data.RecruitUnlocked(md);
            var recruit = UiFactory.CreateButton(_safe, open ? Loc.T("Recruit") : Loc.F("Lv {0}", md.Int("recruit_after_level") + 1), Vector2.zero,
                new Vector2(300f, 130f), () => OpenRecruit(open), open ? ButtonStyle.Primary : ButtonStyle.Secondary, theme.iconStar);
            UiFactory.Place(recruit, new Vector2(0.5f, 0f), new Vector2(0f, 430f));
            var shop = UiFactory.CreateButton(_safe, Loc.T("Shop"), Vector2.zero, new Vector2(300f, 130f), () => _shop.OpenAsync().Forget(),
                ButtonStyle.Secondary, PieceArt.Gem).FullColourIcon();
            UiFactory.Place(shop, new Vector2(0.5f, 0f), new Vector2(320f, 430f));
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
            UiFactory.CreateText(_area, $"{area.id} · {Loc.T(area.name)}", 72, new Vector2(-90f, 370f), new Vector2(760f, 110f), TextAlignmentOptions.MidlineLeft,
                UiFont.Display).color = theme.ink;
            UiFactory.CreateImage(_area, theme.iconStar, new Vector2(330f, 372f), new Vector2(64f, 64f), theme.accentEdge);
            UiFactory.CreateText(_area, club.stars.ToString(), 60, new Vector2(410f, 372f), new Vector2(120f, 90f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateText(_area, Loc.F("{0} of {1} restored", built, tasks.Count), 38, new Vector2(-90f, 295f), new Vector2(760f, 60f), TextAlignmentOptions.MidlineLeft)
                .color = theme.muted;
            UiFactory.CreateRounded(_area, new Vector2(0f, 240f), new Vector2(880f, 22f), theme.paperEdge, 11);
            if (built > 0)
            {
                float width = 880f * built / tasks.Count;
                UiFactory.CreateRounded(_area, new Vector2(-440f + width * 0.5f, 240f), new Vector2(width, 22f), theme.accent, 11);
            }

            // The area's illustration: built tasks in place, the rest as faint ghosts of what they'll be.
            var scene = AreaArt.Scene(_area, area.id, new Vector2(0f, -130f));
            foreach (var task in tasks)
            {
                var item = AreaArt.Item(scene, task.id);
                if (item == null)
                {
                    continue;
                }

                if (!club.IsBuilt(task.id))
                {
                    item.gameObject.AddComponent<CanvasGroup>().alpha = 0.18f;
                }
                else if (task.id == justBuilt && !JuiceFx.ReduceMotion)
                {
                    item.localScale = Vector3.zero;
                    Tween.Scale(item, 1f, 0.5f, Ease.OutBack);
                }
            }

            // Then, on top, a ★ bubble for each task still to restore. First-session script: until anything is built,
            // a fingertip points at the first one the player can afford.
            var firstTask = club.built.Count == 0 ? tasks.FirstOrDefault(t => !club.IsBuilt(t.id) && club.stars >= t.stars) : null;
            foreach (var task in tasks.Where(t => !club.IsBuilt(t.id)))
            {
                var at = AreaArt.BubbleAt(task.id);
                Bubble(scene, task, at);
                if (task == firstTask)
                {
                    Pointer(scene, at + new Vector2(95f, -40f));
                }
            }
        }

        /// <summary>A task still to restore: a button with its ★ cost where the item will appear, and its name under it.</summary>
        private void Bubble(RectTransform scene, TaskDef task, Vector2 at)
        {
            var theme = UiTheme.Current;
            bool affordable = Club.Data.stars >= task.stars;
            UiFactory.CreateButton(scene, task.stars.ToString(), at, new Vector2(150f, 84f), () => Build(task),
                affordable ? ButtonStyle.Primary : ButtonStyle.Secondary, theme.iconStar);
            UiFactory.CreateRounded(scene, at + new Vector2(0f, -70f), new Vector2(240f, 44f), new Color(0.07f, 0.11f, 0.18f, 0.78f), 22);
            var name = UiFactory.CreateText(scene, Loc.T(task.name), 26, at + new Vector2(0f, -69f), new Vector2(226f, 40f), TextAlignmentOptions.Center,
                UiFont.Display);
            name.color = Color.white;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMin = 16f;
            name.fontSizeMax = 26f;
        }

        private static void Pointer(RectTransform parent, Vector2 at)
        {
            var pointer = UiFactory.CreateRect("Pointer", parent);
            pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0.5f);
            pointer.anchoredPosition = at;
            pointer.sizeDelta = new Vector2(100f, 100f);
            UiFactory.CreateImage(pointer, PieceArt.Disc, Vector2.zero, new Vector2(100f, 100f), new Color(0.07f, 0.11f, 0.18f, 0.85f));
            UiFactory.CreateImage(pointer, PieceArt.Disc, Vector2.zero, new Vector2(78f, 78f), Color.white);
            if (!JuiceFx.ReduceMotion)
            {
                Tween.LocalPositionY(pointer, pointer.localPosition.y + 30f, 0.5f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
            }
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
            ClubAudio.Play(ClubAudio.Chime);
            if (result.completedArea != null)
            {
                ClubAudio.Play(ClubAudio.Bell); // the club's bell for each area restored
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
            UiFactory.CreateButton(_play, allDone ? Loc.F("Play level {0} again", Levels.Count) : Loc.F("Play level {0}", next), Vector2.zero, new Vector2(700f, 170f),
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
            var settings = Services.Get<SettingsService>();
            settings.Commit();
            await _stack.PopAsync();
            if (settings.Current.language != _builtIn && !_leaving)
            {
                _leaving = true;
                Services.Get<GameFlow>().GoToAsync(AppState.Title).Forget(); // every label is built once: rebuild Home in the new language
            }
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
