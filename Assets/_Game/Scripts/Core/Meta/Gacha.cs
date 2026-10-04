using System.Collections.Generic;
using System.Linq;
using Template.Core.Random;

namespace CadenceClub.Core
{
    public struct PullResult
    {
        public string riderId;
        public Rarity rarity;
        public bool featured;
    }

    /// <summary>
    /// Rolls a banner. Rarity by the rate table (R 80 · SR 17 · SSR 3), an SSR guaranteed on the banner's pity pull
    /// (60: pity counts pulls since the last SSR), at least one SR or better in every 10-pull, and the featured
    /// rider on <see cref="BannerDef.featuredShare"/>% of SSR pulls. Same seed, same results.
    /// No "complete gacha" rewards: nothing here pays out for owning a set.
    /// </summary>
    public sealed class GachaRoller
    {
        private readonly SeededRandom _rng;

        public GachaRoller(SeededRandom rng)
        {
            _rng = rng;
        }

        /// <param name="sinceSsr">Pulls since the last SSR on this banner; updated.</param>
        public List<PullResult> Pull(MasterData md, BannerDef banner, int count, ref int sinceSsr)
        {
            var weights = md.RateTables[banner.rateTable];
            var results = new List<PullResult>(count);
            for (int i = 0; i < count; i++)
            {
                var rarity = sinceSsr + 1 >= banner.pity ? Rarity.SSR : Roll(weights);
                results.Add(Pick(md, banner, rarity));
                sinceSsr = rarity == Rarity.SSR ? 0 : sinceSsr + 1;
            }

            if (count >= 10 && results.All(r => r.rarity == Rarity.R))
            {
                results[results.Count - 1] = Pick(md, banner, Rarity.SR); // the 10-pull floor; pity unaffected (not an SSR)
            }

            return results;
        }

        private Rarity Roll(Dictionary<Rarity, int> weights)
        {
            int roll = _rng.Range(0, weights.Values.Sum());
            foreach (var pair in weights.OrderBy(p => p.Key))
            {
                if (roll < pair.Value)
                {
                    return pair.Key;
                }

                roll -= pair.Value;
            }

            return Rarity.R;
        }

        private PullResult Pick(MasterData md, BannerDef banner, Rarity rarity)
        {
            if (rarity == Rarity.SSR && _rng.Range(0, 100) < banner.featuredShare)
            {
                return new PullResult { riderId = banner.featured, rarity = rarity, featured = true };
            }

            var pool = md.Riders.Where(r => r.rarity == rarity && (rarity != Rarity.SSR || r.id != banner.featured)).ToList();
            if (pool.Count == 0)
            {
                return new PullResult { riderId = banner.featured, rarity = Rarity.SSR, featured = true };
            }

            var rider = pool[_rng.Range(0, pool.Count)];
            return new PullResult { riderId = rider.id, rarity = rarity, featured = rider.id == banner.featured };
        }
    }
}
