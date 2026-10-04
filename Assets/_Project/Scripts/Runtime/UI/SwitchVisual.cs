using Template.Feel;
using UnityEngine;
using UnityEngine.UI;

namespace Template.UI
{
    /// <summary>
    /// Draws a <see cref="Toggle"/> as an on/off switch: the knob slides and the track takes the accent colour.
    /// It follows <c>isOn</c> every frame, so <c>SetIsOnWithoutNotify</c> updates it too.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public sealed class SwitchVisual : MonoBehaviour
    {
        private Toggle _toggle;
        private float _position = -1f; // 0 = off, 1 = on; -1 = not placed yet

        public Image Track { get; set; }
        public RectTransform Knob { get; set; }
        public float Travel { get; set; }
        public Color OffColor { get; set; }
        public Color OnColor { get; set; }

        private void Awake() => _toggle = GetComponent<Toggle>();

        private void LateUpdate()
        {
            if (Track == null || Knob == null)
            {
                return;
            }

            float target = _toggle.isOn ? 1f : 0f;
            _position = _position < 0f || JuiceFx.ReduceMotion
                ? target
                : Mathf.MoveTowards(_position, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, UiTheme.Current.fast));

            float eased = _position * _position * (3f - 2f * _position);
            Knob.anchoredPosition = new Vector2(Mathf.Lerp(-Travel, Travel, eased) * 0.5f, 0f);
            Track.color = Color.Lerp(OffColor, OnColor, eased);
        }
    }
}
