using Cysharp.Threading.Tasks;
using PrimeTween;
using Template.Feel;
using UnityEngine;

namespace Template.UI
{
    /// <summary>
    /// Base for every screen and popup. Shows and hides with a short fade and scale,
    /// and blocks input while animating. Override <see cref="HandleBack"/> for custom back behaviour.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        private const float TransitionSeconds = 0.18f;
        private CanvasGroup _group;

        protected CanvasGroup Group => _group != null ? _group : (_group = GetComponent<CanvasGroup>());

        /// <summary>Modal screens (popups) stay on top without hiding the screen below.</summary>
        public virtual bool IsModal => false;

        public virtual async UniTask ShowAsync()
        {
            gameObject.SetActive(true);
            Group.blocksRaycasts = true;
            Group.interactable = false;
            UiFeedback.RaiseOpened();

            if (JuiceFx.ReduceMotion)
            {
                Group.alpha = 1f;
                transform.localScale = Vector3.one;
            }
            else
            {
                Group.alpha = 0f;
                transform.localScale = Vector3.one * 0.96f;
                await Sequence.Create(useUnscaledTime: true)
                    .Group(Tween.Custom(Group, 0f, 1f, TransitionSeconds, (g, v) => g.alpha = v, useUnscaledTime: true))
                    .Group(Tween.Scale(transform, 1f, TransitionSeconds, Ease.OutBack, useUnscaledTime: true));
            }

            Group.interactable = true;
            OnShown();
        }

        public virtual async UniTask HideAsync()
        {
            Group.interactable = false;
            UiFeedback.RaiseClosed();
            if (!JuiceFx.ReduceMotion)
            {
                await Tween.Custom(Group, Group.alpha, 0f, TransitionSeconds, (g, v) => g.alpha = v, useUnscaledTime: true);
            }

            Group.blocksRaycasts = false;
            gameObject.SetActive(false);
            OnHidden();
        }

        /// <summary>Return true if the screen consumed the back press.</summary>
        public virtual bool HandleBack() => false;

        protected virtual void OnShown()
        {
        }

        protected virtual void OnHidden()
        {
        }
    }
}
