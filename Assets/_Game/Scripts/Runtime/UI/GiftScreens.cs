using System;
using CadenceClub.Art;
using CadenceClub.Audio;
using Cysharp.Threading.Tasks;
using Template.Feel;
using Template.Infra.Device;
using Template.UI;
using TMPro;
using UnityEngine;

namespace CadenceClub.UI
{
    /// <summary>The 7-day login calendar: claimed days ticked, today highlighted, Claim pays today's gems once a day.</summary>
    public sealed class DailyLoginScreen : UIScreen
    {
        private ScreenStack _stack;
        private RectTransform _card;

        public override bool IsModal => true;

        public static DailyLoginScreen Create(Transform parent, ScreenStack stack)
        {
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Daily gift";
            var view = dim.gameObject.AddComponent<DailyLoginScreen>();
            view._stack = stack;
            dim.gameObject.SetActive(false);
            return view;
        }

        public UniTask OpenAsync()
        {
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
            bool canClaim = club.CanClaimDaily(Club.Today);
            int done = canClaim ? club.DailyIndex : club.DailyIndex == 0 ? 7 : club.DailyIndex; // days ticked this week
            _card = UiFactory.CreateCard(transform, Vector2.zero, new Vector2(940f, 1160f));
            UiFactory.CreateText(_card, Loc.T("Daily gift"), 84, new Vector2(0f, 480f), new Vector2(800f, 120f), TextAlignmentOptions.Center, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateText(_card, Loc.T("A gift for every day you visit. Day 7 is the biggest."), 36, new Vector2(0f, 395f), new Vector2(820f, 60f))
                .color = theme.muted;
            for (int i = 0; i < md.DailyGems.Count; i++)
            {
                bool claimed = i < done;
                bool today = canClaim && i == done;
                var position = i < 4 ? new Vector2((i - 1.5f) * 210f, 210f) : new Vector2((i - 5f) * 210f, -30f);
                var tile = UiFactory.CreateRect($"Day {i + 1}", _card);
                tile.anchorMin = tile.anchorMax = new Vector2(0.5f, 0.5f);
                tile.anchoredPosition = position;
                tile.sizeDelta = new Vector2(190f, 220f);
                UiFactory.CreateRounded(tile, Vector2.zero, tile.sizeDelta, today ? theme.accent : claimed ? Color.Lerp(theme.accent, Color.white, 0.6f) : theme.paperEdge, 28);
                UiFactory.CreateText(tile, Loc.F("Day {0}", i + 1), 32, new Vector2(0f, 75f), new Vector2(180f, 46f), TextAlignmentOptions.Center, UiFont.Display)
                    .color = theme.ink;
                UiFactory.CreateImage(tile, PieceArt.Gem, new Vector2(0f, 5f), new Vector2(i == 6 ? 92f : 72f, i == 6 ? 92f : 72f), Color.white);
                UiFactory.CreateText(tile, md.DailyGems[i].ToString(), 38, new Vector2(0f, -70f), new Vector2(180f, 50f), TextAlignmentOptions.Center, UiFont.Display)
                    .color = theme.ink;
                if (claimed)
                {
                    UiFactory.CreateImage(tile, theme.iconCheck, new Vector2(60f, 80f), new Vector2(48f, 48f), new Color(0.25f, 0.62f, 0.33f));
                }
            }

            if (canClaim)
            {
                UiFactory.CreateButton(_card, Loc.F("Claim {0} gems", md.DailyGems[club.DailyIndex]), new Vector2(0f, -330f), new Vector2(680f, 150f), Claim,
                    ButtonStyle.Primary, PieceArt.Gem).FullColourIcon();
            }
            else
            {
                UiFactory.CreateText(_card, Loc.T("Come back tomorrow for the next gift."), 40, new Vector2(0f, -300f), new Vector2(800f, 60f)).color = theme.muted;
                UiFactory.CreateButton(_card, Loc.T("Close"), new Vector2(0f, -420f), new Vector2(500f, 120f), () => _stack.PopAsync().Forget(), ButtonStyle.Secondary);
            }
        }

        private void Claim()
        {
            if (Club.Data.ClaimDaily(Club.Master, Club.Today) <= 0)
            {
                return;
            }

            Club.Save();
            Haptics.Medium();
            ClubAudio.Play(ClubAudio.Coin);
            Build();
            JuiceFx.Punch(_card, 0.06f, 0.3f);
        }
    }

    /// <summary>
    /// Demo shop: three gem packs that cost nothing, under a clear "no real purchases" banner, to show the purchase flow
    /// without taking money.
    /// </summary>
    public sealed class ShopScreen : UIScreen
    {
        private ScreenStack _stack;
        private RectTransform _card;

        public override bool IsModal => true;

        public static ShopScreen Create(Transform parent, ScreenStack stack)
        {
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Shop";
            var view = dim.gameObject.AddComponent<ShopScreen>();
            view._stack = stack;
            dim.gameObject.SetActive(false);
            return view;
        }

        public UniTask OpenAsync()
        {
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
            _card = UiFactory.CreateCard(transform, Vector2.zero, new Vector2(940f, 1260f));
            UiFactory.CreateText(_card, Loc.T("Shop"), 88, new Vector2(-90f, 545f), new Vector2(700f, 120f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateIconButton(_card, theme.iconClose, new Vector2(390f, 545f), 96f, () => _stack.PopAsync().Forget(), ButtonStyle.Secondary, "x");
            UiFactory.CreateRounded(_card, new Vector2(0f, 420f), new Vector2(820f, 110f), theme.highlight, 30);
            UiFactory.CreateText(_card, Loc.T("Demo shop: no real purchases.") + "\n" + Loc.T("Packs are free in this build."), 34, new Vector2(0f, 422f), new Vector2(780f, 110f),
                TextAlignmentOptions.Center, UiFont.Display);
            for (int i = 0; i < md.Shop.Count; i++)
            {
                var item = md.Shop[i];
                var row = UiFactory.CreateRect(item.id, _card);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
                row.anchoredPosition = new Vector2(0f, 220f - i * 230f);
                row.sizeDelta = new Vector2(820f, 200f);
                UiFactory.CreateRounded(row, Vector2.zero, row.sizeDelta, theme.paperEdge, 30);
                UiFactory.CreateImage(row, PieceArt.Gem, new Vector2(-320f, 0f), Vector2.one * (90f + i * 20f), Color.white);
                UiFactory.CreateText(row, Loc.F("{0:N0} gems", item.gems), 52, new Vector2(-60f, 25f), new Vector2(360f, 70f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                    .color = theme.ink;
                UiFactory.CreateText(row, Loc.T(item.label), 32, new Vector2(-60f, -35f), new Vector2(360f, 50f), TextAlignmentOptions.MidlineLeft).color = theme.muted;
                string id = item.id;
                UiFactory.CreateButton(row, Loc.T("Free"), new Vector2(285f, 0f), new Vector2(200f, 120f), () => Claim(id), ButtonStyle.Primary);
            }

            UiFactory.CreateText(_card, Loc.F("You have {0:N0} gems", Club.Data.gems), 40, new Vector2(0f, -500f), new Vector2(800f, 60f), TextAlignmentOptions.Center,
                UiFont.Display).color = theme.ink;
        }

        private void Claim(string itemId)
        {
            if (Club.Data.ClaimShopItem(Club.Master, itemId) <= 0)
            {
                return;
            }

            Club.Save();
            Haptics.Light();
            ClubAudio.Play(ClubAudio.Coin);
            Build();
        }
    }
}
