using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Template.Core.Random;

namespace CadenceClub.Core.Tests
{
    /// <summary>Gacha, collection, lives, rewards and renovation rules, on small master data written here.</summary>
    public class MetaTests
    {
        private static readonly long T0 = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc).Ticks;

        private static MasterData Md(int pity = 60, string weights = "R,80\nstandard,SR,17\nstandard,SSR,3")
        {
            var tables = new Dictionary<string, string>
            {
                ["config"] = "key,value\nstart_gems,500\nlives_max,5\nlife_seconds,60\nwin_coins,50\ncoins_per_move_left,10\narea_gems,300\n" +
                             "pull_cost,100\nten_pull_cost,1000\nbanner,b\nfree_rider,r1\nfree_rider_after_level,3\nrecruit_after_level,5\n" +
                             "extra_moves,5\nextra_moves_cost,300\nhome_after_level,3",
                ["boosters"] = "id,name,special,count,cost\nrockets,Rockets,RocketH,2,150\nbomb,Bomb,Bomb,1,200\ndisco,Disco,Disco,1,350",
                ["daily_login"] = "day,gems\n1,20\n2,25\n3,30\n4,35\n5,40\n6,45\n7,50",
                ["shop"] = "id,label,gems\nhandful,Handful,100",
                ["strings"] = "en,vi,ja\nShop,Cửa hàng,ショップ\n\"Play level {0}\",Chơi màn {0},レベル{0}をプレイ",
                ["riders"] = "id,name,role,rarity,color,power,charge,power_by_level\n" +
                             "r1,Rider One,Sprinter,R,0,RowRockets,22,1;1;2;2;2\nr2,Rider Two,Climber,R,1,ColumnRockets,22,1;1;2;2;2\n" +
                             "s1,Super One,Mechanic,SR,2,BreakObstacles,18,5;5;6;6;7\ns2,Super Two,Rouleur,SR,3,MakeSpecials,18,5;5;6;6;7\n" +
                             "x1,Star One,Sprinter,SSR,0,RowRockets,14,3;3;4;4;5\nx2,Star Two,Coach,SSR,4,ExtraMoves,16,3;3;4;4;5",
                ["rate_tables"] = "table,rarity,weight\nstandard," + weights,
                ["banners"] = $"id,name,featured,rate_table,pity,featured_share\nb,Banner,x1,standard,{pity},50",
                ["rarities"] = "rarity,duplicate_shards\nR,5\nSR,15\nSSR,40",
                ["rider_levels"] = "level,shards\n2,10\n3,25\n4,50\n5,100",
                ["areas"] = "area,name,story\n1,Shed,\"Grandpa: \"\"It lives.\"\"\"\n2,Yard,Done",
                ["renovation"] = "task,area,name,stars\n" + string.Join("\n", Enumerable.Range(1, 6).Select(i => $"a{i},1,Task A{i},1")) + "\n" +
                                 string.Join("\n", Enumerable.Range(1, 6).Select(i => $"b{i},2,Task B{i},1")),
            };
            return MasterData.Parse(name => tables.TryGetValue(name, out var text) ? text : null);
        }

        private static List<PullResult> Pulls(MasterData md, int count, int batch = 1, ulong seed = 1)
        {
            var roller = new GachaRoller(new SeededRandom(seed));
            var banner = md.Banners[0];
            int pity = 0;
            var all = new List<PullResult>();
            for (int i = 0; i < count; i += batch)
            {
                all.AddRange(roller.Pull(md, banner, batch, ref pity));
            }

            return all;
        }

