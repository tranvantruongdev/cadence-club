using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Template.EditorTools.Setup;
using Template.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CadenceClub.EditorTools
{
    /// <summary>
    /// Builds Cadence Club's UI assets from the files in the repo: TextMeshPro font assets (static atlases with ASCII,
    /// Vietnamese and the symbols the UI uses), two material presets, sprite import settings for the icons, and
    /// the <see cref="UiTheme"/> at Resources/UiTheme. Safe to re-run: existing assets are kept unless rebuilt.
    /// Batch: Unity -batchmode -projectPath . -executeMethod CadenceClub.EditorTools.CadenceClubUiAssets.BuildBatch
    /// </summary>
    public static class CadenceClubUiAssets
    {
        private const string FontsFolder = "Assets/_Game/Fonts";
        private const string IconsFolder = "Assets/_Game/Art/Icons";
        private const string ResourcesFolder = "Assets/_Game/Resources";
        private const string ThemePath = ResourcesFolder + "/UiTheme.asset";

        [MenuItem("Cadence Club/Build UI Assets")]
        public static void Build() => Run(false);

        [MenuItem("Cadence Club/Rebuild UI Assets (fonts and theme)")]
        public static void Rebuild() => Run(true);

        public static void BuildBatch()
        {
            bool ok = false;
            try
            {
                ok = Run(false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Run(bool force)
        {
            TextSetup.EnsureEssentials();
            if (!TextSetup.HasEssentials || Shader.Find("TextMeshPro/Distance Field") == null)
            {
                Debug.LogError("[Cadence Club] TextMeshPro Essential Resources are not ready yet. Run this again.");
                return false;
            }

            ImportIcons();
            string characters = CharacterSet();
            var display = FontAsset("Baloo2-ExtraBold", 72, 7, 2048, 2048, characters, force);
            var body = FontAsset("Baloo2-Bold", 56, 6, 2048, 1024, characters, force);
            if (display == null || body == null)
            {
                return false;
            }

            var japanese = JapaneseFont(characters);
            if (japanese == null)
            {
                return false;
            }

            AddFallback(display, japanese);
            AddFallback(body, japanese);

            var shadow = Preset(display, "Baloo2-ExtraBold SDF Shadow", force, m =>
            {
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", new Color(0.04f, 0.08f, 0.16f, 0.55f));
                m.SetFloat("_UnderlayOffsetX", 0f);
                m.SetFloat("_UnderlayOffsetY", -0.55f);
                m.SetFloat("_UnderlayDilate", 0.1f);
                m.SetFloat("_UnderlaySoftness", 0.35f);
            });
            var outline = Preset(body, "Baloo2-Bold SDF Outline", force, m =>
            {
                m.SetColor("_OutlineColor", new Color(0.04f, 0.08f, 0.16f, 0.9f));
                m.SetFloat("_OutlineWidth", 0.22f);
                m.SetFloat("_FaceDilate", 0.1f);
            });

            BuildTheme(display, body, shadow, outline);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cadence Club] UI assets built: fonts, presets, icons, theme.");
            return true;
        }

        /// <summary>Printable ASCII, every precomposed Vietnamese letter, and the symbols the UI uses (× · — ’ …).</summary>
        private static string CharacterSet()
        {
            var sb = new StringBuilder();
            for (int c = 0x20; c <= 0x7E; c++)
            {
                sb.Append((char)c);
            }

            int[] latin1 =
            {
                0xC0, 0xC1, 0xC2, 0xC3, 0xC8, 0xC9, 0xCA, 0xCC, 0xCD, 0xD2, 0xD3, 0xD4, 0xD5, 0xD9, 0xDA, 0xDD,
                0xE0, 0xE1, 0xE2, 0xE3, 0xE8, 0xE9, 0xEA, 0xEC, 0xED, 0xF2, 0xF3, 0xF4, 0xF5, 0xF9, 0xFA, 0xFD,
            };
            int[] latinExtended = { 0x102, 0x103, 0x110, 0x111, 0x128, 0x129, 0x168, 0x169, 0x1A0, 0x1A1, 0x1AF, 0x1B0 };
            int[] symbols = { 0xA9, 0xB7, 0xD7, 0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2026 };
            foreach (int c in latin1)
            {
                sb.Append((char)c);
            }

            foreach (int c in latinExtended)
            {
                sb.Append((char)c);
            }

            for (int c = 0x1EA0; c <= 0x1EF9; c++)
            {
                sb.Append((char)c);
            }

            foreach (int c in symbols)
            {
                sb.Append((char)c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// M PLUS Rounded 1c as Baloo 2's fallback (Baloo has no Japanese): a static atlas of every character in
        /// strings.csv that the Latin set lacks, plus the language names on the Settings screen. Rebuilt when strings.csv
        /// needs a glyph the atlas doesn't have, so new Japanese text only needs this tool run again.
        /// </summary>
        private static TMP_FontAsset JapaneseFont(string latin)
        {
            const string file = "MPLUSRounded1c-Bold";
            const string languageNames = "日本語"; // Settings shows each language in its own script
            string text = File.ReadAllText("Assets/_Game/Resources/MasterData/strings.csv") + languageNames;
            var needed = new string(text.Where(c => !char.IsControl(c) && latin.IndexOf(c) < 0).Distinct().OrderBy(c => c).ToArray());
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontsFolder}/{file} SDF.asset");
            bool upToDate = existing != null && existing.HasCharacters(needed, out List<char> _);
            return FontAsset(file, 56, 6, 2048, 2048, needed, !upToDate);
        }

        private static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            font.fallbackFontAssetTable.RemoveAll(f => f == null); // a rebuilt fallback leaves a missing reference behind
            if (!font.fallbackFontAssetTable.Contains(fallback))
            {
                font.fallbackFontAssetTable.Add(fallback);
            }

            EditorUtility.SetDirty(font);
        }

        private static TMP_FontAsset FontAsset(string file, int samplingSize, int padding, int width, int height, string characters, bool force)
        {
            string path = $"{FontsFolder}/{file} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null && !force)
            {
                return existing;
            }

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontsFolder}/{file}.ttf");
            if (font == null)
            {
                Debug.LogError($"[Cadence Club] Missing {FontsFolder}/{file}.ttf");
                return null;
            }

            var asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, padding, GlyphRenderMode.SDFAA, width, height,
                AtlasPopulationMode.Dynamic, false);
            asset.name = file + " SDF";
            if (!asset.TryAddCharacters(characters, out string missing))
            {
                Debug.LogWarning($"[Cadence Club] {asset.name}: {missing.Length} characters not added (not in the font, or the atlas is full): {missing}");
            }

            asset.atlasPopulationMode = AtlasPopulationMode.Static; // shipped text never needs glyphs added at runtime

            AssetDatabase.CreateAsset(asset, path);
            var atlas = asset.atlasTexture;
            atlas.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            var material = asset.material;
            material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Cadence Club] {asset.name}: {asset.characterTable.Count} characters in a {atlas.width}x{atlas.height} atlas");
            return asset;
        }

        private static Material Preset(TMP_FontAsset font, string name, bool force, Action<Material> configure)
        {
            string path = $"{FontsFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && !force)
            {
                return existing;
            }

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var material = new Material(font.material) { name = name };
            configure(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ImportIcons()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                // Single, not Multiple: the 2D template's default auto-slices an icon made of separate shapes
                // (pause's two bars, a speaker and its waves) into several sprites, and only the first would load.
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
                    !importer.mipmapEnabled && importer.alphaIsTransparency)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 100f;
                importer.SaveAndReimport();
            }
        }

        private static Sprite Icon(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsFolder}/{name}.png");
            if (sprite == null)
            {
                Debug.LogWarning($"[Cadence Club] Icon not found: {name}");
            }

            return sprite;
        }

        /// <summary>"Club jersey": cream cards, deep-navy ink, the race leader's yellow for actions, red-jersey highlights.</summary>
        private static void BuildTheme(TMP_FontAsset display, TMP_FontAsset body, Material shadow, Material outline)
        {
            Directory.CreateDirectory(ResourcesFolder);
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            bool created = theme == null;
            if (created)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
            }

            theme.display = display;
            theme.body = body;
            theme.story = body; // no story text in this game
            theme.displayShadow = shadow;
            theme.bodyOutline = outline;

            theme.paper = Hex(0xF7F2E7);
            theme.paperEdge = Hex(0xDCD3BF);
            theme.ink = Hex(0x1B2A41);
            theme.muted = Hex(0x1B2A41, 0.6f);
            theme.accent = Hex(0xFFD23F);
            theme.accentEdge = Hex(0xD9A400);
            theme.highlight = Hex(0xE5484D);
            theme.overlay = Hex(0x0B1422, 0.66f);
            theme.textOnDark = Color.white;

            theme.iconPause = Icon("pause");
            theme.iconPlay = Icon("right");
            theme.iconHome = Icon("home");
            theme.iconRetry = Icon("return");
            theme.iconSettings = Icon("gear");
            theme.iconClose = Icon("cross");
            theme.iconSoundOn = Icon("audioOn");
            theme.iconSoundOff = Icon("audioOff");
            theme.iconMusicOn = Icon("musicOn");
            theme.iconMusicOff = Icon("musicOff");
            theme.iconVibration = Icon("phone");
            theme.iconMotion = Icon("contrast");
            theme.iconTrophy = Icon("trophy");
            theme.iconStar = Icon("star");
            theme.iconCheck = Icon("checkmark");
            theme.credits = "Fonts: Baloo 2, M PLUS Rounded 1c (SIL Open Font License). Icons: Kenney (CC0).";

            if (created)
            {
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            else
            {
                EditorUtility.SetDirty(theme);
            }
        }

        private static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
