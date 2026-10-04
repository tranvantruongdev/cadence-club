using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Template.Core.Flow;
using Template.Feel;
using Template.Infra;
using Template.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Template.Game.Flow
{
    public enum AppState
    {
        Boot,
        Title,
        Game,
    }

    /// <summary>App-level state machine. Moves between scenes behind a fade, one transition at a time.</summary>
    public sealed class GameFlow : MonoBehaviour
    {
        private const float FadeSeconds = 0.2f;

        private static readonly Dictionary<AppState, string> SceneNames = new Dictionary<AppState, string>
        {
            [AppState.Title] = "Title",
            [AppState.Game] = "Game",
        };

        private StateMachine<AppState> _states;
        private CanvasGroup _fade;
        private bool _transitioning;

        public AppState State => _states.Current;

        public static GameFlow Create()
        {
            var go = new GameObject("[GameFlow]");
            DontDestroyOnLoad(go);
            return go.AddComponent<GameFlow>();
        }

        private void Awake()
        {
            _states = new StateMachine<AppState>(AppState.Boot)
                .Allow(AppState.Boot, AppState.Title)
                .Allow(AppState.Title, AppState.Game)
                .Allow(AppState.Game, AppState.Title, AppState.Game);

            var canvas = UiFactory.CreateCanvas("[Fade]", 1000);
            canvas.transform.SetParent(transform, false);
            var black = UiFactory.CreatePanel(canvas.transform, Color.black);
            black.raycastTarget = true;
            _fade = canvas.gameObject.AddComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
        }

        public async UniTask GoToAsync(AppState next)
        {
            if (_transitioning)
            {
                return;
            }

            if (!_states.CanGo(next))
            {
                Debug.LogWarning($"[Flow] {_states.Current} -> {next} isn't allowed.");
                return;
            }

            _transitioning = true;
            _fade.blocksRaycasts = true;
            try
            {
                await FadeTo(1f);
                _states.Go(next);
                Time.timeScale = 1f;
                await SceneManager.LoadSceneAsync(SceneNames[next]);
                await FadeTo(0f);
            }
            finally
            {
                _fade.blocksRaycasts = false;
                _transitioning = false;
            }
        }

        private Tween FadeTo(float alpha)
        {
            float seconds = JuiceFx.ReduceMotion ? 0.05f : FadeSeconds;
            return Tween.Custom(_fade, _fade.alpha, alpha, seconds, (g, v) => g.alpha = v, useUnscaledTime: true);
        }
    }

    /// <summary>
    /// Pressing Play in any scene other than Boot loads Boot first, so services always exist.
    /// Call at the top of every scene controller's Start().
    /// </summary>
    public static class BootGuard
    {
        public const string BootScene = "Boot";

        public static bool EnsureBooted()
        {
            if (Services.TryGet<GameFlow>(out _))
            {
                return true;
            }

            SceneManager.LoadScene(BootScene);
            return false;
        }
    }
}
