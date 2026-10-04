using TMPro;
using UnityEngine;

namespace Template.UI
{
    public enum UiFont
    {
        Body,
        Display,
        Story,
    }

    /// <summary>
    /// The look of every code-built screen: fonts, colours, corner radii, motion timings and icons.
    /// A game puts its own asset at <c>Resources/UiTheme</c>; without one, neutral defaults and TextMeshPro's
    /// default font are used. Sizes are reference pixels on the 1080×1920 canvas.
    /// </summary>
    [CreateAssetMenu(menuName = "Template/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Fonts")]
        public TMP_FontAsset body;
        public TMP_FontAsset display;
        public TMP_FontAsset story;

        [Tooltip("Display font with a soft drop shadow, for titles and counters over the game world.")]
        public Material displayShadow;

        [Tooltip("Body font with an outline, for floating text over the game world.")]
        public Material bodyOutline;

        [Header("Colours")]
        public Color paper = new Color(0.97f, 0.97f, 0.97f);
        public Color paperEdge = new Color(0.80f, 0.82f, 0.85f);
        public Color ink = new Color(0.08f, 0.09f, 0.11f);
        public Color muted = new Color(0.32f, 0.35f, 0.40f);
        public Color accent = new Color(1f, 0.78f, 0.18f);
        public Color accentEdge = new Color(0.85f, 0.60f, 0.08f);
        public Color highlight = new Color(1f, 0.48f, 0.27f);
        public Color overlay = new Color(0.05f, 0.05f, 0.08f, 0.7f);
        public Color textOnDark = Color.white;

        [Header("Shape")]
        public int chipRadius = 16;
        public int buttonRadius = 28;
        public int cardRadius = 44;

        [Header("Motion (seconds)")]
        public float instant = 0.08f;
        public float fast = 0.18f;
        public float normal = 0.35f;
        [Range(0.85f, 1f)] public float pressScale = 0.95f;

        [Header("Icons (optional: buttons fall back to their label)")]
        public Sprite iconPause;
        public Sprite iconPlay;
        public Sprite iconHome;
        public Sprite iconRetry;
        public Sprite iconSettings;
        public Sprite iconClose;
        public Sprite iconSoundOn;
        public Sprite iconSoundOff;
        public Sprite iconMusicOn;
        public Sprite iconMusicOff;
        public Sprite iconVibration;
        public Sprite iconMotion;
        public Sprite iconTrophy;
        public Sprite iconStar;
        public Sprite iconCheck;

        [Header("Settings screen")]
        [TextArea] public string credits = "";

        private static UiTheme _current;

        public static UiTheme Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<UiTheme>("UiTheme");
                    if (_current == null)
                    {
                        _current = CreateInstance<UiTheme>();
                        _current.name = "Default UI theme";
                    }
                }

                return _current;
            }
        }

        public TMP_FontAsset Font(UiFont font)
        {
            var chosen = font == UiFont.Display ? display : font == UiFont.Story ? story : body;
            if (chosen == null)
            {
                chosen = body;
            }

            return chosen != null ? chosen : TMP_Settings.defaultFontAsset;
        }
    }
}
