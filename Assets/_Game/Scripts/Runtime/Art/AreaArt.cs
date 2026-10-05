using System;
using System.Collections.Generic;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CadenceClub.Art
{
    /// <summary>
    /// The Home illustrations, built from UI shapes (rounded boxes, ellipses, rings): one flat scene per area, and one
    /// item per renovation task, drawn in its place in the scene so it can pop in when built. Scene coordinates are
    /// 920 × 620 with the origin in the middle.
    /// </summary>
    public static class AreaArt
    {
        public static readonly Vector2 SceneSize = new Vector2(920f, 620f);

        private static readonly Dictionary<string, (Vector2 at, Action<RectTransform> draw)> Items =
            new Dictionary<string, (Vector2, Action<RectTransform>)>
            {
                // 1 · Workshop
                ["fix_lights"] = (new Vector2(0f, 250f), Lamp),
                ["tool_wall"] = (new Vector2(-285f, 120f), ToolWall),
                ["repaint_sign"] = (new Vector2(285f, 175f), r => Sign(r, "CADENCE CLUB", H(0x2E4A6B), 270f)),
                ["workbench"] = (new Vector2(-270f, -50f), Workbench),
                ["bike_stand"] = (new Vector2(275f, -140f), BikeStand),
                ["sweep_floor"] = (new Vector2(-10f, -225f), Rug),

                // 2 · Café corner
                ["counter"] = (new Vector2(-210f, -150f), Counter),
                ["coffee_machine"] = (new Vector2(-300f, -20f), CoffeeMachine),
                ["stools"] = (new Vector2(170f, -195f), Stools),
                ["plants"] = (new Vector2(385f, -150f), Plant),
                ["menu_board"] = (new Vector2(215f, 125f), MenuBoard),
                ["string_lights"] = (new Vector2(0f, 262f), StringLights),

                // 3 · Garage
                ["racks"] = (new Vector2(-280f, -175f), Racks),
                ["tyre_wall"] = (new Vector2(-290f, 140f), TyreWall),
                ["lockers"] = (new Vector2(305f, 40f), Lockers),
                ["washing_bay"] = (new Vector2(30f, -235f), WashingBay),
                ["pump_station"] = (new Vector2(140f, -70f), Pump),
                ["club_banner"] = (new Vector2(0f, 250f), r => Sign(r, "CADENCE CLUB", H(0xE5484D), 360f)),

                // 4 · Training road
                ["cones"] = (new Vector2(-250f, -245f), Cones),
                ["timing_gate"] = (new Vector2(0f, -35f), TimingGate),
                ["water_station"] = (new Vector2(330f, -30f), WaterStation),
                ["rest_bench"] = (new Vector2(-330f, -30f), Bench),
                ["flags"] = (new Vector2(0f, 175f), Flags),
                ["lamp_posts"] = (new Vector2(0f, 50f), LampPosts),

                // 5 · Velodrome stands
                ["seats"] = (new Vector2(0f, 120f), Seats),
                ["scoreboard"] = (new Vector2(0f, 255f), Scoreboard),
                ["stand_flags"] = (new Vector2(0f, 20f), Bunting),
                ["food_stall"] = (new Vector2(-310f, -130f), FoodStall),
                ["podium"] = (new Vector2(10f, -175f), Podium),
                ["trophy_case"] = (new Vector2(320f, -125f), TrophyCase),
            };

        /// <summary>Where a task's build bubble sits when the item's own spot would crowd a neighbour or the edge.</summary>
        private static readonly Dictionary<string, Vector2> BubbleOffsets = new Dictionary<string, Vector2>
        {
            ["sweep_floor"] = new Vector2(0f, 20f),
            ["counter"] = new Vector2(0f, -30f),
            ["cones"] = new Vector2(0f, 40f),
            ["washing_bay"] = new Vector2(0f, 40f),
            ["flags"] = new Vector2(220f, 30f),
            ["lamp_posts"] = new Vector2(-330f, 80f),
            ["seats"] = new Vector2(-260f, 0f),
            ["stand_flags"] = new Vector2(250f, -10f),
        };

        /// <summary>The build bubble's spot for a task: on its item, kept clear of the scene's sides.</summary>
        public static Vector2 BubbleAt(string taskId)
        {
            var at = Items.TryGetValue(taskId, out var item) ? item.at : Vector2.zero;
            at += BubbleOffsets.TryGetValue(taskId, out var offset) ? offset : Vector2.zero;
            return new Vector2(Mathf.Clamp(at.x, -330f, 330f), at.y);
        }

        /// <summary>The area's backdrop, clipped to its panel. Items and bubbles go inside it.</summary>
        public static RectTransform Scene(Transform parent, int area, Vector2 position)
        {
            var scene = UiFactory.CreateRect($"Area {area}", parent);
            scene.anchorMin = scene.anchorMax = new Vector2(0.5f, 0.5f);
            scene.anchoredPosition = position;
            scene.sizeDelta = SceneSize;
            scene.gameObject.AddComponent<RectMask2D>();
            switch (area)
            {
                case 1: Room(scene, H(0xE3CFA8), H(0xB5865A), H(0x8A5F3D)); Window(scene, new Vector2(0f, 105f)); break;
                case 2: Room(scene, H(0xF0D5BF), H(0x7A5546), H(0x5E4033)); Window(scene, new Vector2(-30f, 95f)); break;
                case 3: Room(scene, H(0xA9B5C2), H(0x6B7480), H(0x4F5866)); GarageDoor(scene); break;
                case 4: Outdoors(scene); break;
                default: Velodrome(scene); break;
            }

            return scene;
        }

        /// <summary>The task's item in its place in the scene, or null for a task with no drawing.</summary>
        public static RectTransform Item(RectTransform scene, string taskId)
        {
            if (!Items.TryGetValue(taskId, out var item))
            {
                return null;
            }

            var root = UiFactory.CreateRect(taskId, scene);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = item.at;
            root.sizeDelta = Vector2.zero;
            item.draw(root);
            return root;
        }

        // ---- Backdrops

        private static void Room(RectTransform s, Color wall, Color floor, Color skirting)
        {
            Box(s, 0f, 90f, 920f, 440f, wall, 0);
            Box(s, 0f, -220f, 920f, 260f, floor, 0);
            Box(s, 0f, -92f, 920f, 16f, skirting, 0);
            for (int i = -3; i <= 3; i++)
            {
                Box(s, i * 130f, -220f, 4f, 240f, Color.Lerp(floor, Color.black, 0.12f), 0); // boards
            }
        }

        private static void Window(RectTransform s, Vector2 at)
        {
            Box(s, at.x, at.y, 230f, 170f, Color.white, 14);
            Box(s, at.x, at.y, 200f, 140f, H(0xA9DCF0), 8);
            Box(s, at.x, at.y, 8f, 140f, Color.white, 0);
            Box(s, at.x, at.y, 200f, 8f, Color.white, 0);
        }

        private static void GarageDoor(RectTransform s)
        {
            Box(s, 0f, 60f, 320f, 280f, H(0x8D99A6), 10);
            for (int i = 0; i < 6; i++)
            {
                Box(s, 0f, -60f + i * 46f, 300f, 6f, H(0x7A8693), 0);
            }
        }

        private static void Outdoors(RectTransform s)
        {
            Box(s, 0f, 120f, 920f, 400f, H(0xA9DCF0), 0);
            Dot(s, -260f, -40f, 520f, 220f, H(0x8CC084));
            Dot(s, 260f, -50f, 560f, 200f, H(0x7DB46C));
            Box(s, 0f, -200f, 920f, 260f, H(0x7DB46C), 0);
            Box(s, 0f, -180f, 920f, 150f, H(0x5A6270), 0);
            for (int i = -4; i <= 4; i++)
            {
                Box(s, i * 120f, -180f, 60f, 8f, Color.white, 4);
            }
        }

        private static void Velodrome(RectTransform s)
        {
            Box(s, 0f, 0f, 920f, 620f, H(0x2E4A6B), 0);
            Box(s, 0f, 120f, 920f, 250f, H(0x3A5F86), 0);
            Box(s, 0f, -205f, 920f, 230f, H(0xC9724A), 0);
            Box(s, 0f, -110f, 920f, 10f, H(0x3E8BFF), 0);
            Box(s, 0f, -160f, 920f, 4f, new Color(1f, 1f, 1f, 0.7f), 0);
            Box(s, 0f, -250f, 920f, 4f, new Color(1f, 1f, 1f, 0.7f), 0);
        }

        // ---- Items (drawn around their own origin)

        private static void Lamp(RectTransform r)
        {
            Box(r, 0f, 40f, 6f, 70f, H(0x3A3F4A), 0);
            Dot(r, 0f, -38f, 120f, 120f, new Color(1f, 0.88f, 0.48f, 0.45f));
            Box(r, 0f, -6f, 130f, 50f, H(0x2E4A6B), 22);
            Dot(r, 0f, -34f, 34f, 34f, H(0xFFF6C8));
        }

        private static void ToolWall(RectTransform r)
        {
            Box(r, 0f, 0f, 250f, 180f, H(0xC9A57A), 12);
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    Dot(r, -100f + x * 50f, -66f + y * 44f, 9f, 9f, H(0x9C7A52));
                }
            }

            Box(r, -55f, 0f, 16f, 120f, H(0x9AA3AE), 6, -25f);
            Ring(r, -30f, 52f, 46f, H(0x9AA3AE));
            Box(r, 50f, -10f, 14f, 110f, H(0x8A5F3D), 6);
            Box(r, 50f, 46f, 74f, 26f, H(0x5B6573), 6);
        }

        private static void Sign(RectTransform r, string text, Color board, float width)
        {
            Box(r, -width * 0.32f, 52f, 4f, 40f, H(0x5B6573), 0);
            Box(r, width * 0.32f, 52f, 4f, 40f, H(0x5B6573), 0);
            Box(r, 0f, 0f, width, 80f, board, 14);
            Label(r, text, 0f, 0f, width, 34, UiTheme.Current.accent);
        }

        private static void Workbench(RectTransform r)
        {
            Box(r, -120f, -80f, 22f, 130f, H(0x6E4B3A), 4);
            Box(r, 120f, -80f, 22f, 130f, H(0x6E4B3A), 4);
            Box(r, -10f, -50f, 140f, 44f, H(0xA87A52), 6);
            Box(r, 0f, 0f, 300f, 28f, H(0x8A5F3D), 6);
            Box(r, 110f, 30f, 54f, 36f, H(0x5B6573), 6);
        }

        private static void BikeStand(RectTransform r)
        {
            Box(r, 0f, -80f, 240f, 14f, H(0x5B6573), 6);
            Bike(r, 0f, -6f, H(0xE5484D));
        }

        /// <summary>A side-on bicycle: two wheels, a diamond frame, saddle and bars.</summary>
        private static void Bike(RectTransform r, float x, float y, Color frame)
        {
            Ring(r, x - 75f, y, 120f, H(0x1B2A41));
            Ring(r, x + 75f, y, 120f, H(0x1B2A41));
            Box(r, x - 6f, y + 42f, 120f, 10f, frame, 5);
            Box(r, x - 38f, y + 18f, 10f, 66f, frame, 5, 35f);
            Box(r, x + 30f, y + 18f, 10f, 70f, frame, 5, -25f);
            Box(r, x - 40f, y + 6f, 80f, 10f, frame, 5, 20f);
            Box(r, x - 62f, y + 60f, 44f, 12f, H(0x1B2A41), 6);
            Box(r, x + 66f, y + 64f, 36f, 10f, H(0x1B2A41), 5);
        }

        private static void Rug(RectTransform r)
        {
            Dot(r, 0f, 0f, 400f, 104f, H(0xE5484D));
            Dot(r, 0f, 0f, 320f, 74f, H(0xF5B90F));
            Dot(r, 0f, 0f, 200f, 40f, H(0xE5484D));
            Box(r, 190f, 70f, 10f, 170f, H(0x8A5F3D), 4, 22f);
            Box(r, 222f, -6f, 64f, 40f, H(0xF5B90F), 8, 22f);
        }

        private static void Counter(RectTransform r)
        {
            Box(r, 0f, 0f, 380f, 140f, H(0x8E6550), 12);
            for (int i = -1; i <= 1; i++)
            {
                Box(r, i * 120f, -6f, 100f, 96f, H(0xA07455), 8);
            }

            Box(r, 0f, 78f, 410f, 26f, H(0xC9A57A), 8);
        }

        private static void CoffeeMachine(RectTransform r)
        {
            Box(r, 0f, 0f, 110f, 124f, H(0x3A3F4A), 14);
            Box(r, 0f, 68f, 124f, 20f, H(0x5B6573), 8);
            Box(r, 0f, -18f, 22f, 22f, H(0x9AA3AE), 4);
            Box(r, 0f, -48f, 40f, 34f, Color.white, 6);
            Dot(r, 36f, 36f, 14f, 14f, H(0xE5484D));
        }

        private static void Stools(RectTransform r)
        {
            for (int i = -1; i <= 1; i++)
            {
                Box(r, i * 95f, -6f, 12f, 84f, H(0x5B6573), 4);
                Box(r, i * 95f, -46f, 52f, 10f, H(0x5B6573), 4);
                Dot(r, i * 95f, 40f, 84f, 28f, H(0xE5484D));
            }
        }

        private static void Plant(RectTransform r)
        {
            Dot(r, -24f, 36f, 74f, 74f, H(0x3FA34D));
            Dot(r, 24f, 48f, 70f, 70f, H(0x3FA34D));
            Dot(r, 0f, 80f, 60f, 60f, H(0x56B865));
            Dot(r, 8f, 20f, 54f, 54f, H(0x2E7D3A));
            Box(r, 0f, -30f, 84f, 84f, H(0xC9724A), 12);
        }

        private static void MenuBoard(RectTransform r)
        {
            Box(r, 0f, 0f, 250f, 180f, H(0x8A5F3D), 14);
            Box(r, 0f, 0f, 224f, 154f, H(0x2F3A35), 8);
            Label(r, "MENU", 0f, 48f, 220f, 30, Color.white);
            float[] widths = { 150f, 120f, 160f };
            for (int i = 0; i < widths.Length; i++)
            {
                Box(r, -20f + widths[i] * 0.1f, 8f - i * 30f, widths[i], 6f, new Color(1f, 1f, 1f, 0.75f), 3);
            }
        }

        private static void StringLights(RectTransform r)
        {
            Box(r, 0f, 0f, 900f, 4f, H(0x5B6573), 0);
            Color[] colours = { H(0xFFD23F), H(0xE5484D), H(0x3E8BFF), H(0x3FA34D) };
            for (int i = 0; i < 9; i++)
            {
                Dot(r, -400f + i * 100f, -16f, 24f, 28f, colours[i % colours.Length]);
            }
        }

        private static void Racks(RectTransform r)
        {
            Box(r, 0f, -64f, 300f, 14f, H(0x3A3F4A), 6);
            Ring(r, -105f, -8f, 96f, H(0x1B2A41));
            Ring(r, -25f, -8f, 96f, H(0x1B2A41));
            Box(r, -65f, 20f, 70f, 10f, H(0x3E8BFF), 5);
            Ring(r, 35f, -8f, 96f, H(0x1B2A41));
            Ring(r, 115f, -8f, 96f, H(0x1B2A41));
            Box(r, 75f, 20f, 70f, 10f, H(0xF5B90F), 5);
        }

        private static void TyreWall(RectTransform r)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    Dot(r, x * 86f, 40f - y * 84f, 6f, 6f, H(0x5B6573));
                    Ring(r, x * 86f, 40f - y * 84f, 76f, H(0x1B2A41));
                }
            }
        }

        private static void Lockers(RectTransform r)
        {
            for (int i = -1; i <= 1; i++)
            {
                Box(r, i * 88f, 0f, 82f, 230f, i == 0 ? H(0x2E6FD0) : H(0x3E8BFF), 8);
                for (int v = 0; v < 3; v++)
                {
                    Box(r, i * 88f, 84f - v * 12f, 44f, 5f, new Color(1f, 1f, 1f, 0.6f), 2);
                }

                Dot(r, i * 88f + 26f, 0f, 10f, 10f, Color.white);
            }
        }

        private static void WashingBay(RectTransform r)
        {
            Dot(r, 0f, 0f, 320f, 76f, new Color(0.62f, 0.83f, 0.91f, 0.9f));
            Ring(r, 150f, 50f, 70f, H(0x3FA34D));
            Box(r, 110f, 22f, 40f, 10f, H(0x3FA34D), 5, -30f);
            Box(r, 86f, 8f, 26f, 14f, H(0xF5B90F), 4, -30f);
        }

        private static void Pump(RectTransform r)
        {
            Box(r, 0f, -72f, 90f, 16f, H(0x3A3F4A), 6);
            Box(r, 0f, 0f, 52f, 136f, H(0xE5484D), 12);
            Dot(r, 0f, 40f, 44f, 44f, Color.white);
            Box(r, 4f, 44f, 4f, 18f, H(0x1B2A41), 2, -40f);
            Box(r, 44f, -16f, 8f, 90f, H(0x1B2A41), 4, 20f);
        }

        private static void Cones(RectTransform r)
        {
            for (int i = -1; i <= 1; i++)
            {
                UiFactory.CreateImage(r, PieceArt.Piece(3), new Vector2(i * 70f, 0f), new Vector2(64f, 64f), new Color(1f, 0.62f, 0.4f));
                Box(r, i * 70f, -30f, 70f, 10f, H(0xF2802E), 4);
            }
        }

        private static void TimingGate(RectTransform r)
        {
            Box(r, -210f, 0f, 18f, 230f, H(0x3A3F4A), 6);
            Box(r, 210f, 0f, 18f, 230f, H(0x3A3F4A), 6);
            Box(r, 0f, 110f, 450f, 64f, H(0x1B2A41), 10);
            Label(r, "FINISH", 0f, 110f, 300f, 34, UiTheme.Current.accent);
            for (int i = 0; i < 8; i++)
            {
                Box(r, -200f + i * 57f, 70f, 28f, 14f, i % 2 == 0 ? Color.white : H(0x1B2A41), 0);
            }
        }

        private static void WaterStation(RectTransform r)
        {
            Box(r, -80f, -50f, 12f, 90f, H(0x6E4B3A), 4);
            Box(r, 80f, -50f, 12f, 90f, H(0x6E4B3A), 4);
            Box(r, 0f, 0f, 210f, 16f, H(0x8A5F3D), 6);
            for (int i = -1; i <= 1; i++)
            {
                Box(r, i * 50f, 40f, 26f, 62f, H(0x3E8BFF), 8);
                Box(r, i * 50f, 76f, 14f, 10f, Color.white, 3);
            }
        }

        private static void Bench(RectTransform r)
        {
            Box(r, -80f, -40f, 12f, 70f, H(0x5B6573), 4);
            Box(r, 80f, -40f, 12f, 70f, H(0x5B6573), 4);
            Box(r, 0f, 0f, 210f, 22f, H(0x8A5F3D), 6);
            Box(r, 0f, 44f, 210f, 18f, H(0x8A5F3D), 6);
        }

        private static void Flags(RectTransform r)
        {
            foreach (var (x, colour) in new[] { (-220f, H(0xE5484D)), (220f, H(0x3E8BFF)) })
            {
                Box(r, x, -40f, 8f, 190f, H(0x5B6573), 3);
                Box(r, x + 40f, 30f, 76f, 50f, colour, 6, -6f);
            }
        }

        private static void LampPosts(RectTransform r)
        {
            foreach (float x in new[] { -400f, 400f })
            {
                Box(r, x, -40f, 14f, 260f, H(0x3A3F4A), 6);
                Box(r, x + (x < 0 ? 30f : -30f), 92f, 70f, 10f, H(0x3A3F4A), 5);
                Dot(r, x + (x < 0 ? 58f : -58f), 76f, 70f, 70f, new Color(1f, 0.88f, 0.48f, 0.4f));
                Dot(r, x + (x < 0 ? 58f : -58f), 80f, 30f, 30f, H(0xFFE07A));
            }
        }

        private static void Seats(RectTransform r)
        {
            for (int row = 0; row < 3; row++)
            {
                for (int i = 0; i < 9; i++)
                {
                    Box(r, -400f + i * 100f, 70f - row * 60f, 74f, 30f, row % 2 == 0 ? H(0xE5484D) : H(0xFFD23F), 8);
                }
            }
        }

        private static void Scoreboard(RectTransform r)
        {
            Box(r, 0f, 0f, 300f, 90f, H(0xFFD23F), 12);
            Box(r, 0f, 0f, 284f, 74f, H(0x1B2A41), 8);
            Label(r, "1:03.48", 0f, 0f, 280f, 40, H(0xFFD23F));
        }

        private static void Bunting(RectTransform r)
        {
            Box(r, 0f, 12f, 900f, 4f, Color.white, 0);
            Color[] colours = { H(0xE5484D), H(0xFFD23F), H(0x3E8BFF), H(0x3FA34D), H(0xFFFFFF) };
            for (int i = 0; i < 15; i++)
            {
                Box(r, -420f + i * 60f, -6f, 28f, 28f, colours[i % colours.Length], 2, 45f);
            }
        }

        private static void FoodStall(RectTransform r)
        {
            Box(r, 0f, -10f, 200f, 120f, Color.white, 10);
            Box(r, 0f, -4f, 150f, 60f, H(0x2E4A6B), 6);
            for (int i = 0; i < 5; i++)
            {
                Box(r, -80f + i * 40f, 72f, 40f, 44f, i % 2 == 0 ? H(0xE5484D) : Color.white, 0);
            }

            Box(r, 0f, -62f, 220f, 18f, H(0x8A5F3D), 6);
        }

        private static void Podium(RectTransform r)
        {
            Box(r, -125f, -30f, 124f, 80f, H(0xC9D1DA), 6);
            Box(r, 125f, -40f, 124f, 60f, H(0xE0A66A), 6);
            Box(r, 0f, -10f, 124f, 120f, H(0xFFD23F), 6);
            Label(r, "2", -125f, -30f, 100f, 40, H(0x1B2A41));
            Label(r, "3", 125f, -40f, 100f, 40, H(0x1B2A41));
            Label(r, "1", 0f, -10f, 100f, 48, H(0x1B2A41));
        }

        private static void TrophyCase(RectTransform r)
        {
            Box(r, 0f, 0f, 170f, 210f, H(0x8A5F3D), 12);
            Box(r, 0f, 6f, 136f, 170f, new Color(0.66f, 0.86f, 0.94f, 0.7f), 8);
            Box(r, 0f, -30f, 136f, 8f, H(0x6E4B3A), 2);
            UiFactory.CreateImage(r, PieceArt.Trophy, new Vector2(0f, 30f), new Vector2(96f, 96f), Color.white);
        }

        // ---- Shapes

        private static Image Box(RectTransform parent, float x, float y, float w, float h, Color colour, int radius, float angle = 0f)
        {
            var box = UiFactory.CreateRounded(parent, new Vector2(x, y), new Vector2(w, h), colour, radius);
            box.raycastTarget = false;
            if (angle != 0f)
            {
                box.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            return box;
        }

        private static void Dot(RectTransform parent, float x, float y, float w, float h, Color colour)
        {
            var dot = UiFactory.CreateImage(parent, PieceArt.Disc, new Vector2(x, y), new Vector2(w, h), colour);
            dot.preserveAspect = false; // ellipses as well as circles
        }

        private static void Ring(RectTransform parent, float x, float y, float d, Color colour) =>
            UiFactory.CreateImage(parent, PieceArt.Ring, new Vector2(x, y), new Vector2(d, d), colour);

        private static void Label(RectTransform parent, string text, float x, float y, float w, int size, Color colour)
        {
            var label = UiFactory.CreateText(parent, text, size, new Vector2(x, y), new Vector2(w, size * 1.4f), TextAlignmentOptions.Center, UiFont.Display);
            label.color = colour;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static Color H(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
