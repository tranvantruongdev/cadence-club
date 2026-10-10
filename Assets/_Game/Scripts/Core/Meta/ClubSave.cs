using System;
using System.Collections.Generic;
using System.Linq;

namespace CadenceClub.Core
{
    [Serializable]
    public sealed class OwnedRider
    {
        public string id;
        public int level = 1;
        public int shards;
    }

    public struct GrantResult
    {
        public string riderId;
        public bool isNew;

        /// <summary>Shards added (duplicates only).</summary>
        public int shards;
    }

    public struct PullOutcome
    {
        public PullResult pull;
        public GrantResult grant;
    }

    public struct WinResult
    {
        public int stars;
        public int coins;
    }

    public struct BuildResult
    {
        public bool built;

        /// <summary>The area this task finished, or null.</summary>
        public AreaDef completedArea;

        public int gems;
    }

    /// <summary>
    /// Everything the player owns and has done, plus the rules that change it: wallet (★ stars, coins, gems), lives
    /// that refill over time, level progress, renovation, the rider collection and squad, and gacha pulls. Pure C#;
    /// the game stores it inside the template's save file. Times are UTC ticks passed in, so tests control the clock.
    /// </summary>
    [Serializable]
    public sealed class ClubSave
    {
        public const int MaxRiderLevel = 5;

        /// <summary>The next level to play (1-based).</summary>
        public int level = 1;

        public int stars;
        public int coins;
        public int gems;
        public int lives;

        /// <summary>A loss that still needs its one life charge (resolved on leave or next boot).</summary>
        public bool pendingLoss;

        /// <summary>When the current life started refilling (UTC ticks).</summary>
        public long livesSince;

        public List<string> built = new List<string>();
        public List<OwnedRider> riders = new List<OwnedRider>();

        /// <summary>Pulls since the last SSR, for the pity counter.</summary>
        public int pity;

        public List<string> squad = new List<string>();
        public bool started;
        public bool freeRiderGiven;

        /// <summary>Daily login: claims so far (the next calendar day is claims % 7) and the last day claimed.</summary>
        public int loginClaims;

        /// <summary>Local calendar day of the last claim, as days since 1970-01-01; -1 before the first.</summary>
        public int lastLoginDay = -1;

        // ---- Coins: boosters before a level, +5 moves after losing.

        public bool TrySpendCoins(int amount)
        {
            if (amount < 0 || coins < amount)
            {
                return false;
            }

            coins -= amount;
            return true;
        }

        public int BoostersCost(MasterData md, IEnumerable<string> boosterIds) =>
            boosterIds.Select(id => md.Boosters.FirstOrDefault(b => b.id == id)).Where(b => b != null).Sum(b => b.cost);

        /// <summary>"+5 moves" on a lost level: pays coins; the level then carries on.</summary>
        public bool TryBuyContinue(MasterData md) => TrySpendCoins(md.Int("extra_moves_cost"));

        // ---- Daily login: one claim per calendar day, days 1–7 then round again; a missed day doesn't reset it.

        public bool CanClaimDaily(int today) => lastLoginDay != today;

        /// <summary>The calendar day (0–6) the next claim pays.</summary>
        public int DailyIndex => loginClaims % 7;

        /// <summary>Pays today's gems; 0 if today is already claimed.</summary>
        public int ClaimDaily(MasterData md, int today)
        {
            if (!CanClaimDaily(today) || md.DailyGems.Count == 0)
            {
                return 0;
            }

            int gemsToday = md.DailyGems[DailyIndex % md.DailyGems.Count];
            gems += gemsToday;
            loginClaims++;
            lastLoginDay = today;
            return gemsToday;
        }

        // ---- Demo shop: gem packs that cost nothing in this build ("no real purchases").

        public int ClaimShopItem(MasterData md, string itemId)
        {
            var item = md.Shop.FirstOrDefault(s => s.id == itemId);
            if (item == null)
            {
                return 0;
            }

            gems += item.gems;
            return item.gems;
        }

