using System;
using System.Collections.Generic;
using CadenceClub.Art;
using CadenceClub.Core;
using PrimeTween;
using Template.Feel;
using Template.Infra.Device;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CadenceClub.UI
{
    /// <summary>Goals and moves bar at the top, a home button, and the end card (won or out of moves).</summary>
    public sealed class LevelHud : MonoBehaviour
    {
        private static readonly Color Done = new Color(0.35f, 0.82f, 0.45f);

        private TextMeshProUGUI _moves;
        private TextMeshProUGUI[] _goalCounts;
        private Image[] _goalChecks;
        private RectTransform[] _goalIcons;
        private GameObject _end;
        private RectTransform _endCard;
        private TextMeshProUGUI _endTitle;
        private TextMeshProUGUI _endBody;
        private GameObject _next;
        private GameObject _continue;
        private GameObject _retry;
        private GameObject _home;
        private GameObject _pause;
        private int _shownMoves = -1;
        private RectTransform _safe;
        private readonly List<Portrait> _portraits = new List<Portrait>();
        private bool _squadBuilt;

        public event Action RetryPressed;
        public event Action NextPressed;
        public event Action ContinuePressed;
        public event Action ResumePressed;
        public event Action HomePressed;

        /// <summary>A squad portrait was tapped (slot index).</summary>
        public event Action<int> PowerPressed;

        private sealed class Portrait
        {
            public RectTransform root;
            public Image ring;
            public Image glow;
            public bool wasFull;
        }

        public static LevelHud Create(LevelDef def)
        {
            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<LevelHud>();
            hud.Build(def);
            return hud;
        }

        private void Build(LevelDef def)
        {
            var theme = UiTheme.Current;
            var safe = _safe = UiFactory.CreateSafeArea(transform);

            var bar = UiFactory.CreateRect("Goals bar", safe);
            bar.sizeDelta = new Vector2(1000f, 230f);
            UiFactory.Place(bar, new Vector2(0.5f, 1f), new Vector2(0f, -200f));
            UiFactory.AddShadow(bar, Vector2.zero, bar.sizeDelta, 40, 0.3f, 10f);
            UiFactory.CreateRounded(bar, Vector2.zero, bar.sizeDelta, theme.paper, 40);

            UiFactory.CreateText(bar, Loc.F("LEVEL {0}", def.id), 34, new Vector2(-200f, 78f), new Vector2(500f, 50f)).color = theme.muted;
            _goalCounts = new TextMeshProUGUI[def.goals.Length];
            _goalChecks = new Image[def.goals.Length];
            _goalIcons = new RectTransform[def.goals.Length];
            float spacing = 190f;
            float start = -200f - (def.goals.Length - 1) * spacing * 0.5f;
            for (int i = 0; i < def.goals.Length; i++)
            {
                float x = start + i * spacing;
                var tint = def.goals[i].kind == GoalKind.Ice ? new Color(0.45f, 0.7f, 0.95f) : Color.white; // pale ice vanishes on the cream card
                var icon = UiFactory.CreateImage(bar, PieceArt.GoalIcon(def.goals[i]), new Vector2(x, 0f), new Vector2(104f, 104f), tint);
                _goalIcons[i] = icon.rectTransform;
                _goalCounts[i] = UiFactory.CreateText(bar, "", 54, new Vector2(x, -80f), new Vector2(170f, 70f), TextAlignmentOptions.Center, UiFont.Display);
                _goalCounts[i].color = theme.ink;
                _goalChecks[i] = UiFactory.CreateImage(bar, theme.iconCheck, new Vector2(x, -80f), new Vector2(60f, 60f), Done);
                _goalChecks[i].enabled = false;
            }

            UiFactory.CreateRounded(bar, new Vector2(260f, 0f), new Vector2(4f, 170f), new Color(0f, 0f, 0f, 0.12f), 2);
            UiFactory.CreateText(bar, Loc.T("MOVES"), 34, new Vector2(370f, 78f), new Vector2(240f, 50f)).color = theme.muted;
            _moves = UiFactory.CreateText(bar, "", 110, new Vector2(370f, -12f), new Vector2(240f, 140f), TextAlignmentOptions.Center, UiFont.Display);
            _moves.color = theme.ink;

            var home = UiFactory.CreateIconButton(safe, theme.iconHome, Vector2.zero, 96f, () => HomePressed?.Invoke(), ButtonStyle.Glass, "<");
            UiFactory.Place(home, new Vector2(0f, 0f), new Vector2(92f, 92f));

            BuildEndCard(theme);
            BuildPauseCard(theme);
        }

        private void BuildPauseCard(UiTheme theme)
        {
            var overlay = UiFactory.CreateOverlay(transform);
            overlay.name = "Pause";
            _pause = overlay.gameObject;
            var card = UiFactory.CreateCard(overlay.rectTransform, Vector2.zero, new Vector2(860f, 560f));
            var title = UiFactory.CreateText(card, Loc.T("Paused"), 88, new Vector2(0f, 170f), new Vector2(780f, 120f),
                TextAlignmentOptions.Center, UiFont.Display);
            title.color = theme.ink;
            UiFactory.CreateButton(card, Loc.T("Continue"), new Vector2(0f, 10f), new Vector2(680f, 140f),
                () => ResumePressed?.Invoke(), ButtonStyle.Primary, theme.iconPlay);
            UiFactory.CreateButton(card, Loc.T("Home"), new Vector2(0f, -170f), new Vector2(680f, 120f),
                () => HomePressed?.Invoke(), ButtonStyle.Secondary, theme.iconHome);
            _pause.SetActive(false);
        }

        private void BuildEndCard(UiTheme theme)
        {
            var overlay = UiFactory.CreateOverlay(transform);
            overlay.name = "End";
            _end = overlay.gameObject;
            _endCard = UiFactory.CreateCard(overlay.rectTransform, Vector2.zero, new Vector2(860f, 920f));
            _endTitle = UiFactory.CreateText(_endCard, "", 88, new Vector2(0f, 330f), new Vector2(780f, 130f), TextAlignmentOptions.Center, UiFont.Display);
            _endTitle.color = theme.ink;
            _endTitle.enableAutoSizing = true; // "Level 30 complete!" is wider than "Out of moves"
            _endTitle.fontSizeMin = 56f;
            _endTitle.fontSizeMax = 88f;
            _endBody = UiFactory.CreateText(_endCard, "", 46, new Vector2(0f, 165f), new Vector2(760f, 200f));
            _endBody.color = theme.muted;
            _endBody.enableAutoSizing = true; // up to three lines: moves, rewards, a new rider
            _endBody.fontSizeMin = 30f;
            _endBody.fontSizeMax = 46f;
            // ShowEnd stacks whichever of these apply, top down.
            _next = UiFactory.CreateButton(_endCard, Loc.T("Next level"), Vector2.zero, new Vector2(680f, 140f), () => NextPressed?.Invoke(),
                ButtonStyle.Primary, theme.iconPlay).gameObject;
            _continue = UiFactory.CreateButton(_endCard, Loc.F("+{0} moves", 5), Vector2.zero, new Vector2(680f, 140f), () => ContinuePressed?.Invoke(),
                ButtonStyle.Primary, PieceArt.Coin).FullColourIcon().gameObject;
            _retry = UiFactory.CreateButton(_endCard, Loc.T("Play again"), Vector2.zero, new Vector2(680f, 120f), () => RetryPressed?.Invoke(),
                ButtonStyle.Secondary, theme.iconRetry).gameObject;
            _home = UiFactory.CreateButton(_endCard, Loc.T("Home"), Vector2.zero, new Vector2(680f, 120f), () => HomePressed?.Invoke(),
                ButtonStyle.Secondary, theme.iconHome).gameObject;
            _end.SetActive(false);
        }

        public void Refresh(LevelState state)
        {
            RefreshSquad(state.Squad);
            if (state.MovesLeft != _shownMoves)
            {
                bool changed = _shownMoves >= 0;
                _shownMoves = state.MovesLeft;
                _moves.text = state.MovesLeft.ToString();
                _moves.color = state.MovesLeft <= 3 ? UiTheme.Current.highlight : UiTheme.Current.ink;
                if (changed)
                {
                    JuiceFx.Punch(_moves.transform, 0.12f, 0.2f);
                }
            }

            for (int i = 0; i < _goalCounts.Length; i++)
            {
                int remaining = state.Remaining(i);
                string text = remaining.ToString();
                if (_goalCounts[i].text != text && _goalCounts[i].text.Length > 0)
                {
                    JuiceFx.Punch(_goalIcons[i], 0.18f, 0.25f);
                }

                _goalCounts[i].text = text;
                _goalCounts[i].color = remaining == 0 ? Done : UiTheme.Current.ink;
                bool check = remaining == 0 && _goalChecks[i].sprite != null; // neutral theme has no icons: keep the green 0
                _goalChecks[i].enabled = check;
                _goalCounts[i].enabled = !check;
            }
        }

        /// <param name="rewards">The payout or life line under the result, e.g. "+90 coins · +1 star".</param>
        /// <param name="continueLabel">On a loss, the "+5 moves · 300" offer.</param>
        /// <param name="showHome">False in the first session, so the win card only leads on.</param>
        /// <param name="nextLabel">The win card's way on ("Next level", or "Continue" when it leads Home).</param>
        public void ShowEnd(LevelState state, bool hasNext, string rewards, string continueLabel = null, bool showHome = true,
            string nextLabel = null)
        {
            bool won = state.Outcome == LevelOutcome.Won;
            _next.SetActive(won && hasNext);
            _continue.SetActive(!won && continueLabel != null);
            _retry.SetActive(!(won && hasNext));
            _home.SetActive(showHome || !(won && hasNext)); // never a card with no way out
            _next.GetComponentInChildren<TextMeshProUGUI>().text = nextLabel ?? Loc.T("Next level");
            if (continueLabel != null)
            {
                _continue.GetComponentInChildren<TextMeshProUGUI>().text = continueLabel;
            }

            _retry.GetComponentInChildren<TextMeshProUGUI>().text = won ? Loc.T("Play again") : Loc.T("Try again");
            float y = -30f;
            foreach (var button in new[] { _next, _continue, _retry, _home })
            {
                if (button.activeSelf)
                {
                    ((RectTransform)button.transform).anchoredPosition = new Vector2(0f, y);
                    y -= 150f;
                }
            }

            _endTitle.text = won ? Loc.F("Level {0} complete!", state.Def.id) : Loc.T("Out of moves");
            _endBody.text = (won
                ? state.MovesLeft == 1 ? Loc.F("{0} move to spare", 1) : Loc.F("{0} moves to spare", state.MovesLeft)
                : Loc.T("So close!")) + "\n" + rewards;
            _end.SetActive(true);
            JuiceFx.Punch(_endCard, 0.08f, 0.3f);
        }

        /// <summary>Replaces the end card's message (no coins for the offer, no life to play again).</summary>
        public void ShowEndMessage(string message)
        {
            _endBody.text = message;
            JuiceFx.Punch(_endCard, 0.06f, 0.2f);
        }

        public void ShowNoLives(System.TimeSpan nextLife)
        {
            _endBody.text = Loc.T("No lives left.") + "\n" + Loc.F("The next one comes in {0}:{1:00}.", nextLife.Minutes, nextLife.Seconds);
            JuiceFx.Punch(_endCard, 0.06f, 0.2f);
        }

        public void HideEnd() => _end.SetActive(false);

        public void ShowPause(bool visible) => _pause.SetActive(visible);

        /// <summary>
        /// Rider portraits along the bottom (the thumb zone): a charge ring in the rider's colour fills as their pieces
        /// clear; full, the portrait glows and breathes, and a tap fires the power.
        /// </summary>
        private void RefreshSquad(IReadOnlyList<RiderSlot> squad)
        {
            if (!_squadBuilt)
            {
                _squadBuilt = true;
                for (int i = 0; i < squad.Count; i++)
                {
                    _portraits.Add(BuildPortrait(squad[i].Def, i, squad.Count));
                }
            }

            for (int i = 0; i < _portraits.Count && i < squad.Count; i++)
            {
                var p = _portraits[i];
                bool full = squad[i].Full;
                p.ring.fillAmount = squad[i].Fill;
                if (full && !p.wasFull)
                {
                    Haptics.Light();
                    p.glow.color = WithAlpha(p.glow.color, 0.45f);
                    JuiceFx.Punch(p.root, 0.15f, 0.3f);
                    if (!JuiceFx.ReduceMotion)
                    {
                        Tween.Scale(p.root, 1.07f, 0.5f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, startDelay: 0.3f);
                    }
                }
                else if (!full && p.wasFull)
                {
                    Tween.StopAll(p.root);
                    p.root.localScale = Vector3.one;
                    p.glow.color = WithAlpha(p.glow.color, 0f);
                }

                p.wasFull = full;
            }
        }

        private Portrait BuildPortrait(RiderDef rider, int slot, int count)
        {
            var theme = UiTheme.Current;
            var colour = PieceArt.Colors[Mathf.Clamp(rider.color, 0, PieceArt.Colors.Length - 1)];
            var root = UiFactory.CreateRect($"Rider {rider.name}", _safe);
            root.sizeDelta = new Vector2(200f, 250f);
            UiFactory.Place(root, new Vector2(0.5f, 0f), new Vector2((slot - (count - 1) * 0.5f) * 250f, 210f));
            var glow = UiFactory.CreateImage(root, PieceArt.Disc, new Vector2(0f, 20f), new Vector2(220f, 220f), WithAlpha(colour, 0f));
            UiFactory.CreateImage(root, PieceArt.Disc, new Vector2(0f, 20f), new Vector2(180f, 180f), new Color(0.04f, 0.07f, 0.12f, 0.92f));
            var ring = UiFactory.CreateImage(root, PieceArt.Ring, new Vector2(0f, 20f), new Vector2(180f, 180f), colour);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;
            UiFactory.CreateImage(root, PieceArt.Disc, new Vector2(0f, 20f), new Vector2(128f, 128f), Color.Lerp(colour, Color.black, 0.2f));
            UiFactory.CreateText(root, rider.name.Substring(0, 1), 76, new Vector2(0f, 26f), new Vector2(128f, 110f), TextAlignmentOptions.Center, UiFont.Display)
                .color = Color.white;
            UiFactory.CreateText(root, rider.name, 32, new Vector2(0f, -96f), new Vector2(240f, 46f)).color = theme.textOnDark;
            UiFactory.CreateText(root, rider.rarity.ToString(), 26, new Vector2(70f, 96f), new Vector2(80f, 40f), TextAlignmentOptions.Center, UiFont.Display)
                .color = rider.rarity == Rarity.SSR ? theme.accent : theme.textOnDark;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = ring;
            button.onClick.AddListener(() => PowerPressed?.Invoke(slot));
            return new Portrait { root = root, ring = ring, glow = glow };
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private void OnDestroy()
        {
            foreach (var p in _portraits)
            {
                if (p.root != null)
                {
                    Tween.StopAll(p.root);
                }
            }
        }
    }
}
