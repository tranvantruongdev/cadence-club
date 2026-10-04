using System.Linq;
using CadenceClub.Core;
using Cysharp.Threading.Tasks;
using Template.Feel;
using Template.Infra.Device;
using Template.UI;
using TMPro;
using UnityEngine;

namespace CadenceClub.UI
{
    /// <summary>
    /// The collection: every rider, recruited ones in colour with their level, the rest as "?". No reward for owning a
    /// set ("complete gacha"); a tap opens the rider's card.
    /// </summary>
    public sealed class RidersScreen : UIScreen
    {
        private ScreenStack _stack;
        private RiderDetailScreen _detail;
        private RectTransform _card;

        public override bool IsModal => true;

        public static RidersScreen Create(Transform parent, ScreenStack stack)
        {
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Riders";
            var view = dim.gameObject.AddComponent<RidersScreen>();
            view._stack = stack;
            view._detail = RiderDetailScreen.Create(parent, stack);
            view._detail.Changed += view.Build;
            dim.gameObject.SetActive(false);
            return view;
        }

        public UniTask OpenAsync()
        {
            Build();
            return _stack.PushAsync(this);
        }

        public UniTask OpenDetailAsync(string riderId) => _detail.OpenAsync(riderId);

        public override bool HandleBack()
        {
            _stack.PopAsync().Forget();
            return true;
        }

