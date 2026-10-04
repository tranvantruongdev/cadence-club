using Cysharp.Threading.Tasks;
using Template.Core.Flow;
using Template.Core.Save;
using Template.Game.Flow;
using Template.Feel;
using Template.Infra;
using Template.Infra.Audio;
using Template.Infra.Device;
using Template.UI;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Template.Game.Sample
{
    /// <summary>
    /// Ten-second tap challenge that exercises every template system: state machine,
    /// game feel, procedural audio, haptics, saving the best score, and back-button flow.
    /// Delete it when you start a real game.
    /// </summary>
    public sealed class SampleGameController : MonoBehaviour
    {
        private enum RunState
        {
            Ready,
            Playing,
            Results,
        }

        private const float RoundSeconds = 10f;

        private StateMachine<RunState> _run;
        private RectTransform _root;
        private RectTransform _target;
        private Image _targetImage;
        private TextMeshProUGUI _scoreText;
        private TextMeshProUGUI _timerText;
        private TextMeshProUGUI _hintText;
        private GameObject _resultsPanel;
        private TextMeshProUGUI _resultsText;
        private AudioClip _tap;
        private AudioClip _milestone;
        private int _score;
        private float _timeLeft;

        private void Start()
        {
            if (!BootGuard.EnsureBooted())
            {
                return;
            }

            _run = new StateMachine<RunState>(RunState.Ready)
                .Allow(RunState.Ready, RunState.Playing)
                .Allow(RunState.Playing, RunState.Results)
                .Allow(RunState.Results, RunState.Ready);

            _tap = ToneFactory.Blip("tap", 660f);
            _milestone = ToneFactory.Blip("milestone", 990f, 0.16f);
            BuildUi();
            AppLifecycle.BackPressed += OnBack;
            ResetRound();
        }

        private void OnDestroy()
        {
            AppLifecycle.BackPressed -= OnBack;
        }

        private void Update()
        {
            if (_run == null || _run.Current != RunState.Playing)
            {
                return;
            }

            _timeLeft -= Time.deltaTime;
            _timerText.text = $"{Mathf.Max(0f, _timeLeft):0.0}s";
            if (_timeLeft <= 0f)
            {
                EndRound();
            }
        }

        private void BuildUi()
        {
            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas("Game UI");
            _root = UiFactory.CreateSafeArea(canvas.transform);

            _scoreText = UiFactory.CreateText(_root, "0", 120, new Vector2(0, 700), new Vector2(800, 180));
            _timerText = UiFactory.CreateText(_root, "", 56, new Vector2(0, 560), new Vector2(800, 100));
            _hintText = UiFactory.CreateText(_root, "Tap the circle as fast as you can", 48, new Vector2(0, -520), new Vector2(900, 120));

            var button = UiFactory.CreateButton(_root, "TAP", new Vector2(0, 0), new Vector2(420, 420), OnTap);
            _target = (RectTransform)button.transform;
            _targetImage = button.GetComponent<Image>();

            var panel = UiFactory.CreatePanel(_root, new Color(0f, 0f, 0f, 0.85f));
            panel.raycastTarget = true;
            _resultsPanel = panel.gameObject;
            _resultsText = UiFactory.CreateText(panel.rectTransform, "", 72, new Vector2(0, 300), new Vector2(950, 400));
            UiFactory.CreateButton(panel.rectTransform, "Retry", new Vector2(0, -100), new Vector2(520, 160), ResetRound);
            UiFactory.CreateButton(panel.rectTransform, "Home", new Vector2(0, -300), new Vector2(520, 130),
                () => Services.Get<GameFlow>().GoToAsync(AppState.Title).Forget());
        }

        private void ResetRound()
        {
            if (_run.Current == RunState.Results)
            {
                _run.Go(RunState.Ready);
            }

            _score = 0;
            _timeLeft = RoundSeconds;
            _scoreText.text = "0";
            _timerText.text = $"{RoundSeconds:0.0}s";
            _hintText.gameObject.SetActive(true);
            _resultsPanel.SetActive(false);
        }

        private void OnTap()
        {
            if (_run.Current == RunState.Ready)
            {
                _run.Go(RunState.Playing);
                _hintText.gameObject.SetActive(false);
            }

            if (_run.Current != RunState.Playing)
            {
                return;
            }

            _score++;
            _scoreText.text = _score.ToString();
            JuiceFx.Punch(_scoreText.transform, 0.18f);
            JuiceFx.FloatingText(_root, "+1", _target.anchoredPosition + new Vector2(UnityEngine.Random.Range(-120f, 120f), 220f), UiFactory.Accent);
            Haptics.Light();

            var audio = Services.Get<AudioService>();
            if (_score % 10 == 0)
            {
                audio.PlaySfx(_milestone);
                JuiceFx.Shake(_root, 18f, 0.25f);
                JuiceFx.Flash(_targetImage, Color.white);
                JuiceFx.HitStop(0.06f).Forget();
                Haptics.Medium();
            }
            else
            {
                audio.PlaySfx(_tap, 0.8f, 1f + (_score % 10) * 0.04f);
            }
        }

        private void EndRound()
        {
            _run.Go(RunState.Results);

            var save = Services.Get<SaveService>();
            bool newBest = _score > save.Data.bestScore;
            if (newBest)
            {
                save.Data.bestScore = _score;
            }

            save.Data.totalRuns++;
            save.MarkDirty();
            save.Save();

            _resultsText.text = newBest ? $"{_score} taps\nNew best!" : $"{_score} taps\nBest: {save.Data.bestScore}";
            _resultsPanel.SetActive(true);
            JuiceFx.Punch(_resultsText.transform, 0.2f, 0.35f);
            Haptics.Heavy();
        }

        private void OnBack()
        {
            Services.Get<GameFlow>().GoToAsync(AppState.Title).Forget();
        }
    }
}
