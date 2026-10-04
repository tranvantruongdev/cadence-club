using System;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Template.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Template.Feel
{
    /// <summary>
    /// One place for game feel: punch, shake, hit-stop, flash and floating text.
    /// Every effect respects the player's "reduce motion" setting.
    /// </summary>
    public static class JuiceFx
    {
        public static bool ReduceMotion { get; set; }

        public static void Punch(Transform target, float strength = 0.12f, float duration = 0.25f)
        {
            if (target == null)
            {
                return;
            }

            float s = ReduceMotion ? strength * 0.4f : strength;
            Tween.PunchScale(target, Vector3.one * s, duration, frequency: 8, useUnscaledTime: true);
        }

        public static void Shake(Transform target, float strength = 0.25f, float duration = 0.3f)
        {
            if (target == null || ReduceMotion)
            {
                return;
            }

            Tween.ShakeLocalPosition(target, new Vector3(strength, strength, 0f), duration, frequency: 18);
        }

        /// <summary>Freezes gameplay time for a moment to sell an impact.</summary>
        public static async UniTask HitStop(float seconds = 0.05f)
        {
            if (ReduceMotion || seconds <= 0f || Time.timeScale == 0f)
            {
                return;
            }

            float previous = Time.timeScale;
            Time.timeScale = 0f;
            await UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true);
            Time.timeScale = previous;
        }

        public static void Flash(Graphic graphic, Color flashColor, float duration = 0.15f)
        {
            if (graphic == null)
            {
                return;
            }

            var original = graphic.color;
            graphic.color = flashColor;
            Tween.Custom(graphic, flashColor, original, duration, (g, c) => g.color = c, useUnscaledTime: true);
        }

        /// <summary>Text that rises and fades, e.g. "+1" or "COMBO!". Destroys itself.</summary>
        public static void FloatingText(RectTransform parent, string text, Vector2 anchoredPosition, Color color, int fontSize = 64)
        {
            if (parent == null)
            {
                return;
            }

            var label = UiFactory.CreateText(parent, text, fontSize, anchoredPosition, new Vector2(600, 140));
            label.color = color;
            if (UiTheme.Current.bodyOutline != null)
            {
                label.fontSharedMaterial = UiTheme.Current.bodyOutline; // stays readable over bright sky
            }
            var rect = label.rectTransform;
            float rise = ReduceMotion ? 40f : 140f;

            Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Custom(rect, anchoredPosition, anchoredPosition + new Vector2(0f, rise), 0.6f,
                    (r, p) => r.anchoredPosition = p, Ease.OutCubic, useUnscaledTime: true))
                .Group(Tween.Custom(label, 1f, 0f, 0.6f, (l, a) =>
                {
                    var c = l.color;
                    c.a = a;
                    l.color = c;
                }, Ease.InQuad, useUnscaledTime: true))
                .OnComplete(label.gameObject, go => UnityEngine.Object.Destroy(go), warnIfTargetDestroyed: false); // the scene may unload first
        }
    }
}