        /// <summary>First launch: the starting gems and full lives.</summary>
        public void StartIfNew(MasterData md)
        {
            if (started)
            {
                return;
            }

            started = true;
            gems += md.Int("start_gems");
            lives = md.Int("lives_max");
        }

        // ---- Lives: one refills every life_seconds (60 s in this build) up to lives_max.

        /// <summary>Records a loss until Continue clears it or leaving/reboot resolves its life charge.</summary>
        public bool MarkPendingLoss()
        {
            if (pendingLoss)
            {
                return false;
            }

            pendingLoss = true;
            return true;
        }

        public void ClearPendingLoss() => pendingLoss = false;

        /// <summary>Clears and charges a saved loss once. Returns true when a pending loss was resolved.</summary>
        public bool ResolvePendingLoss(MasterData md, long now)
        {
            if (!pendingLoss)
            {
                return false;
            }

            pendingLoss = false;
            SpendLife(md, now);
            return true;
        }

        public int Lives(MasterData md, long now)
        {
            Refill(md, now);
            return lives;
        }

        public TimeSpan NextLifeIn(MasterData md, long now)
        {
            Refill(md, now);
            return lives >= md.Int("lives_max") ? TimeSpan.Zero : TimeSpan.FromTicks(LifeTicks(md) - (now - livesSince));
        }

        /// <summary>Losing a level costs a life. False when there is none to spend.</summary>
        public bool SpendLife(MasterData md, long now)
        {
            Refill(md, now);
            if (lives <= 0)
            {
                return false;
            }

            if (lives >= md.Int("lives_max"))
            {
                livesSince = now; // the refill timer starts with the first missing life
            }

            lives--;
            return true;
        }

        private void Refill(MasterData md, long now)
        {
            int max = md.Int("lives_max");
            if (lives >= max)
            {
                lives = max;
                return;
            }

            long gained = (now - livesSince) / LifeTicks(md);
            if (gained > 0)
            {
                lives = (int)Math.Min(max, lives + gained);
                livesSince += gained * LifeTicks(md);
            }
        }

        private static long LifeTicks(MasterData md) => TimeSpan.FromSeconds(Math.Max(1, md.Int("life_seconds"))).Ticks;

        // ---- Levels

        /// <summary>Coins every win (more for moves left); a ★ and the next level only for the first win of the newest level.</summary>
        public WinResult Win(MasterData md, int levelId, int movesLeft)
        {
            var result = new WinResult { coins = md.Int("win_coins") + md.Int("coins_per_move_left") * Math.Max(0, movesLeft) };
            coins += result.coins;
            if (levelId == level)
            {
                result.stars = 1;
                stars++;
                level++;
            }

            return result;
        }

        /// <summary>The scripted gift: the free rider once the player is past level free_rider_after_level.</summary>
        public GrantResult? TryGiveFreeRider(MasterData md)
        {
            if (freeRiderGiven || level <= md.Int("free_rider_after_level"))
            {
                return null;
            }

            freeRiderGiven = true;
            var grant = Grant(md, md.Text("free_rider"));
            if (squad.Count == 0)
            {
                squad.Add(grant.riderId);
            }

            return grant;
        }

        public bool RecruitUnlocked(MasterData md) => level > md.Int("recruit_after_level");

        /// <summary>
        /// The scripted first session: until level home_after_level is won, the app opens straight into the next level
        /// and the win card only offers the next one.
        /// </summary>
        public bool InFirstSession(MasterData md) => level <= md.Int("home_after_level");

        /// <summary>One slot with the free rider, two once Recruit opens.</summary>
        public int SquadSlots(MasterData md) => RecruitUnlocked(md) ? 2 : freeRiderGiven ? 1 : 0;

        // ---- Renovation: tasks cost ★; finishing an area pays gems and tells its story beat.

        public bool IsBuilt(string task) => built.Contains(task);

