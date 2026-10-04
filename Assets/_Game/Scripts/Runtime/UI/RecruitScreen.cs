using System;
using System.Collections.Generic;
using System.Linq;
using CadenceClub.Art;
using CadenceClub.Core;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Template.Core.Random;
using Template.Feel;
using Template.Infra.Device;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CadenceClub.UI
{
    /// <summary>
    /// The banner: featured SSR, the rates one tap from the pull buttons (stores require odds before any purchase of
    /// random items; shown here although nothing costs money), the pity countdown, and pulls for gems.
    /// </summary>
    public sealed class RecruitScreen : UIScreen
    {
        private ScreenStack _stack;
        private RatesScreen _rates;
        private RevealScreen _reveal;
        private RectTransform _card;
        private bool _pulling;

        public override bool IsModal => true;

        public static RecruitScreen Create(Transform parent, ScreenStack stack, RevealScreen reveal)
        {
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Recruit";
            var view = dim.gameObject.AddComponent<RecruitScreen>();
            view._stack = stack;
            view._reveal = reveal;
            view._rates = RatesScreen.Create(parent, stack);
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

        private static BannerDef Banner => Club.Master.Banner(Club.Master.Text("banner")) ?? Club.Master.Banners[0];

        private void Build()
        {
            if (_card != null)
            {
                Destroy(_card.gameObject);
            }

            var theme = UiTheme.Current;
            var md = Club.Master;
            var club = Club.Data;
            var banner = Banner;
            var featured = md.Rider(banner.featured);
            _card = UiFactory.CreateCard(transform, Vector2.zero, new Vector2(980f, 1620f));
            UiFactory.CreateText(_card, "Recruit", 88, new Vector2(-80f, 720f), new Vector2(700f, 120f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateIconButton(_card, theme.iconClose, new Vector2(410f, 720f), 96f, () => _stack.PopAsync().Forget(), ButtonStyle.Secondary, "x");

            // Banner panel: the featured rider.
            UiFactory.CreateRounded(_card, new Vector2(0f, 330f), new Vector2(900f, 600f), ClubUi.Night, 40);
            UiFactory.CreateRounded(_card, new Vector2(0f, 580f), new Vector2(330f, 64f), theme.accent, 32);
            UiFactory.CreateText(_card, "Featured SSR", 34, new Vector2(0f, 582f), new Vector2(330f, 64f), TextAlignmentOptions.Center, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateText(_card, banner.name, 76, new Vector2(0f, 480f), new Vector2(860f, 110f), TextAlignmentOptions.Center, UiFont.Display);
            ClubUi.Portrait(_card, featured, new Vector2(-230f, 250f), 300f);
            UiFactory.CreateText(_card, featured.name, 64, new Vector2(170f, 340f), new Vector2(460f, 90f), TextAlignmentOptions.MidlineLeft, UiFont.Display);
            UiFactory.CreateText(_card, featured.role, 38, new Vector2(170f, 270f), new Vector2(460f, 60f), TextAlignmentOptions.MidlineLeft)
                .color = new Color(1f, 1f, 1f, 0.7f);
            UiFactory.CreateText(_card, RiderText.Power(featured, 1), 36, new Vector2(170f, 170f), new Vector2(460f, 120f), TextAlignmentOptions.MidlineLeft);

            // Pity, gems, rates, pulls: everything about the pull sits together.
            UiFactory.CreateText(_card, $"SSR guaranteed within {club.PullsToPity(banner)} pulls", 42, new Vector2(0f, -40f), new Vector2(880f, 70f),
                TextAlignmentOptions.Center, UiFont.Display).color = theme.ink;
            UiFactory.CreateImage(_card, PieceArt.Gem, new Vector2(-175f, -130f), new Vector2(64f, 64f), Color.white);
            UiFactory.CreateText(_card, $"{club.gems:N0} gems", 46, new Vector2(70f, -128f), new Vector2(360f, 70f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                .color = theme.ink;
            Pull(new Vector2(-235f, -290f), 1, club, md);
            Pull(new Vector2(235f, -290f), 10, club, md);
            UiFactory.CreateButton(_card, "Rates", new Vector2(0f, -470f), new Vector2(300f, 110f), () => _rates.OpenAsync(Banner).Forget(),
                ButtonStyle.Secondary);
            UiFactory.CreateText(_card, "No real purchases: gems come from playing.\nEvery 10-pull has at least one SR.", 32, new Vector2(0f, -640f),
                new Vector2(860f, 110f)).color = theme.muted;
        }

        private void Pull(Vector2 position, int count, ClubSave club, MasterData md)
        {
            int cost = club.PullCost(md, count);
            bool affordable = club.gems >= cost;
            UiFactory.CreateButton(_card, $"×{count}  ·  {cost:N0}", position, new Vector2(430f, 160f), () => PullAsync(count).Forget(),
                affordable ? ButtonStyle.Primary : ButtonStyle.Secondary);
        }

        /// <summary>Spends gems, rolls, saves, then plays the reveal; the screen rebuilds with the new gems and pity.</summary>
        public async UniTask PullAsync(int count)
        {
            if (_pulling)
            {
                return;
            }

            var outcomes = Club.Data.TryPull(Club.Master, Banner, count, new GachaRoller(new SeededRandom((ulong)DateTime.UtcNow.Ticks)));
            if (outcomes == null)
            {
                JuiceFx.Punch(_card, 0.04f, 0.2f); // not enough gems
                return;
            }

            _pulling = true;
            Club.Save();
            await _reveal.PlayAsync(outcomes);
            Build();
            _pulling = false;
        }
    }

    /// <summary>Every rarity's odds, each rider's own chance, and how pity and the 10-pull floor work.</summary>
    public sealed class RatesScreen : UIScreen
    {
        private ScreenStack _stack;
        private RectTransform _card;

        public override bool IsModal => true;

        public static RatesScreen Create(Transform parent, ScreenStack stack)
        {
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Rates";
            var view = dim.gameObject.AddComponent<RatesScreen>();
            view._stack = stack;
            dim.gameObject.SetActive(false);
            return view;
        }

        public UniTask OpenAsync(BannerDef banner)
        {
            if (_card != null)
            {
                Destroy(_card.gameObject);
            }

            var theme = UiTheme.Current;
            var md = Club.Master;
            var rates = md.RateTables[banner.rateTable];
            _card = UiFactory.CreateCard(transform, Vector2.zero, new Vector2(940f, 1500f));
            UiFactory.CreateText(_card, "Rates", 80, new Vector2(-80f, 650f), new Vector2(700f, 110f), TextAlignmentOptions.MidlineLeft, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateIconButton(_card, theme.iconClose, new Vector2(390f, 650f), 96f, () => _stack.PopAsync().Forget(), ButtonStyle.Secondary, "x");

            float y = 530f;
            foreach (var rarity in new[] { Rarity.SSR, Rarity.SR, Rarity.R })
            {
                var riders = md.Riders.Where(r => r.rarity == rarity).ToList();
                UiFactory.CreateText(_card, $"{rarity}  {rates[rarity]}%", 48, new Vector2(0f, y), new Vector2(840f, 70f), TextAlignmentOptions.MidlineLeft,
                    UiFont.Display).color = theme.ink;
                y -= 60f;
                var lines = riders.Select(r => $"{r.name}{(r.id == banner.featured ? " (featured)" : "")}  {Chance(md, banner, r):0.##}%");
                var text = UiFactory.CreateText(_card, string.Join("   ·   ", lines), 32, new Vector2(0f, y - 30f), new Vector2(840f, 120f),
                    TextAlignmentOptions.TopLeft);
                text.color = theme.muted;
                y -= 160f;
            }

            UiFactory.CreateText(_card,
                $"An SSR is guaranteed by pull {banner.pity}: the counter shows how many pulls are left.\n" +
                $"Half of SSR pulls give the featured rider.\nEvery 10-pull has at least one SR.\nA rider you already have becomes shards that level them up.",
                34, new Vector2(0f, y - 120f), new Vector2(840f, 300f), TextAlignmentOptions.TopLeft).color = theme.ink;
            return _stack.PushAsync(this);
        }

        public override bool HandleBack()
        {
            _stack.PopAsync().Forget();
            return true;
        }

        /// <summary>One rider's chance per pull before pity (percent).</summary>
        public static double Chance(MasterData md, BannerDef banner, RiderDef rider)
        {
            double rarity = md.RateTables[banner.rateTable][rider.rarity];
            int same = md.Riders.Count(r => r.rarity == rider.rarity);
            if (rider.rarity != Rarity.SSR)
            {
                return rarity / same;
            }

            double featured = banner.featuredShare / 100.0;
            return rider.id == banner.featured ? rarity * featured : rarity * (1 - featured) / Math.Max(1, same - 1);
        }
    }

    /// <summary>
    /// The reveal: bikes roll in (0.8 s), a flash in the best rarity's colour teases the batch, then the cards flip one at
    /// a time; an SSR flips slowly with a burst and a line from the rider. Summary: every card, NEW or +shards.
    /// Skip jumps to the summary; a tap anywhere fast-forwards.
    /// </summary>
    public sealed class RevealScreen : UIScreen
    {
        private static readonly Dictionary<string, string> Lines = new Dictionary<string, string>
        {
            ["Sprinter"] = "Watch me go!",
            ["Climber"] = "Hills? Easy.",
            ["Mechanic"] = "Leave the bikes to me.",
            ["Rouleur"] = "Steady wins the day.",
            ["Coach"] = "Breathe, then push.",
        };

        private ScreenStack _stack;
        private RectTransform _content;
        private Image _flash;
        private readonly List<Card> _cards = new List<Card>();
        private bool _skip;
        private float _speed = 1f;
        private UniTaskCompletionSource _closed;

        private sealed class Card
        {
            public RectTransform root;
            public GameObject back;
            public GameObject front;
            public PullOutcome outcome;
        }

        public override bool IsModal => true;

        public static RevealScreen Create(Transform parent, ScreenStack stack)
        {
            var dim = UiFactory.CreateOverlay(parent);
            dim.name = "Reveal";
            dim.color = new Color(0.03f, 0.05f, 0.09f, 0.97f);
            var view = dim.gameObject.AddComponent<RevealScreen>();
            view._stack = stack;
            var tap = dim.gameObject.AddComponent<Button>(); // a tap anywhere fast-forwards
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => view._speed = 4f);
            dim.gameObject.SetActive(false);
            return view;
        }

        public override bool HandleBack()
        {
            _skip = true;
            return true;
        }

        public async UniTask PlayAsync(IReadOnlyList<PullOutcome> outcomes)
        {
            Build(outcomes);
            _skip = false;
            _speed = JuiceFx.ReduceMotion ? 4f : 1f;
            await _stack.PushAsync(this);
            await Run(outcomes);
            ShowSummary();
            _closed = new UniTaskCompletionSource();
            await _closed.Task;
        }

        /// <summary>Jumps to the summary (the Skip button).</summary>
        public void Skip() => _skip = true;

        private void Build(IReadOnlyList<PullOutcome> outcomes)
        {
            if (_content != null)
            {
                Destroy(_content.gameObject);
            }

            _cards.Clear();
            _content = UiFactory.CreateRect("Reveal content", transform);
            UiFactory.Stretch(_content);
            UiFactory.CreatePanel(_content, new Color(0.03f, 0.05f, 0.09f, 0.97f)).raycastTarget = false; // backdrop: the screen behind stays hidden
            _flash = UiFactory.CreatePanel(_content, new Color(1f, 1f, 1f, 0f));
            _flash.raycastTarget = false;
            var skip = UiFactory.CreateButton(_content, "Skip", Vector2.zero, new Vector2(220f, 100f), Skip, ButtonStyle.Glass);
            UiFactory.Place(skip, new Vector2(1f, 1f), new Vector2(-150f, -110f));

            bool single = outcomes.Count == 1;
            var size = single ? new Vector2(440f, 600f) : new Vector2(184f, 260f);
            for (int i = 0; i < outcomes.Count; i++)
            {
                var position = single ? Vector2.zero : new Vector2((i % 5 - 2) * 200f, i < 5 ? 150f : -150f);
                _cards.Add(BuildCard(outcomes[i], position, size));
            }
        }

        private Card BuildCard(PullOutcome outcome, Vector2 position, Vector2 size)
        {
            var theme = UiTheme.Current;
            var rider = Club.Master.Rider(outcome.pull.riderId);
            float k = size.x / 184f; // everything scales with the card
            var root = UiFactory.CreateRect($"Card {rider.name}", _content);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = position;
            root.sizeDelta = size;

            var back = UiFactory.CreateRect("Back", root);
            UiFactory.Stretch(back);
            UiFactory.CreateRounded(back, Vector2.zero, size, theme.accentEdge, Mathf.RoundToInt(22 * k));
            UiFactory.CreateRounded(back, Vector2.zero, size - Vector2.one * 12f * k, ClubUi.Night, Mathf.RoundToInt(18 * k));
            UiFactory.CreateText(back, "C", Mathf.RoundToInt(110 * k), Vector2.zero, size, TextAlignmentOptions.Center, UiFont.Display).color = theme.accent;

            var front = UiFactory.CreateRect("Front", root);
            UiFactory.Stretch(front);
            UiFactory.CreateRounded(front, Vector2.zero, size, ClubUi.RarityColor(outcome.pull.rarity), Mathf.RoundToInt(22 * k));
            UiFactory.CreateRounded(front, Vector2.zero, size - Vector2.one * 12f * k, theme.paper, Mathf.RoundToInt(18 * k));
            ClubUi.Portrait(front, rider, new Vector2(0f, 40f * k), 120f * k);
            UiFactory.CreateText(front, rider.name, Mathf.RoundToInt(30 * k), new Vector2(0f, -58f * k), new Vector2(size.x, 44f * k),
                TextAlignmentOptions.Center, UiFont.Display).color = theme.ink;
            string tag = outcome.grant.isNew ? "NEW" : $"+{outcome.grant.shards} shards";
            UiFactory.CreateRounded(front, new Vector2(0f, -100f * k), new Vector2(150f * k, 36f * k), outcome.grant.isNew ? theme.highlight : theme.paperEdge,
                Mathf.RoundToInt(18 * k));
            UiFactory.CreateText(front, tag, Mathf.RoundToInt(22 * k), new Vector2(0f, -98f * k), new Vector2(150f * k, 36f * k), TextAlignmentOptions.Center,
                UiFont.Display).color = outcome.grant.isNew ? Color.white : theme.ink;
            front.gameObject.SetActive(false);
            root.localScale = Vector3.zero;
            return new Card { root = root, back = back.gameObject, front = front.gameObject, outcome = outcome };
        }

        private async UniTask Run(IReadOnlyList<PullOutcome> outcomes)
        {
            // 1. Build-up: two wheels roll in.
            for (int i = 0; i < 2; i++)
            {
                var wheelImage = UiFactory.CreateImage(_content, PieceArt.Piece(0), new Vector2(-900f - i * 200f, 520f), new Vector2(170f, 170f), Color.white);
                wheelImage.name = "Wheel";
                var wheel = wheelImage
                    .rectTransform;
                Tween.LocalPositionX(wheel, i == 0 ? 110f : -110f, 0.8f / _speed, Ease.OutCubic);
                // Euler angles, not a rotation: 720° as a rotation equals 0° and wouldn't turn at all.
                Tween.EulerAngles(wheel, Vector3.zero, new Vector3(0f, 0f, -720f), 0.8f / _speed, Ease.OutCubic);
            }

            await Wait(0.8f);

            // 2. The best rarity's colour flashes the screen.
            var best = outcomes.Max(o => o.pull.rarity);
            var tint = ClubUi.RarityColor(best);
            Tween.Color(_flash, new Color(tint.r, tint.g, tint.b, best == Rarity.R ? 0.15f : 0.5f), 0.25f / _speed, cycles: 2, cycleMode: CycleMode.Yoyo);
            foreach (var card in _cards)
            {
                Tween.Scale(card.root, 1f, 0.25f / _speed, Ease.OutBack);
            }

            await Wait(0.6f);

            // 3. Flips, one at a time; SSRs slow, with a burst and a voice line.
            foreach (var card in _cards)
            {
                if (_skip)
                {
                    return;
                }

                bool ssr = card.outcome.pull.rarity == Rarity.SSR;
                await Flip(card, ssr ? 0.9f : 0.3f);
                if (ssr)
                {
                    Haptics.Medium();
                    Burst(card.root);
                    var rider = Club.Master.Rider(card.outcome.pull.riderId);
                    string said = Lines.TryGetValue(rider.role, out var l) ? l : "Let's ride!";
                    var line = UiFactory.CreateText(_content, $"{rider.name}: “{said}”", 56, new Vector2(0f, -560f), new Vector2(980f, 120f),
                        TextAlignmentOptions.Center, UiFont.Story);
                    await Wait(1.3f);
                    if (line != null)
                    {
                        Destroy(line.gameObject);
                    }
                }
            }
        }

        private async UniTask Flip(Card card, float seconds)
        {
            await Tween.Scale(card.root, new Vector3(0f, 1f, 1f), seconds * 0.5f / _speed, Ease.InSine);
            card.back.SetActive(false);
            card.front.SetActive(true);
            await Tween.Scale(card.root, Vector3.one, seconds * 0.5f / _speed, Ease.OutBack);
        }

        private void Burst(RectTransform at)
        {
            for (int i = 0; i < 14; i++)
            {
                float angle = i * Mathf.PI * 2f / 14f;
                var dot = UiFactory.CreateImage(_content, PieceArt.Disc, at.anchoredPosition, new Vector2(34f, 34f), PieceArt.Colors[i % 5]);
                dot.name = "Burst";
                var target = at.anchoredPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 420f;
                Tween.LocalPosition(dot.rectTransform, new Vector3(target.x, target.y, 0f), 0.7f, Ease.OutCubic);
                Tween.Color(dot, new Color(dot.color.r, dot.color.g, dot.color.b, 0f), 0.7f, Ease.InQuad)
                    .OnComplete(dot.gameObject, go => Destroy(go), warnIfTargetDestroyed: false);
            }
        }

        private async UniTask Wait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds && !_skip)
            {
                elapsed += Time.unscaledDeltaTime * _speed;
                await UniTask.Yield();
            }
        }

        private void ShowSummary()
        {
            foreach (Transform child in _content)
            {
                Tween.StopAll(child);
            }

            foreach (var card in _cards)
            {
                card.back.SetActive(false);
                card.front.SetActive(true);
                card.root.localScale = Vector3.one;
            }

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i);
                if (child.name.StartsWith("Button Skip") || child.GetComponent<TextMeshProUGUI>() != null || child.name == "Wheel" ||
                    child.name == "Burst")
                {
                    Destroy(child.gameObject); // the skip button, a voice line, the wheels, burst dots
                }
            }

            var theme = UiTheme.Current;
            int fresh = _cards.Count(c => c.outcome.grant.isNew);
            var title = UiFactory.CreateText(_content, fresh > 0 ? $"{fresh} new {(fresh == 1 ? "rider" : "riders")}!" : "Shards for your riders", 80,
                Vector2.zero, new Vector2(1000f, 120f), TextAlignmentOptions.Center, UiFont.Display);
            UiFactory.Place(title, new Vector2(0.5f, 0.5f), new Vector2(0f, 420f));
            var done = UiFactory.CreateButton(_content, "Done", Vector2.zero, new Vector2(600f, 150f), Close, ButtonStyle.Primary, theme.iconCheck);
            UiFactory.Place(done, new Vector2(0.5f, 0.5f), new Vector2(0f, -430f));
        }

        private void Close()
        {
            _stack.PopAsync().Forget();
            _closed?.TrySetResult();
        }

        private void OnDestroy()
        {
            if (_content != null)
            {
                foreach (Transform child in _content)
                {
                    Tween.StopAll(child);
                }
            }
        }
    }
}
