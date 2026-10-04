using System;
using CadenceClub.Art;
using CadenceClub.Core;
using Template.Feel;
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
        private int _shownMoves = -1;

        public event Action RetryPressed;
        public event Action HomePressed;

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
            var safe = UiFactory.CreateSafeArea(transform);

            var bar = UiFactory.CreateRect("Goals bar", safe);
            bar.sizeDelta = new Vector2(1000f, 230f);
            UiFactory.Place(bar, new Vector2(0.5f, 1f), new Vector2(0f, -200f));
            UiFactory.AddShadow(bar, Vector2.zero, bar.sizeDelta, 40, 0.3f, 10f);
            UiFactory.CreateRounded(bar, Vector2.zero, bar.sizeDelta, theme.paper, 40);

            UiFactory.CreateText(bar, "GOALS", 34, new Vector2(-200f, 78f), new Vector2(500f, 50f)).color = theme.muted;
            _goalCounts = new TextMeshProUGUI[def.goals.Length];
            _goalChecks = new Image[def.goals.Length];
            _goalIcons = new RectTransform[def.goals.Length];
            float spacing = 190f;
            float start = -200f - (def.goals.Length - 1) * spacing * 0.5f;
            for (int i = 0; i < def.goals.Length; i++)
            {
                float x = start + i * spacing;
                var icon = UiFactory.CreateImage(bar, PieceArt.Piece(def.goals[i].color), new Vector2(x, 0f), new Vector2(104f, 104f), Color.white);
                _goalIcons[i] = icon.rectTransform;
                _goalCounts[i] = UiFactory.CreateText(bar, "", 54, new Vector2(x, -80f), new Vector2(170f, 70f), TextAlignmentOptions.Center, UiFont.Display);
                _goalCounts[i].color = theme.ink;
                _goalChecks[i] = UiFactory.CreateImage(bar, theme.iconCheck, new Vector2(x, -80f), new Vector2(60f, 60f), Done);
                _goalChecks[i].enabled = false;
            }

            UiFactory.CreateRounded(bar, new Vector2(260f, 0f), new Vector2(4f, 170f), new Color(0f, 0f, 0f, 0.12f), 2);
            UiFactory.CreateText(bar, "MOVES", 34, new Vector2(370f, 78f), new Vector2(240f, 50f)).color = theme.muted;
            _moves = UiFactory.CreateText(bar, "", 110, new Vector2(370f, -12f), new Vector2(240f, 140f), TextAlignmentOptions.Center, UiFont.Display);
            _moves.color = theme.ink;

            var home = UiFactory.CreateIconButton(safe, theme.iconHome, Vector2.zero, 96f, () => HomePressed?.Invoke(), ButtonStyle.Glass, "<");
            UiFactory.Place(home, new Vector2(0f, 0f), new Vector2(92f, 92f));

            BuildEndCard(theme);
        }

        private void BuildEndCard(UiTheme theme)
        {
            var overlay = UiFactory.CreateOverlay(transform);
            overlay.name = "End";
            _end = overlay.gameObject;
            _endCard = UiFactory.CreateCard(overlay.rectTransform, Vector2.zero, new Vector2(860f, 760f));
            _endTitle = UiFactory.CreateText(_endCard, "", 88, new Vector2(0f, 250f), new Vector2(780f, 130f), TextAlignmentOptions.Center, UiFont.Display);
            _endTitle.color = theme.ink;
            _endBody = UiFactory.CreateText(_endCard, "", 48, new Vector2(0f, 90f), new Vector2(760f, 150f));
            _endBody.color = theme.muted;
            UiFactory.CreateButton(_endCard, "Play again", new Vector2(0f, -120f), new Vector2(680f, 150f), () => RetryPressed?.Invoke(),
                ButtonStyle.Primary, theme.iconRetry);
            UiFactory.CreateButton(_endCard, "Home", new Vector2(0f, -280f), new Vector2(680f, 120f), () => HomePressed?.Invoke(),
                ButtonStyle.Secondary, theme.iconHome);
            _end.SetActive(false);
        }

        public void Refresh(LevelState state)
        {
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

        public void ShowEnd(LevelState state)
        {
            bool won = state.Outcome == LevelOutcome.Won;
            _endTitle.text = won ? "Level complete!" : "Out of moves";
            _endBody.text = won
                ? $"{state.MovesLeft} {(state.MovesLeft == 1 ? "move" : "moves")} to spare"
                : "So close! Try that board again.";
            _end.SetActive(true);
            JuiceFx.Punch(_endCard, 0.08f, 0.3f);
        }

        public void HideEnd() => _end.SetActive(false);
    }
}