        public BuildResult TryBuild(MasterData md, string taskId)
        {
            var task = md.Tasks.FirstOrDefault(t => t.id == taskId);
            if (task == null || IsBuilt(taskId) || stars < task.stars)
            {
                return default;
            }

            stars -= task.stars;
            built.Add(taskId);
            var result = new BuildResult { built = true };
            if (md.Tasks.Where(t => t.area == task.area).All(t => IsBuilt(t.id)))
            {
                result.completedArea = md.Areas.First(a => a.id == task.area);
                result.gems = md.Int("area_gems");
                gems += result.gems;
            }

            return result;
        }

        /// <summary>The first area with work left (the last area once everything is built).</summary>
        public AreaDef CurrentArea(MasterData md) =>
            md.Areas.FirstOrDefault(a => md.Tasks.Any(t => t.area == a.id && !IsBuilt(t.id))) ?? md.Areas.LastOrDefault();

        // ---- Riders

        public OwnedRider Owned(string id) => riders.FirstOrDefault(r => r.id == id);

        /// <summary>A new rider joins at level 1; a duplicate turns into shards by rarity.</summary>
        public GrantResult Grant(MasterData md, string riderId)
        {
            var owned = Owned(riderId);
            if (owned == null)
            {
                riders.Add(new OwnedRider { id = riderId });
                return new GrantResult { riderId = riderId, isNew = true };
            }

            int shards = md.DuplicateShards[md.Rider(riderId).rarity];
            owned.shards += shards;
            return new GrantResult { riderId = riderId, shards = shards };
        }

        public int LevelUpCost(MasterData md, string riderId)
        {
            var owned = Owned(riderId);
            return owned == null || owned.level >= MaxRiderLevel ? 0 : md.LevelUpShards[owned.level + 1];
        }

        public bool CanLevelUp(MasterData md, string riderId)
        {
            var owned = Owned(riderId);
            return owned != null && owned.level < MaxRiderLevel && owned.shards >= LevelUpCost(md, riderId);
        }

        public bool TryLevelUp(MasterData md, string riderId)
        {
            if (!CanLevelUp(md, riderId))
            {
                return false;
            }

            var owned = Owned(riderId);
            owned.shards -= LevelUpCost(md, riderId);
            owned.level++;
            return true;
        }

        /// <summary>Puts an owned rider in a squad slot; a rider already in the other slot swaps over.</summary>
        public bool SetSquad(MasterData md, int slot, string riderId)
        {
            if (slot < 0 || slot >= SquadSlots(md) || Owned(riderId) == null)
            {
                return false;
            }

            while (squad.Count <= slot)
            {
                squad.Add(null);
            }

            int other = squad.IndexOf(riderId);
            if (other >= 0 && other != slot)
            {
                squad[other] = squad[slot];
            }

            squad[slot] = riderId;
            return true;
        }

        /// <summary>The squad riders that are set, in slot order.</summary>
        public IEnumerable<OwnedRider> Squad(MasterData md) =>
            squad.Take(SquadSlots(md)).Where(id => id != null).Select(Owned).Where(r => r != null);

        // ---- Recruit

        public int PullCost(MasterData md, int count) => count >= 10 ? md.Int("ten_pull_cost") : md.Int("pull_cost") * count;

        public int PullsToPity(BannerDef banner) => Math.Max(1, banner.pity - pity);

        /// <summary>Spends gems and pulls; null when Recruit is locked or the gems don't cover it.</summary>
        public List<PullOutcome> TryPull(MasterData md, BannerDef banner, int count, GachaRoller roller)
        {
            int cost = PullCost(md, count);
            if (!RecruitUnlocked(md) || gems < cost)
            {
                return null;
            }

            gems -= cost;
            var pulls = roller.Pull(md, banner, count, ref pity);
            return pulls.Select(p => new PullOutcome { pull = p, grant = Grant(md, p.riderId) }).ToList();
        }
    }
}
