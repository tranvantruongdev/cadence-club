using PrimeTween;
using Template.Feel;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Template.UI
{
    /// <summary>
    /// Tactile press for a <see cref="Button"/>: shrinks while the finger is down and springs back on release,
    /// and raises <see cref="UiFeedback.Pressed"/> (sound, haptic) on the way down, not after the click.
    /// With reduce motion on, it keeps the sound and skips the movement.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class PressableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Button _button;
        private Tween _tween;
        private bool _down;

        /// <summary>What shrinks; defaults to the button itself.</summary>
        public Transform Visual { get; set; }

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (Visual == null)
            {
                Visual = transform;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button == null || !_button.IsInteractable())
            {
                return;
            }

            _down = true;
            UiFeedback.RaisePressed();
            var theme = UiTheme.Current;
            ScaleTo(theme.pressScale, theme.instant, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData) => Release(Ease.OutBack);

        public void OnPointerExit(PointerEventData eventData) => Release(Ease.OutQuad);

        private void OnDisable()
        {
            _tween.Stop();
            _down = false;
            if (Visual != null)
            {
                Visual.localScale = Vector3.one;
            }
        }

        private void Release(Ease ease)
        {
            if (!_down)
            {
                return;
            }

            _down = false;
            ScaleTo(1f, UiTheme.Current.fast, ease);
        }

        private void ScaleTo(float scale, float seconds, Ease ease)
        {
            if (JuiceFx.ReduceMotion || Visual == null)
            {
                return;
            }

            _tween.Stop();
            _tween = Tween.Scale(Visual, scale, seconds, ease, useUnscaledTime: true);
        }
    }
}
