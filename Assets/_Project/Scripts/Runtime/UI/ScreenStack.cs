using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Template.Infra;
using UnityEngine;

namespace Template.UI
{
    /// <summary>
    /// Navigation stack for screens and popups with Android back-button handling.
    /// Back closes the top popup or screen; at the root it raises <see cref="RootBackPressed"/>.
    /// </summary>
    public sealed class ScreenStack : MonoBehaviour
    {
        private readonly List<UIScreen> _stack = new List<UIScreen>();
        private bool _busy;

        public UIScreen Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        public int Count => _stack.Count;

        public event Action RootBackPressed;

        private void OnEnable() => AppLifecycle.BackPressed += OnBack;
        private void OnDisable() => AppLifecycle.BackPressed -= OnBack;

        public async UniTask PushAsync(UIScreen screen)
        {
            if (_busy || screen == null || _stack.Contains(screen))
            {
                return;
            }

            _busy = true;
            try
            {
                var below = Top;
                if (below != null && !screen.IsModal)
                {
                    await below.HideAsync();
                }

                _stack.Add(screen);
                await screen.ShowAsync();
            }
            finally
            {
                _busy = false;
            }
        }

        public async UniTask PopAsync()
        {
            if (_busy || _stack.Count == 0)
            {
                return;
            }

            _busy = true;
            try
            {
                var top = Top;
                _stack.RemoveAt(_stack.Count - 1);
                await top.HideAsync();

                var next = Top;
                if (next != null && !top.IsModal)
                {
                    await next.ShowAsync();
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private void OnBack()
        {
            if (_busy)
            {
                return;
            }

            var top = Top;
            if (top != null && top.HandleBack())
            {
                return;
            }

            if (top != null)
            {
                PopAsync().Forget();
            }
            else
            {
                RootBackPressed?.Invoke();
            }
        }
    }
}
