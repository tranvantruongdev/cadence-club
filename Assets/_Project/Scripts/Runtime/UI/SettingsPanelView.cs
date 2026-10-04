using System;
using Template.Core.Save;
using Template.Core.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Template.UI
{
    /// <summary>
    /// The "V" in MVP: only displays values and forwards input. All logic sits in
    /// <see cref="SettingsPresenter"/> (Core), which is unit-tested without Unity.
    /// </summary>
    public sealed class SettingsPanelView : UIScreen, ISettingsView
    {
        private Slider _music;
        private Slider _sfx;
        private Toggle _haptics;
        private Toggle _reduceMotion;

        public event Action<float> MusicChanged;
        public event Action<float> SfxChanged;
        public event Action<bool> HapticsChanged;
        public event Action<bool> ReduceMotionChanged;
        public event Action CloseRequested;

        public override bool IsModal => true;

        public static SettingsPanelView Create(Transform parent)
        {
            var theme = UiTheme.Current;
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Settings";
            var view = dim.gameObject.AddComponent<SettingsPanelView>();
            var card = UiFactory.CreateCard(dim.rectTransform, Vector2.zero, new Vector2(900, 1240));

            // Title on the left, close button on the right of the same line.
            var title = UiFactory.CreateText(card, "Settings", 88, new Vector2(-60, 515), new Vector2(640, 140),
                TextAlignmentOptions.MidlineLeft, UiFont.Display);
            title.color = theme.ink;
            UiFactory.CreateIconButton(card, theme.iconClose, new Vector2(370, 520), 96, () => view.CloseRequested?.Invoke(),
                ButtonStyle.Secondary, "x");

            view._music = UiFactory.CreateSlider(card, "Music", new Vector2(0, 330), v => view.MusicChanged?.Invoke(v), theme.iconMusicOn);
            view._sfx = UiFactory.CreateSlider(card, "Sound effects", new Vector2(0, 150), v => view.SfxChanged?.Invoke(v), theme.iconSoundOn);
            view._haptics = UiFactory.CreateToggle(card, "Vibration", new Vector2(0, -20), v => view.HapticsChanged?.Invoke(v), theme.iconVibration);
            view._reduceMotion = UiFactory.CreateToggle(card, "Reduce motion", new Vector2(0, -150),
                v => view.ReduceMotionChanged?.Invoke(v), theme.iconMotion);
            UiFactory.CreateButton(card, "Done", new Vector2(0, -360), new Vector2(760, 140), () => view.CloseRequested?.Invoke(),
                ButtonStyle.Primary, theme.iconCheck);

            if (!string.IsNullOrEmpty(theme.credits))
            {
                var credits = UiFactory.CreateText(card, theme.credits, 30, new Vector2(0, -530), new Vector2(800, 110));
                credits.color = theme.muted;
            }

            dim.gameObject.SetActive(false);
            return view;
        }

        public void Show(SettingsData settings)
        {
            _music.SetValueWithoutNotify(settings.musicVolume);
            _sfx.SetValueWithoutNotify(settings.sfxVolume);
            _haptics.SetIsOnWithoutNotify(settings.haptics);
            _reduceMotion.SetIsOnWithoutNotify(settings.reduceMotion);
        }

        public override bool HandleBack()
        {
            CloseRequested?.Invoke();
            return true;
        }
    }
}
