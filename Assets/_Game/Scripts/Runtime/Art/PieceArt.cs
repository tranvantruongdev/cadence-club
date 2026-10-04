using System.Collections.Generic;
using CadenceClub.Core;
using UnityEngine;

namespace CadenceClub.Art
{
    /// <summary>
    /// Piece sprites drawn in code. Every colour has its own shape (wheel, jersey, gem, bell, nut, drop), so the
    /// board reads without colour too (colour-blind players, greyscale). Specials add a white mark on top.
    /// One sprite = one world unit.
    /// </summary>
    public static class PieceArt
    {
        private const int Size = 128;

        public static readonly Color[] Colors =
        {
            Hex(0xE5484D), // red: wheel
            Hex(0x3FA34D), // green: jersey
            Hex(0x3E8BFF), // blue: gem
            Hex(0xF5B90F), // yellow: bell
            Hex(0x8E5BD9), // purple: nut
            Hex(0xF2802E), // orange: drop
        };

        public static readonly string[] ShapeNames = { "wheel", "jersey", "gem", "bell", "nut", "drop" };

        private static readonly Dictionary<int, Sprite> Pieces = new Dictionary<int, Sprite>();
        private static readonly Dictionary<Special, Sprite> Marks = new Dictionary<Special, Sprite>();
        private static Sprite _disco;
        private static Sprite _cell;

        public static Sprite Piece(int color)
        {
            color = Mathf.Clamp(color, 0, Colors.Length - 1);
            if (!Pieces.TryGetValue(color, out var sprite) || sprite == null)
            {
                Pieces[color] = sprite = Draw($"Piece{color}", (x, y) => Shape(color, x, y), Colors[color]);
            }

            return sprite;
        }

        /// <summary>White mark drawn over a coloured special (stripes, ring, chevron). Null for none or disco.</summary>
        public static Sprite Mark(Special special)
        {
            if (special == Special.None || special == Special.Disco)
            {
                return null;
            }

            if (!Marks.TryGetValue(special, out var sprite) || sprite == null)
            {
                Marks[special] = sprite = DrawMark(special);
            }

            return sprite;
        }

        public static Sprite Disco => _disco != null ? _disco : (_disco = DrawDisco());

        /// <summary>Rounded tile behind each playable cell.</summary>
        public static Sprite CellTile => _cell != null ? _cell : (_cell = DrawMask("Cell", (x, y) => RoundedBox(x, y, 0.94f, 0.94f, 0.22f), Color.white));

        private static float Shape(int color, float x, float y)
        {
            switch (color)
            {
                case 0: // wheel: rim and hub
                    float r = Mathf.Sqrt(x * x + y * y);
                    return Mathf.Min(Mathf.Abs(r - 0.6f) - 0.17f, r - 0.17f);
                case 1: // jersey: body with short sleeves
                    return Mathf.Min(RoundedBox(x, y + 0.05f, 0.42f, 0.62f, 0.12f), RoundedBox(x, y - 0.38f, 0.74f, 0.2f, 0.12f));
                case 2: // gem
                    return (Mathf.Abs(x) + Mathf.Abs(y)) * 0.7071f - 0.6f;
                case 3: // bell: rounded triangle
                    return Triangle(x, y + 0.08f, 0.62f) - 0.1f;
                case 4: // nut: hexagon with a hole
                    return Mathf.Max(Hexagon(x, y, 0.66f), 0.2f - Mathf.Sqrt(x * x + y * y));
                default: // drop
                    float circle = Mathf.Sqrt(x * x + (y + 0.22f) * (y + 0.22f)) - 0.5f;
                    return Mathf.Min(circle, Triangle(x, y - 0.12f, 0.5f));
            }
        }

        private static Sprite Draw(string name, System.Func<float, float, float> sdf, Color baseColor)
        {
            var texture = NewTexture(name);
            var pixels = new Color32[Size * Size];
            var dark = baseColor * 0.62f;
            dark.a = 1f;
            var light = Color.Lerp(baseColor, Color.white, 0.3f);
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float x = (px + 0.5f) / Size * 2f - 1f;
                    float y = (py + 0.5f) / Size * 2f - 1f;
                    float d = sdf(x, y);
                    float alpha = Mathf.Clamp01(0.5f - d * Size * 0.5f);
                    if (alpha <= 0f)
                    {
                        pixels[py * Size + px] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    var c = Color.Lerp(baseColor * 0.9f, light, (y + 1f) * 0.5f); // lit from above
                    c = Color.Lerp(dark, c, Mathf.Clamp01(-d / 0.09f)); // darker rim
                    float gloss = Mathf.Clamp01(1f - ((x + 0.28f) * (x + 0.28f) / 0.02f + (y - 0.36f) * (y - 0.36f) / 0.006f));
                    c = Color.Lerp(c, Color.white, gloss * 0.55f);
                    c.a = alpha;
                    pixels[py * Size + px] = c;
                }
            }

