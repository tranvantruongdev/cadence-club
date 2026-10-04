using CadenceClub.Art;
using CadenceClub.Core;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CadenceClub.UI
{
    /// <summary>Small pieces the club's screens share: currency pills, rider portraits, power descriptions.</summary>
    public static class ClubUi
    {
        public static readonly Color Night = new Color(0.07f, 0.11f, 0.18f);

        /// <summary>A dark pill with an icon and a number (lives, coins, gems). Returns the label to update.</summary>
        public static TextMeshProUGUI Pill(Transform parent, Sprite icon, string text, Vector2 anchor, Vector2 offset, float width = 250f)
        {
            var root = UiFactory.CreateRect("Pill", parent);
            root.sizeDelta = new Vector2(width, 84f);
            UiFactory.Place(root, anchor, offset);
            UiFactory.CreateRounded(root, Vector2.zero, root.sizeDelta, new Color(0f, 0f, 0f, 0.35f), 42);
            UiFactory.CreateImage(root, icon, new Vector2(-width * 0.5f + 44f, 0f), new Vector2(60f, 60f), Color.white);
            var label = UiFactory.CreateText(root, text, 40, new Vector2(30f, 2f), new Vector2(width - 80f, 84f), TextAlignmentOptions.Center, UiFont.Display);
            label.color = UiTheme.Current.textOnDark;
            return label;
        }

        /// <summary>A round rider portrait: the rider's colour, their initial, and a rarity tag. Dimmed with a "?" when not owned.</summary>
        public static RectTransform Portrait(Transform parent, RiderDef rider, Vector2 position, float size, bool owned = true)
        {
            var colour = owned ? PieceArt.Colors[Mathf.Clamp(rider.color, 0, PieceArt.Colors.Length - 1)] : new Color(0.3f, 0.34f, 0.42f);
            var root = UiFactory.CreateRect($"Portrait {rider.name}", parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = position;
            root.sizeDelta = new Vector2(size, size);
            UiFactory.CreateImage(root, PieceArt.Disc, Vector2.zero, root.sizeDelta, Color.Lerp(colour, Color.white, 0.25f));
            UiFactory.CreateImage(root, PieceArt.Disc, Vector2.zero, root.sizeDelta * 0.84f, Color.Lerp(colour, Color.black, 0.15f));
            UiFactory.CreateText(root, owned ? rider.name.Substring(0, 1) : "?", Mathf.RoundToInt(size * 0.42f), new Vector2(0f, size * 0.03f),
                root.sizeDelta, TextAlignmentOptions.Center, UiFont.Display).color = Color.white;
            if (owned)
            {
                var tag = UiFactory.CreateRect("Rarity", root);
                tag.sizeDelta = new Vector2(size * 0.5f, size * 0.22f);
                tag.anchoredPosition = new Vector2(0f, -size * 0.46f);
                UiFactory.CreateRounded(tag, Vector2.zero, tag.sizeDelta, RarityColor(rider.rarity), Mathf.RoundToInt(size * 0.11f));
                UiFactory.CreateText(tag, rider.rarity.ToString(), Mathf.RoundToInt(size * 0.15f), new Vector2(0f, 1f), tag.sizeDelta,
                    TextAlignmentOptions.Center, UiFont.Display).color = UiTheme.Current.ink;
            }

            return root;
        }

        /// <summary>Shows a button's icon in its own colours (coin, gem) instead of tinted like the label.</summary>
        public static Button FullColourIcon(this Button button)
        {
            foreach (var image in button.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Icon")
                {
                    image.color = Color.white;
                }
            }

            return button;
        }

        public static Color RarityColor(Rarity rarity) =>
            rarity == Rarity.SSR ? new Color(1f, 0.82f, 0.25f) : rarity == Rarity.SR ? new Color(0.78f, 0.66f, 1f) : new Color(0.82f, 0.86f, 0.92f);
    }

    /// <summary>What a rider's power does, in words, at a level.</summary>
    public static class RiderText
    {
        public static string Power(RiderDef rider, int level)
        {
            int n = rider.Power(level);
            switch (rider.power)
            {
                case PowerKind.RowRockets: return n == 1 ? "Fires a rocket along the fullest row" : $"Fires {n} rockets along the fullest rows";
                case PowerKind.ColumnRockets: return n == 1 ? "Fires a rocket down the fullest column" : $"Fires {n} rockets down the fullest columns";
                case PowerKind.BreakObstacles: return $"Breaks {n} crates, ice or chains";
                case PowerKind.MakeSpecials: return $"Turns {n} pieces into rockets and bombs";
                default: return n == 1 ? "+1 move" : $"+{n} moves";
            }
        }

        public static string Charge(RiderDef rider) => $"Charges with {rider.charge} {PieceArt.ShapeNames[Mathf.Clamp(rider.color, 0, PieceArt.ShapeNames.Length - 1)]}s";
    }
}
