using System;
using System.Linq;
using CadenceClub.Art;
using CadenceClub.Core;
using Cysharp.Threading.Tasks;
using Template.UI;
using TMPro;
using UnityEngine;

namespace CadenceClub.UI
{
    /// <summary>Before a level: goals, moves, a "Hard level" tag on bumps, the squad (tap a slot to pick a rider), Play.</summary>
    public sealed class LevelStartScreen : UIScreen
    {
        private ScreenStack _stack;
        private SquadPickerScreen _picker;
        private LevelDef _def;
        private RectTransform _card;
        private RectTransform _squad;
        private TextMeshProUGUI _playLabel;

        public event Action PlayPressed;

        public override bool IsModal => true;

        public static LevelStartScreen Create(Transform parent, ScreenStack stack, SquadPickerScreen picker)
        {
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Level start";
            var view = dim.gameObject.AddComponent<LevelStartScreen>();
            view._stack = stack;
            view._picker = picker;
            dim.gameObject.SetActive(false);
            return view;
        }

        public UniTask OpenAsync(LevelDef def)
        {
            _def = def;
            Build();
            return _stack.PushAsync(this);
        }

        public override bool HandleBack()
        {
            _stack.PopAsync().Forget();
            return true;
        }

        private void Build()
        {
            if (_card != null)
            {
                Destroy(_card.gameObject);
            }

            var theme = UiTheme.Current;
            _card = UiFactory.CreateCard(transform, Vector2.zero, new Vector2(920f, 1240f));
            UiFactory.CreateText(_card, $"Level {_def.id}", 96, new Vector2(0f, 510f), new Vector2(700f, 130f), TextAlignmentOptions.Center, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateIconButton(_card, theme.iconClose, new Vector2(380f, 530f), 96f, () => _stack.PopAsync().Forget(), ButtonStyle.Secondary, "x");
            if (LevelBands.For(_def.id).max < 0.6)
            {
                UiFactory.CreateRounded(_card, new Vector2(0f, 415f), new Vector2(260f, 60f), theme.highlight, 30);
                UiFactory.CreateText(_card, "Hard level", 34, new Vector2(0f, 417f), new Vector2(260f, 60f), TextAlignmentOptions.Center, UiFont.Display);
            }

            float spacing = 210f;
            for (int i = 0; i < _def.goals.Length; i++)
            {
                float x = (i - (_def.goals.Length - 1) * 0.5f) * spacing;
                var goal = _def.goals[i];
                var tint = goal.kind == GoalKind.Ice ? new Color(0.45f, 0.7f, 0.95f) : Color.white;
                UiFactory.CreateImage(_card, PieceArt.GoalIcon(goal), new Vector2(x, 270f), new Vector2(130f, 130f), tint);
                UiFactory.CreateText(_card, goal.count.ToString(), 56, new Vector2(x, 170f), new Vector2(200f, 70f), TextAlignmentOptions.Center, UiFont.Display)
                    .color = theme.ink;
            }

            UiFactory.CreateText(_card, $"in {_def.moves} moves", 44, new Vector2(0f, 90f), new Vector2(700f, 60f)).color = theme.muted;
            UiFactory.CreateRounded(_card, new Vector2(0f, 30f), new Vector2(760f, 4f), new Color(0f, 0f, 0f, 0.1f), 2);
            UiFactory.CreateText(_card, "Squad", 40, new Vector2(0f, -30f), new Vector2(700f, 60f), TextAlignmentOptions.Center, UiFont.Display).color = theme.ink;
            BuildSquad();

            var play = UiFactory.CreateButton(_card, "Play", new Vector2(0f, -470f), new Vector2(720f, 160f), Play, ButtonStyle.Primary);
            _playLabel = play.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void BuildSquad()
        {
            if (_squad != null)
            {
                Destroy(_squad.gameObject);
            }

            var theme = UiTheme.Current;
            var md = Club.Master;
            var club = Club.Data;
            _squad = UiFactory.CreateRect("Squad", _card);
            _squad.anchoredPosition = new Vector2(0f, -200f);
            int slots = club.SquadSlots(md);
            if (slots == 0)
            {
                UiFactory.CreateText(_squad, $"Riders join the club after level {md.Int("free_rider_after_level")}.", 36, Vector2.zero, new Vector2(760f, 120f))
                    .color = theme.muted;
                return;
            }

            for (int i = 0; i < slots; i++)
            {
                int slot = i;
                float x = (i - (slots - 1) * 0.5f) * 280f;
                string id = i < club.squad.Count ? club.squad[i] : null;
                var rider = id != null ? md.Rider(id) : null;
                var button = UiFactory.CreateButton(_squad, "", new Vector2(x, 0f), new Vector2(230f, 230f), () => PickAsync(slot).Forget(), ButtonStyle.Glass);
                if (rider != null)
                {
                    ClubUi.Portrait(button.transform, rider, new Vector2(0f, 20f), 160f);
                    UiFactory.CreateText(button.transform, rider.name, 34, new Vector2(0f, -95f), new Vector2(240f, 50f)).color = theme.ink;
                }
                else
                {
                    UiFactory.CreateImage(button.transform, PieceArt.Ring, new Vector2(0f, 20f), new Vector2(160f, 160f), theme.paperEdge);
                    UiFactory.CreateText(button.transform, "+", 80, new Vector2(0f, 24f), new Vector2(160f, 160f), TextAlignmentOptions.Center, UiFont.Display)
                        .color = theme.muted;
                    UiFactory.CreateText(button.transform, "Add rider", 30, new Vector2(0f, -95f), new Vector2(240f, 50f)).color = theme.muted;
                }
            }
        }

        private async UniTaskVoid PickAsync(int slot)
        {
            string picked = await _picker.PickAsync();
            if (picked != null && Club.Data.SetSquad(Club.Master, slot, picked))
            {
                Club.Save();
                BuildSquad();
            }
        }

        private void Play()
        {
            var club = Club.Data;
            if (club.Lives(Club.Master, Club.Now) <= 0)
            {
                var next = club.NextLifeIn(Club.Master, Club.Now);
                _playLabel.text = $"Next life in {next.Minutes}:{next.Seconds:00}";
                return;
            }

            PlayPressed?.Invoke();
        }
    }

    /// <summary>A list of owned riders to put in a squad slot; resolves with the chosen id, or null when closed.</summary>
    public sealed class SquadPickerScreen : UIScreen
    {
        private ScreenStack _stack;
        private RectTransform _card;
        private UniTaskCompletionSource<string> _choice;

        public override bool IsModal => true;

        public static SquadPickerScreen Create(Transform parent, ScreenStack stack)
        {
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Squad picker";
            var view = dim.gameObject.AddComponent<SquadPickerScreen>();
            view._stack = stack;
            dim.gameObject.SetActive(false);
            return view;
        }

        public async UniTask<string> PickAsync()
        {
            Build();
            _choice = new UniTaskCompletionSource<string>();
            await _stack.PushAsync(this);
            return await _choice.Task;
        }

        public override bool HandleBack()
        {
            Choose(null);
            return true;
        }

        private void Build()
        {
            if (_card != null)
            {
                Destroy(_card.gameObject);
            }

            var theme = UiTheme.Current;
            var md = Club.Master;
            var owned = Club.Data.riders.Select(o => (owned: o, def: md.Rider(o.id))).Where(r => r.def != null)
                .OrderByDescending(r => r.def.rarity).ThenBy(r => r.def.name).ToList();
            int rows = Mathf.Max(1, (owned.Count + 1) / 2);
            float height = Mathf.Min(1700f, 260f + rows * 220f);
            float top = height * 0.5f;
            _card = UiFactory.CreateCard(transform, Vector2.zero, new Vector2(960f, height));
            UiFactory.CreateText(_card, "Pick a rider", 72, new Vector2(-60f, top - 90f), new Vector2(700f, 110f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateIconButton(_card, theme.iconClose, new Vector2(400f, top - 90f), 96f, () => Choose(null), ButtonStyle.Secondary, "x");
            for (int i = 0; i < owned.Count; i++)
            {
                var (rider, def) = owned[i];
                float x = i % 2 == 0 ? -225f : 225f;
                float y = top - 270f - (i / 2) * 220f;
                string id = def.id;
                var tile = UiFactory.CreateButton(_card, "", new Vector2(x, y), new Vector2(430f, 200f), () => Choose(id), ButtonStyle.Secondary);
                ClubUi.Portrait(tile.transform, def, new Vector2(-140f, 8f), 130f);
                UiFactory.CreateText(tile.transform, $"{def.name}  <size=70%>Lv {rider.level}</size>", 40, new Vector2(70f, 50f), new Vector2(270f, 56f),
                    TextAlignmentOptions.MidlineLeft, UiFont.Display).color = theme.ink;
                UiFactory.CreateText(tile.transform, RiderText.Power(def, rider.level), 26, new Vector2(70f, -20f), new Vector2(270f, 90f),
                    TextAlignmentOptions.MidlineLeft).color = theme.muted;
            }
        }

        private void Choose(string id)
        {
            if (_choice == null)
            {
                return;
            }

            var choice = _choice;
            _choice = null;
            _stack.PopAsync().Forget();
            choice.TrySetResult(id);
        }
    }

    /// <summary>The story beat when an area is finished: who says what, and the gems it pays.</summary>
    public sealed class StoryScreen : UIScreen
    {
        private ScreenStack _stack;
        private RectTransform _card;

        public override bool IsModal => true;

        public static StoryScreen Create(Transform parent, ScreenStack stack)
        {
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Story";
            var view = dim.gameObject.AddComponent<StoryScreen>();
            view._stack = stack;
            dim.gameObject.SetActive(false);
            return view;
        }

        public UniTask OpenAsync(AreaDef area, int gems)
        {
            if (_card != null)
            {
                Destroy(_card.gameObject);
            }

            var theme = UiTheme.Current;
            _card = UiFactory.CreateCard(transform, Vector2.zero, new Vector2(900f, 820f));
            UiFactory.CreateText(_card, $"{area.name} restored!", 76, new Vector2(0f, 290f), new Vector2(820f, 120f), TextAlignmentOptions.Center, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateText(_card, area.story, 48, new Vector2(0f, 90f), new Vector2(780f, 240f), TextAlignmentOptions.Center, UiFont.Story).color = theme.ink;
            UiFactory.CreateImage(_card, PieceArt.Gem, new Vector2(-100f, -110f), new Vector2(84f, 84f), Color.white);
            UiFactory.CreateText(_card, $"+{gems} gems", 52, new Vector2(40f, -108f), new Vector2(300f, 80f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateButton(_card, "Continue", new Vector2(0f, -290f), new Vector2(640f, 150f), () => _stack.PopAsync().Forget(),
                ButtonStyle.Primary, theme.iconCheck);
            return _stack.PushAsync(this);
        }

        public override bool HandleBack()
        {
            _stack.PopAsync().Forget();
            return true;
        }
    }
}