            return Finish(texture, pixels);
        }

        private static Sprite DrawMask(string name, System.Func<float, float, float> sdf, Color color)
        {
            var texture = NewTexture(name);
            var pixels = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float x = (px + 0.5f) / Size * 2f - 1f;
                    float y = (py + 0.5f) / Size * 2f - 1f;
                    var c = color;
                    c.a = Mathf.Clamp01(0.5f - sdf(x, y) * Size * 0.5f) * color.a;
                    pixels[py * Size + px] = c;
                }
            }

            return Finish(texture, pixels);
        }

        private static Sprite DrawMark(Special special)
        {
            switch (special)
            {
                case Special.RocketH:
                    return DrawMask("RocketH", (x, y) => Mathf.Min(RoundedBox(x, y - 0.2f, 0.55f, 0.06f, 0.05f), RoundedBox(x, y + 0.2f, 0.55f, 0.06f, 0.05f)),
                        Color.white);
                case Special.RocketV:
                    return DrawMask("RocketV", (x, y) => Mathf.Min(RoundedBox(x - 0.2f, y, 0.06f, 0.55f, 0.05f), RoundedBox(x + 0.2f, y, 0.06f, 0.55f, 0.05f)),
                        Color.white);
                case Special.Bomb:
                    return DrawMask("Bomb", (x, y) => Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.3f) - 0.07f, Color.white);
                default: // glider: a chevron pointing right
                    return DrawMask("Glider", (x, y) => Mathf.Max(Triangle(-y, x - 0.05f, 0.38f), -Triangle(-y, x + 0.18f, 0.28f)), Color.white);
            }
        }

        private static Sprite DrawDisco()
        {
            var texture = NewTexture("Disco");
            var pixels = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float x = (px + 0.5f) / Size * 2f - 1f;
                    float y = (py + 0.5f) / Size * 2f - 1f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    float alpha = Mathf.Clamp01(0.5f - (r - 0.74f) * Size * 0.5f);
                    float angle = (Mathf.Atan2(y, x) / (2f * Mathf.PI) + 1f) % 1f;
                    var c = Colors[Mathf.Min(Colors.Length - 1, (int)(angle * Colors.Length))];
                    c = Color.Lerp(c, Color.white, Mathf.Clamp01(1f - r / 0.32f)); // bright core
                    c.a = alpha;
                    pixels[py * Size + px] = c;
                }
            }

            return Finish(texture, pixels);
        }

        private static float RoundedBox(float x, float y, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(x) - halfW + radius;
            float qy = Mathf.Abs(y) - halfH + radius;
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>Equilateral triangle pointing up, "radius" r (Inigo Quilez's distance function).</summary>
        private static float Triangle(float x, float y, float r)
        {
            const float k = 1.7320508f;
            x = Mathf.Abs(x) - r;
            y = y + r / k;
            if (x + k * y > 0f)
            {
                float nx = (x - k * y) / 2f;
                float ny = (-k * x - y) / 2f;
                x = nx;
                y = ny;
            }

            x -= Mathf.Clamp(x, -2f * r, 0f);
            return -new Vector2(x, y).magnitude * Mathf.Sign(y);
        }

        private static float Hexagon(float x, float y, float r)
        {
            const float kx = -0.8660254f;
            const float ky = 0.5f;
            const float kz = 0.57735f;
            x = Mathf.Abs(x);
            y = Mathf.Abs(y);
            float dot = 2f * Mathf.Min(kx * x + ky * y, 0f);
            x -= dot * kx;
            y -= dot * ky;
            x -= Mathf.Clamp(x, -kz * r, kz * r);
            y -= r;
            return new Vector2(x, y).magnitude * Mathf.Sign(y);
        }

        private static Texture2D NewTexture(string name) =>
            new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        private static Sprite Finish(Texture2D texture, Color32[] pixels)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size, 0, SpriteMeshType.FullRect);
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