        [Test]
        public void Csv_reads_quotes_commas_and_blank_lines()
        {
            var rows = Csv.Parse("a,b\r\n\"x, \"\"y\"\"\",2\n\n");
            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { "x, \"y\"", "2" }, rows[1]);
        }

        [Test]
        public void Master_data_validates_and_explains_mistakes()
        {
            var md = Md();
            CollectionAssert.IsEmpty(md.Validate());
            Assert.AreEqual("Grandpa: \"It lives.\"", md.Areas[0].story);
            Assert.AreEqual(4, md.Rider("x1").Power(3));

            var broken = Md(pity: 150, weights: "R,80\nstandard,SR,16\nstandard,SSR,3").Validate();
            Assert.That(broken, Has.Some.Contains("add up to 99"));
            Assert.That(broken, Has.Some.Contains("pity 150"));
        }

        [Test]
        public void Rates_match_the_table_over_many_pulls()
        {
            var pulls = Pulls(Md(pity: 1000000), 100000);
            double Share(Rarity r) => pulls.Count(p => p.rarity == r) / (double)pulls.Count;
            Assert.AreEqual(0.80, Share(Rarity.R), 0.01);
            Assert.AreEqual(0.17, Share(Rarity.SR), 0.01);
            Assert.AreEqual(0.03, Share(Rarity.SSR), 0.004);
        }

        [Test]
        public void Pity_guarantees_an_SSR_by_the_60th_pull()
        {
            // With no SSR in the table, pity alone makes exactly every 60th pull an SSR.
            var forced = Pulls(Md(weights: "R,83\nstandard,SR,17\nstandard,SSR,0"), 600);
            var ssrAt = forced.Select((p, i) => (p, i)).Where(x => x.p.rarity == Rarity.SSR).Select(x => x.i + 1).ToList();
            CollectionAssert.AreEqual(new[] { 60, 120, 180, 240, 300, 360, 420, 480, 540, 600 }, ssrAt);

            // With real rates, no run of pulls without an SSR is longer than 59.
            int gap = 0;
            int longest = 0;
            foreach (var p in Pulls(Md(), 30000))
            {
                gap = p.rarity == Rarity.SSR ? 0 : gap + 1;
                longest = Math.Max(longest, gap);
            }

            Assert.LessOrEqual(longest, 59);
        }

        [Test]
        public void Every_ten_pull_has_an_SR_or_better()
        {
            var pulls = Pulls(Md(weights: "R,100\nstandard,SR,0\nstandard,SSR,0"), 50, batch: 10);
            for (int batch = 0; batch < 5; batch++)
            {
                var ten = pulls.Skip(batch * 10).Take(10).ToList();
                Assert.AreEqual(1, ten.Count(p => p.rarity == Rarity.SR), $"10-pull {batch + 1}: the last R is lifted to SR");
                Assert.AreEqual(Rarity.SR, ten[9].rarity);
            }
        }

        [Test]
        public void The_featured_rider_is_half_of_the_SSRs_and_seeds_repeat()
        {
            var pulls = Pulls(Md(weights: "R,0\nstandard,SR,0\nstandard,SSR,100"), 4000);
            Assert.AreEqual(0.5, pulls.Count(p => p.riderId == "x1") / 4000.0, 0.03);
            Assert.IsTrue(pulls.Where(p => p.riderId == "x1").All(p => p.featured));

            var again = Pulls(Md(), 200, 10, seed: 9);
            CollectionAssert.AreEqual(again.Select(p => p.riderId), Pulls(Md(), 200, 10, seed: 9).Select(p => p.riderId));
        }

        [Test]
        public void Duplicates_become_shards_and_shards_level_riders_up_to_5()
        {
            var md = Md();
            var save = new ClubSave();
            Assert.IsTrue(save.Grant(md, "r1").isNew);
            Assert.AreEqual(5, save.Grant(md, "r1").shards, "an R duplicate is 5 shards");
            save.Grant(md, "r1");
            Assert.IsTrue(save.TryLevelUp(md, "r1"), "10 shards reach level 2");
            Assert.AreEqual((2, 0), (save.Owned("r1").level, save.Owned("r1").shards));
            Assert.IsFalse(save.TryLevelUp(md, "r1"), "level 3 needs 25");

            save.Owned("r1").shards = 1000;
            while (save.TryLevelUp(md, "r1"))
            {
            }

            Assert.AreEqual(5, save.Owned("r1").level);
            Assert.AreEqual(1000 - 25 - 50 - 100, save.Owned("r1").shards);
        }

        [Test]
        public void Lives_refill_one_a_minute_up_to_five()
        {
            var md = Md();
            var save = new ClubSave();
            save.StartIfNew(md);
            Assert.AreEqual(5, save.Lives(md, T0));
            save.SpendLife(md, T0);
            save.SpendLife(md, T0);
            Assert.AreEqual(3, save.Lives(md, T0 + TimeSpan.FromSeconds(59).Ticks));
            Assert.AreEqual(4, save.Lives(md, T0 + TimeSpan.FromSeconds(60).Ticks));
            Assert.AreEqual(TimeSpan.FromSeconds(60), save.NextLifeIn(md, T0 + TimeSpan.FromSeconds(60).Ticks));
            Assert.AreEqual(5, save.Lives(md, T0 + TimeSpan.FromSeconds(150).Ticks));
            Assert.AreEqual(TimeSpan.Zero, save.NextLifeIn(md, T0 + TimeSpan.FromSeconds(150).Ticks));

            long later = T0 + TimeSpan.FromHours(1).Ticks;
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(save.SpendLife(md, later));
            }

            Assert.IsFalse(save.SpendLife(md, later), "no life left to spend");
        }

        [Test]
        public void A_win_pays_coins_and_the_first_win_of_a_level_a_star()
        {
            var md = Md();
            var save = new ClubSave();
            var first = save.Win(md, 1, 4);
            Assert.AreEqual((1, 90), (first.stars, first.coins), "50 + 10 per move left");
            Assert.AreEqual(2, save.level);
            var replay = save.Win(md, 1, 0);
            Assert.AreEqual((0, 50), (replay.stars, replay.coins), "replays earn coins, not stars");
            Assert.AreEqual((1, 140, 2), (save.stars, save.coins, save.level));
        }

        [Test]
        public void Renovation_spends_stars_and_finishing_an_area_pays_gems_once()
        {
            var md = Md();
            var save = new ClubSave { stars = 6 };
            for (int i = 1; i <= 5; i++)
            {
                var step = save.TryBuild(md, $"a{i}");
                Assert.IsTrue(step.built);
                Assert.IsNull(step.completedArea);
            }

            Assert.IsFalse(save.TryBuild(md, "a1").built, "already built");
            var last = save.TryBuild(md, "a6");
            Assert.AreEqual("Shed", last.completedArea.name);
            Assert.AreEqual((300, 300), (last.gems, save.gems));
            Assert.AreEqual("Yard", save.CurrentArea(md).name);
            Assert.IsFalse(save.TryBuild(md, "b1").built, "no stars left");
        }

        [Test]
        public void The_free_rider_squad_slots_and_recruit_open_with_progress()
        {
            var md = Md();
            var save = new ClubSave();
            save.StartIfNew(md);
            Assert.IsNull(save.TryGiveFreeRider(md));
            Assert.AreEqual(0, save.SquadSlots(md));

            save.level = 4;
            var gift = save.TryGiveFreeRider(md).Value;
            Assert.AreEqual(("r1", true), (gift.riderId, gift.isNew));
            CollectionAssert.AreEqual(new[] { "r1" }, save.squad);
            Assert.AreEqual(1, save.SquadSlots(md));
            Assert.IsNull(save.TryGiveFreeRider(md), "only once");

            var roller = new GachaRoller(new SeededRandom(3));
            Assert.IsNull(save.TryPull(md, md.Banners[0], 1, roller), "Recruit opens after level 5");
            save.level = 6;
            Assert.AreEqual(2, save.SquadSlots(md));
            Assert.IsNull(save.TryPull(md, md.Banners[0], 10, roller), "500 gems don't cover a 10-pull");
            save.gems = 1000;
            var ten = save.TryPull(md, md.Banners[0], 10, roller);
            Assert.AreEqual(10, ten.Count);
            Assert.AreEqual(0, save.gems);
            Assert.AreEqual(10, ten.Count(o => o.grant.isNew) + ten.Count(o => o.grant.shards > 0), "each pull is a new rider or shards");
        }

        [Test]
        public void Boosters_place_their_specials_on_a_settled_board()
        {
            var md = Md();
            var def = LevelDef.Rectangle(8, 8, 5, 20, LevelDef.Collect(0, 30));
            var state = new LevelState(def, 4, null, md.Boosters);
            var specials = Enumerable.Range(0, 8).SelectMany(y => Enumerable.Range(0, 8).Select(x => state.Board[x, y].special))
                .Where(s => s != Special.None).ToList();
            CollectionAssert.AreEquivalent(new[] { Special.RocketH, Special.RocketV, Special.Bomb, Special.Disco }, specials,
                "2 rockets (one each way), a bomb and a disco piece");
            Assert.AreEqual(0, MatchFinder.Find(state.Board).Count, "placing them makes no match");
            Assert.IsTrue(MoveFinder.HasMove(state.Board));
        }

        [Test]
        public void Five_more_moves_carry_a_lost_level_on()
        {
            var state = new LevelState(LevelDef.Rectangle(6, 6, 4, 2, LevelDef.Collect(0, 999)), 1);
            Assert.IsFalse(state.Continue(5), "only a lost level can continue");
            var bot = new Bot(BotKind.Greedy, new SeededRandom(1));
            while (state.Outcome == LevelOutcome.Playing)
            {
                var move = bot.Choose(state).Value;
                state.TryMove(move.a, move.b, new List<BoardEvent>());
            }

            Assert.AreEqual(LevelOutcome.Lost, state.Outcome);
            Assert.IsTrue(state.Continue(5));
            Assert.AreEqual((LevelOutcome.Playing, 5), (state.Outcome, state.MovesLeft));
            var next = bot.Choose(state).Value;
            Assert.IsTrue(state.TryMove(next.a, next.b, new List<BoardEvent>()), "play goes on");
        }

        [Test]
        public void Coins_pay_for_boosters_and_a_continue_only_when_they_cover_it()
        {
            var md = Md();
            var save = new ClubSave { coins = 400 };
            Assert.AreEqual(350, save.BoostersCost(md, new[] { "rockets", "bomb" }));
            Assert.IsTrue(save.TrySpendCoins(350));
            Assert.IsFalse(save.TryBuyContinue(md), "50 coins don't cover +5 moves (300)");
            save.coins = 300;
            Assert.IsTrue(save.TryBuyContinue(md));
            Assert.AreEqual(0, save.coins);
        }

        [Test]
        public void Text_translates_by_its_english_and_falls_back_to_it()
        {
            var md = Md();
            Assert.AreEqual(("Shop", "Cửa hàng", "ショップ"), (md.Translate("Shop", "en"), md.Translate("Shop", "vi"), md.Translate("Shop", "ja")));
            Assert.AreEqual("レベル{0}をプレイ", md.Translate("Play level {0}", "ja"));
            Assert.AreEqual("Not in the table", md.Translate("Not in the table", "ja"), "unknown text stays English");
            Assert.AreEqual("Shop", md.Translate("Shop", "fr"), "unknown language stays English");
        }

        [Test]
        public void Strings_must_be_translated_keep_their_holes_and_appear_once()
        {
            var tables = new Dictionary<string, string>();
            foreach (var table in MasterData.Tables)
            {
                tables[table] = "x\n";
            }

            tables["strings"] = "en,vi,ja\nShop,,ショップ\n\"Play level {0}\",Chơi màn,レベル{0}\nShop,Cửa hàng,ショップ";
            var problems = MasterData.Parse(t => tables[t]).Validate().Where(p => p.StartsWith("strings")).ToList();
            CollectionAssert.AreEquivalent(new[]
            {
                "strings: 'Shop' appears twice",
                "strings: 'Shop' has no vi",
                "strings: 'Play level {0}' in vi must keep the same {0} holes",
            }, problems);
        }

        [Test]
        public void The_first_session_lasts_until_its_last_level_is_won()
        {
            var md = Md();
            var save = new ClubSave();
            var inFirstSession = Enumerable.Range(1, 5).Select(level =>
            {
                save.level = level; // the next level to play
                return save.InFirstSession(md);
            });
            CollectionAssert.AreEqual(new[] { true, true, true, false, false }, inFirstSession.ToList());
            Assert.IsFalse(save.InFirstSession(MasterData.Parse(_ => null)), "no home_after_level: no scripted session");
        }

        [Test]
        public void Daily_login_pays_once_a_day_through_a_seven_day_calendar()
        {
            var md = Md();
            var save = new ClubSave();
            var paid = new List<int>();
            for (int day = 100; day < 109; day += 1)
            {
                paid.Add(save.ClaimDaily(md, day));
                Assert.AreEqual(0, save.ClaimDaily(md, day), $"day {day}: a second claim pays nothing");
            }

            CollectionAssert.AreEqual(new[] { 20, 25, 30, 35, 40, 45, 50, 20, 25 }, paid, "day 8 starts the calendar again");
            Assert.AreEqual(290, save.gems);
            Assert.AreEqual(30, save.ClaimDaily(md, 200), "a gap of days doesn't reset it: the next claim is day 3 (30 gems)");
        }

        [Test]
        public void The_demo_shop_gives_gems()
        {
            var md = Md();
            var save = new ClubSave();
            Assert.AreEqual(100, save.ClaimShopItem(md, "handful"));
            Assert.AreEqual(0, save.ClaimShopItem(md, "unknown"));
            Assert.AreEqual(100, save.gems);
        }

        [Test]
        public void A_rider_moved_to_the_other_squad_slot_swaps_places()
        {
            var md = Md();
            var save = new ClubSave { level = 6 };
            save.Grant(md, "r1");
            save.Grant(md, "s1");
            Assert.IsTrue(save.SetSquad(md, 0, "r1"));
            Assert.IsTrue(save.SetSquad(md, 1, "s1"));
            Assert.IsTrue(save.SetSquad(md, 0, "s1"));
            CollectionAssert.AreEqual(new[] { "s1", "r1" }, save.squad);
            Assert.IsFalse(save.SetSquad(md, 1, "x2"), "not owned");
        }
    }
}
