using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Template.UI
{
    public enum ButtonStyle
    {
        /// <summary>The one main action on a screen: accent face, dark text.</summary>
        Primary,

        /// <summary>Other actions: paper face, dark text.</summary>
        Secondary,

        /// <summary>Over the game world: translucent dark face, light icon or text.</summary>
        Glass,
    }

    /// <summary>
    /// Builds themed uGUI in code (TextMeshPro text, rounded shapes, tactile buttons), so games need no prefabs.
    /// Looks come from <see cref="UiTheme.Current"/>. Positions and sizes are reference pixels (1080×1920).
    /// </summary>
    public static class UiFactory
    {
        public const float MinHitSize = 120f;
        private const int ShadowBlur = 24;

        private static UiTheme Theme => UiTheme.Current;

        public static Color Accent => Theme.accent;
        public static Color Ink => Theme.ink;
        public static Color Muted => Theme.muted;

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Anchors a rect to a point of its parent and offsets it from there, e.g. top-centre is (0.5, 1).
        /// Use it to pin HUD items to screen edges so tall phones and tablets keep the layout.
        /// </summary>
        public static T Place<T>(T component, Vector2 anchor, Vector2 offset) where T : Component
        {
            var rect = (RectTransform)component.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = offset;
            return component;
        }

        public static RectTransform CreateSafeArea(Transform parent)
        {
            var rect = CreateRect("SafeArea", parent);
            Stretch(rect);
            rect.gameObject.AddComponent<SafeArea>();
            return rect;
        }

        /// <summary>Full-screen tint.</summary>
        public static Image CreatePanel(Transform parent, Color color)
        {
            var rect = CreateRect("Panel", parent);
            Stretch(rect);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>Full-screen dim in the theme's overlay colour that also blocks taps to what's behind.</summary>
        public static Image CreateOverlay(Transform parent)
        {
            var image = CreatePanel(parent, Theme.overlay);
            image.raycastTarget = true;
            return image;
        }

        public static Image CreateImage(Transform parent, Sprite sprite, Vector2 anchoredPosition, Vector2 size, Color color, bool sliced = false)
        {
            var rect = CreateRect(sprite != null ? sprite.name : "Image", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = !sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateRounded(Transform parent, Vector2 anchoredPosition, Vector2 size, Color color, int radius) =>
            CreateImage(parent, UiSprites.RoundedRect(radius), anchoredPosition, size, color, true);

        /// <summary>Soft drop shadow for a rounded shape. Create it before the shape so it draws underneath.</summary>
        public static Image AddShadow(Transform parent, Vector2 anchoredPosition, Vector2 size, int radius, float alpha = 0.28f, float drop = 10f)
        {
            var image = CreateImage(parent, UiSprites.Shadow(radius, ShadowBlur), anchoredPosition + new Vector2(0f, -drop),
                size + Vector2.one * (ShadowBlur * 2), new Color(0f, 0f, 0f, alpha), true);
            image.name = "Shadow";
            return image;
        }

        /// <summary>Paper card with a soft shadow. Returns the card's root: put its content inside.</summary>
        public static RectTransform CreateCard(Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            var root = CreateRect("Card", parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = anchoredPosition;
            root.sizeDelta = size;
            AddShadow(root, Vector2.zero, size, Theme.cardRadius, 0.32f, 16f);
            var face = CreateRounded(root, Vector2.zero, size, Theme.paper, Theme.cardRadius);
            face.name = "Face";
            face.raycastTarget = true; // taps on the card don't fall through to the overlay behind it
            return root;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string text, int fontSize, Vector2 anchoredPosition, Vector2 size,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, UiFont font = UiFont.Body)
        {
            var rect = CreateRect("Text", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Theme.Font(font);
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Theme.textOnDark;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        /// <summary>Wraps digits so they don't jitter as a counter changes.</summary>
        public static string Tabular(string digits) => $"<mspace=0.58em>{digits}</mspace>";

        public static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size, Action onClick,
            ButtonStyle style = ButtonStyle.Primary, Sprite icon = null)
        {
            var root = CreateRect("Button " + label, parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = anchoredPosition;
            root.sizeDelta = new Vector2(size.x, Mathf.Max(size.y, MinHitSize));
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear; // the whole root is the tap target; the visuals sit inside it

            int radius = Mathf.Min(Theme.buttonRadius, Mathf.RoundToInt(size.y * 0.5f));
            var colors = StyleColors(style);
            if (style != ButtonStyle.Glass)
            {
                // A darker lip under the face gives the button depth.
                CreateRounded(root, new Vector2(0f, -8f), size, colors.edge, radius).name = "Lip";
            }

            var face = CreateRounded(root, Vector2.zero, size, colors.face, radius);
            face.name = "Face";
            AddContent(root, label, icon, size, colors.text);
            return Finish(root, face, onClick);
        }

        /// <summary>Round icon button; the tap target is at least <see cref="MinHitSize"/> even when the circle is smaller.</summary>
        public static Button CreateIconButton(Transform parent, Sprite icon, Vector2 anchoredPosition, float diameter, Action onClick,
            ButtonStyle style = ButtonStyle.Secondary, string fallbackLabel = "")
        {
            var root = CreateRect("IconButton " + (icon != null ? icon.name : fallbackLabel), parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = anchoredPosition;
            float hitSize = Mathf.Max(diameter, MinHitSize);
            root.sizeDelta = new Vector2(hitSize, hitSize);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var size = new Vector2(diameter, diameter);
            int radius = Mathf.RoundToInt(diameter * 0.5f);
            var colors = StyleColors(style);
            if (style != ButtonStyle.Glass)
            {
                CreateRounded(root, new Vector2(0f, -6f), size, colors.edge, radius).name = "Lip";
            }

            var face = CreateRounded(root, Vector2.zero, size, colors.face, radius);
            face.name = "Face";
            if (icon != null)
            {
                CreateImage(root, icon, Vector2.zero, size * 0.5f, colors.text).name = "Icon";
            }
            else
            {
                CreateText(root, fallbackLabel, Mathf.RoundToInt(diameter * 0.4f), Vector2.zero, size).color = colors.text;
            }

            return Finish(root, face, onClick);
        }

        public static Slider CreateSlider(Transform parent, string label, Vector2 anchoredPosition, Action<float> onChanged,
            Sprite icon = null, float width = 760f)
        {
            var row = CreateRect("Slider " + label, parent);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = anchoredPosition;
            row.sizeDelta = new Vector2(width, 150f);
            AddRowLabel(row, label, icon, width - 80f, 38f);

            var root = CreateRect("Slider", row);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, -32f);
            root.sizeDelta = new Vector2(width, 72f);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear; // a tap anywhere on the track grabs the slider, not only the knob

            const float trackHeight = 24f;
            const float knob = 64f;
            CreateRounded(root, Vector2.zero, new Vector2(width, trackHeight), Theme.paperEdge, 12).name = "Track";

            var fillArea = CreateRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, trackHeight);
            var fill = CreateRounded(fillArea, Vector2.zero, Vector2.zero, Theme.accent, 12);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var slideArea = CreateRect("Handle Slide Area", root);
            Stretch(slideArea);
            slideArea.offsetMin = new Vector2(knob * 0.5f, 0f);
            slideArea.offsetMax = new Vector2(-knob * 0.5f, 0f);
            var handle = CreateRect("Handle", slideArea);
            handle.sizeDelta = new Vector2(knob, 0f);
            AddShadow(handle, Vector2.zero, new Vector2(knob, knob), Mathf.RoundToInt(knob * 0.5f), 0.3f, 4f);
            var knobFace = CreateRounded(handle, Vector2.zero, new Vector2(knob, knob), Color.white, Mathf.RoundToInt(knob * 0.5f));

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.targetGraphic = knobFace;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return slider;
        }

        /// <summary>On/off switch row: label on the left, sliding switch on the right. The whole row is the tap target.</summary>
        public static Toggle CreateToggle(Transform parent, string label, Vector2 anchoredPosition, Action<bool> onChanged,
            Sprite icon = null, float width = 760f)
        {
            var row = CreateRect("Toggle " + label, parent);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = anchoredPosition;
            row.sizeDelta = new Vector2(width, MinHitSize);
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            AddRowLabel(row, label, icon, width - 220f, 0f);

            var trackSize = new Vector2(124f, 68f);
            var track = CreateRounded(row, new Vector2(width * 0.5f - trackSize.x * 0.5f, 0f), trackSize, Theme.paperEdge, 34);
            track.name = "Track";
            var knob = CreateRect("Knob", track.rectTransform);
            knob.sizeDelta = new Vector2(56f, 56f);
            AddShadow(knob, Vector2.zero, knob.sizeDelta, 28, 0.25f, 3f);
            CreateRounded(knob, Vector2.zero, knob.sizeDelta, Color.white, 28);

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = hit;
            var visual = row.gameObject.AddComponent<SwitchVisual>();
            visual.Track = track;
            visual.Knob = knob;
            visual.Travel = trackSize.x - trackSize.y;
            visual.OffColor = Theme.paperEdge;
            visual.OnColor = Theme.accent;
            toggle.onValueChanged.AddListener(v =>
            {
                UiFeedback.RaisePressed();
                onChanged?.Invoke(v);
            });
            return toggle;
        }

        private static (Color face, Color edge, Color text) StyleColors(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Secondary:
                    // Lighter than the paper so it still reads as a button when it sits on a paper card.
                    return (Color.Lerp(Theme.paper, Color.white, 0.7f), Theme.paperEdge, Theme.ink);
                case ButtonStyle.Glass:
                    return (new Color(0f, 0f, 0f, 0.32f), Color.clear, Theme.textOnDark);
                default:
                    return (Theme.accent, Theme.accentEdge, Theme.ink);
            }
        }

        /// <summary>Icon and label centred as one group inside a button.</summary>
        private static void AddContent(RectTransform root, string label, Sprite icon, Vector2 size, Color color)
        {
            int fontSize = Mathf.RoundToInt(size.y * 0.38f);
            bool hasLabel = !string.IsNullOrEmpty(label);
            float iconSize = size.y * 0.46f;
            if (icon != null && !hasLabel)
            {
                CreateImage(root, icon, Vector2.zero, new Vector2(iconSize, iconSize), color).name = "Icon";
                return;
            }

            if (icon == null)
            {
                var plain = CreateText(root, label, fontSize, Vector2.zero, size);
                plain.color = color;
                plain.textWrappingMode = TextWrappingModes.NoWrap;
                return;
            }

            // Icon and label centred as a group by a layout group, which sizes the label once the button is active.
            // (Measuring the text here returns 0 when a popup builds its buttons while still hidden.)
            var row = CreateRect("Content", root);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.sizeDelta = size;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var image = CreateImage(row, icon, Vector2.zero, new Vector2(iconSize, iconSize), color);
            image.name = "Icon";
            var iconLayout = image.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = iconLayout.preferredHeight = iconSize;
            var text = CreateText(row, label, fontSize, Vector2.zero, size);
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>Left-aligned row label in ink, with an optional icon before it.</summary>
        private static void AddRowLabel(RectTransform row, string label, Sprite icon, float labelWidth, float y)
        {
            float left = -row.sizeDelta.x * 0.5f;
            if (icon != null)
            {
                CreateImage(row, icon, new Vector2(left + 26f, y), new Vector2(52f, 52f), Theme.ink).name = "Icon";
                left += 72f;
            }

            var text = CreateText(row, label, 46, new Vector2(left + labelWidth * 0.5f, y), new Vector2(labelWidth, 70f),
                TextAlignmentOptions.MidlineLeft);
            text.color = Theme.ink;
        }

        private static Button Finish(RectTransform root, Image face, Action onClick)
        {
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.gameObject.AddComponent<PressableButton>();
            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
        }
    }
}