        private void Build()
        {
            if (_card != null)
            {
                Destroy(_card.gameObject);
            }

            var theme = UiTheme.Current;
            var md = Club.Master;
            var club = Club.Data;
            var riders = md.Riders.OrderByDescending(r => r.rarity).ThenBy(r => r.name).ToList();
            _card = UiFactory.CreateCard(transform, Vector2.zero, new Vector2(1000f, 1700f));
            UiFactory.CreateText(_card, "Riders", 88, new Vector2(-90f, 760f), new Vector2(700f, 120f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateIconButton(_card, theme.iconClose, new Vector2(420f, 760f), 96f, () => _stack.PopAsync().Forget(), ButtonStyle.Secondary, "x");
            UiFactory.CreateText(_card, $"{club.riders.Count(o => md.Rider(o.id) != null)} of {riders.Count} recruited", 40, new Vector2(-90f, 680f),
                new Vector2(700f, 60f), TextAlignmentOptions.MidlineLeft).color = theme.muted;

            for (int i = 0; i < riders.Count; i++)
            {
                var rider = riders[i];
                var owned = club.Owned(rider.id);
                var position = new Vector2((i % 3 - 1) * 310f, 470f - (i / 3) * 360f);
                string id = rider.id;
                var tile = UiFactory.CreateButton(_card, "", position, new Vector2(290f, 340f), () => OpenIfOwned(id), ButtonStyle.Secondary);
                ClubUi.Portrait(tile.transform, rider, new Vector2(0f, 50f), 190f, owned != null);
                UiFactory.CreateText(tile.transform, owned != null ? rider.name : rider.rarity.ToString(), 38, new Vector2(0f, -88f), new Vector2(280f, 54f),
                    TextAlignmentOptions.Center, UiFont.Display).color = owned != null ? theme.ink : theme.muted;
                if (owned != null)
                {
                    string level = owned.level >= ClubSave.MaxRiderLevel ? "Lv 5 · max" : $"Lv {owned.level} · {owned.shards}/{club.LevelUpCost(md, id)}";
                    var label = UiFactory.CreateText(tile.transform, level, 28, new Vector2(0f, -130f), new Vector2(280f, 44f));
                    label.color = club.CanLevelUp(md, id) ? theme.highlight : theme.muted;
                }
            }
        }

        private void OpenIfOwned(string id)
        {
            if (Club.Data.Owned(id) != null)
            {
                _detail.OpenAsync(id).Forget();
            }
        }
    }

    /// <summary>A rider's card: role, rarity, the power at their level, the charge, level 1–5 with a shard bar, Level Up.</summary>
    public sealed class RiderDetailScreen : UIScreen
    {
        private ScreenStack _stack;
        private RectTransform _card;
        private string _riderId;

        public event System.Action Changed;

        public override bool IsModal => true;

        public static RiderDetailScreen Create(Transform parent, ScreenStack stack)
        {
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Rider detail";
            var view = dim.gameObject.AddComponent<RiderDetailScreen>();
            view._stack = stack;
            dim.gameObject.SetActive(false);
            return view;
        }

        public UniTask OpenAsync(string riderId)
        {
            _riderId = riderId;
            Build();
            return _stack.PushAsync(this);
        }

        public override bool HandleBack()
        {
            _stack.PopAsync().Forget();
            return true;
        }

        private void Build()
        {
            if (_card != null)
            {
                Destroy(_card.gameObject);
            }

            var theme = UiTheme.Current;
            var md = Club.Master;
            var club = Club.Data;
            var rider = md.Rider(_riderId);
            var owned = club.Owned(_riderId);
            _card = UiFactory.CreateCard(transform, Vector2.zero, new Vector2(920f, 1360f));
            UiFactory.CreateIconButton(_card, theme.iconClose, new Vector2(380f, 600f), 96f, () => _stack.PopAsync().Forget(), ButtonStyle.Secondary, "x");
            ClubUi.Portrait(_card, rider, new Vector2(0f, 420f), 300f);
            UiFactory.CreateText(_card, rider.name, 88, new Vector2(0f, 200f), new Vector2(800f, 120f), TextAlignmentOptions.Center, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateText(_card, $"{rider.role} · {rider.rarity}", 42, new Vector2(0f, 120f), new Vector2(800f, 60f)).color = theme.muted;

            UiFactory.CreateRounded(_card, new Vector2(0f, -20f), new Vector2(800f, 170f), theme.paperEdge, 30);
            var power = UiFactory.CreateText(_card, RiderText.Power(rider, owned.level), 44, new Vector2(0f, 18f), new Vector2(760f, 90f),
                TextAlignmentOptions.Center, UiFont.Display);
            power.color = theme.ink;
            power.enableAutoSizing = true; // one line however long the power reads
            power.fontSizeMin = 28f;
            power.fontSizeMax = 44f;
            power.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.CreateText(_card, RiderText.Charge(rider), 34, new Vector2(0f, -60f), new Vector2(760f, 50f)).color = theme.muted;

            UiFactory.CreateText(_card, $"Level {owned.level} of {ClubSave.MaxRiderLevel}", 50, new Vector2(0f, -190f), new Vector2(800f, 70f),
                TextAlignmentOptions.Center, UiFont.Display).color = theme.ink;
            int cost = club.LevelUpCost(md, _riderId);
            if (owned.level < ClubSave.MaxRiderLevel)
            {
                float fill = Mathf.Clamp01(owned.shards / (float)cost);
                UiFactory.CreateRounded(_card, new Vector2(0f, -270f), new Vector2(700f, 34f), theme.paperEdge, 17);
                if (fill > 0f)
                {
                    UiFactory.CreateRounded(_card, new Vector2(-350f + 350f * fill, -270f), new Vector2(700f * fill, 34f), theme.accent, 17);
                }

                UiFactory.CreateText(_card, $"{owned.shards} / {cost} shards", 36, new Vector2(0f, -325f), new Vector2(700f, 50f)).color = theme.muted;
                bool ready = club.CanLevelUp(md, _riderId);
                UiFactory.CreateButton(_card, ready ? $"Level up to {owned.level + 1}" : "More shards from Recruit", new Vector2(0f, -500f),
                    new Vector2(720f, 150f), LevelUp, ready ? ButtonStyle.Primary : ButtonStyle.Secondary, ready ? theme.iconStar : null);
            }
            else
            {
                UiFactory.CreateText(_card, "Fully trained", 44, new Vector2(0f, -300f), new Vector2(700f, 60f), TextAlignmentOptions.Center, UiFont.Display)
                    .color = theme.highlight;
            }
        }

        private void LevelUp()
        {
            if (!Club.Data.TryLevelUp(Club.Master, _riderId))
            {
                return;
            }

            Club.Save();
            Haptics.Medium();
            Build();
            JuiceFx.Punch(_card, 0.06f, 0.3f);
            Changed?.Invoke();
        }
    }
}
