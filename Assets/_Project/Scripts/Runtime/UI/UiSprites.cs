using System.Collections.Generic;
using UnityEngine;

namespace Template.UI
{
    /// <summary>
    /// White UI shapes drawn into textures at runtime: rounded rectangles and soft shadows, 9-sliced so one
    /// sprite fits any size. Tint them with <c>Image.color</c>. One texel = one reference pixel on the canvas.
    /// </summary>
    public static class UiSprites
    {
        private const float PixelsPerUnit = 100f; // matches the canvas' reference pixels per unit
        private static readonly Dictionary<int, Sprite> Rounded = new Dictionary<int, Sprite>();
        private static readonly Dictionary<long, Sprite> Shadows = new Dictionary<long, Sprite>();

        /// <summary>Rounded rectangle; use with <c>Image.type = Sliced</c>. Radius in reference pixels.</summary>
        public static Sprite RoundedRect(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 160);
            if (Rounded.TryGetValue(radius, out var cached) && cached != null)
            {
                return cached;
            }

            int size = radius * 2 + 4; // a 4-texel straight middle is what 9-slicing stretches
            var texture = NewTexture(size, $"Rounded{radius}");
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            }

            var sprite = Finish(texture, pixels, radius + 1);
            Rounded[radius] = sprite;
            return sprite;
        }

        /// <summary>A fully rounded end cap (pill or circle) for a shape of the given height.</summary>
        public static Sprite Pill(float height) => RoundedRect(Mathf.RoundToInt(height * 0.5f));

        /// <summary>
        /// Soft shadow for a rounded rectangle. Size the shadow's rect <paramref name="blur"/> pixels larger than
        /// the shape on every side (see <see cref="UiFactory.AddShadow"/>).
        /// </summary>
        public static Sprite Shadow(int radius, int blur)
        {
            radius = Mathf.Clamp(radius, 2, 160);
            blur = Mathf.Clamp(blur, 2, 64);
            long key = ((long)radius << 16) | (uint)blur;
            if (Shadows.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            int inner = radius * 2 + 4;
            int size = inner + blur * 2;
            var texture = NewTexture(size, $"Shadow{radius}x{blur}");
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float innerHalf = inner * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, innerHalf, innerHalf, radius);
                    float t = Mathf.Clamp01((d + blur * 0.35f) / blur); // 0 inside, 1 at the outer edge
                    float a = 1f - t * t * (3f - 2f * t);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            var sprite = Finish(texture, pixels, radius + blur + 1);
            Shadows[key] = sprite;
            return sprite;
        }

        /// <summary>Signed distance from a point to a rounded box centred on the origin (negative inside).</summary>
        private static float RoundedBoxDistance(float px, float py, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(px) - (halfWidth - radius);
            float qy = Mathf.Abs(py) - (halfHeight - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static Texture2D NewTexture(int size, string name) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

        private static Sprite Finish(Texture2D texture, Color32[] pixels, int border)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            int size = texture.width;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
