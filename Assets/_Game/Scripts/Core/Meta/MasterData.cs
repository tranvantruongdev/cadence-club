using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CadenceClub.Core
{
    public enum Rarity
    {
        R,
        SR,
        SSR,
    }

    /// <summary>What a rider does when their charge is full. Amounts come from <see cref="RiderDef.Power"/> by level.</summary>
    public enum PowerKind
    {
        /// <summary>Horizontal rockets through the fullest rows.</summary>
        RowRockets,

        /// <summary>Vertical rockets through the fullest columns.</summary>
        ColumnRockets,

        /// <summary>Hits random crates, ice and chains (random pieces when there are too few).</summary>
        BreakObstacles,

        /// <summary>Turns random plain pieces into rockets and bombs.</summary>
        MakeSpecials,

        /// <summary>Adds moves.</summary>
        ExtraMoves,
    }

    public sealed class RiderDef
    {
        public string id;
        public string name;
        public string role;
        public Rarity rarity;
        public int color;
        public PowerKind power;

        /// <summary>Pieces of the rider's colour to clear for a full charge.</summary>
        public int charge;

        /// <summary>Power amount at levels 1–5.</summary>
        public int[] powerByLevel;

        public int Power(int level) => powerByLevel[Math.Max(1, Math.Min(level, powerByLevel.Length)) - 1];
    }

    public sealed class BannerDef
    {
        public string id;
        public string name;
        public string featured;
        public string rateTable;

        /// <summary>The SSR is guaranteed on this pull at the latest.</summary>
        public int pity;

        /// <summary>Percent of SSR pulls that give the featured rider.</summary>
        public int featuredShare;
    }

    public sealed class AreaDef
    {
        public int id;
        public string name;

        /// <summary>The story beat shown when the area's last task is built.</summary>
        public string story;
    }

    public sealed class TaskDef
    {
        public string id;
        public int area;
        public string name;
        public int stars;
    }

    /// <summary>
    /// The game's tuning tables, read from CSV (Resources/MasterData) and validated. Everything a designer would change
    /// without code lives here: riders, banner, rates, shards, renovation tasks, economy numbers.
    /// </summary>
    public sealed class MasterData
    {
        public static readonly string[] Tables = { "config", "riders", "rate_tables", "banners", "rarities", "rider_levels", "areas", "renovation" };

        public readonly List<RiderDef> Riders = new List<RiderDef>();
        public readonly List<BannerDef> Banners = new List<BannerDef>();
        public readonly Dictionary<string, Dictionary<Rarity, int>> RateTables = new Dictionary<string, Dictionary<Rarity, int>>();
        public readonly Dictionary<Rarity, int> DuplicateShards = new Dictionary<Rarity, int>();

        /// <summary>Shards needed to reach each level, by level (2–5).</summary>
        public readonly Dictionary<int, int> LevelUpShards = new Dictionary<int, int>();

        public readonly List<AreaDef> Areas = new List<AreaDef>();
        public readonly List<TaskDef> Tasks = new List<TaskDef>();
        public readonly Dictionary<string, string> Config = new Dictionary<string, string>();
        private readonly List<string> _problems = new List<string>();

        public RiderDef Rider(string id) => Riders.FirstOrDefault(r => r.id == id);

        public BannerDef Banner(string id) => Banners.FirstOrDefault(b => b.id == id);

        public int Int(string key) => Config.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

        public string Text(string key) => Config.TryGetValue(key, out var v) ? v : "";

        /// <summary>Parses every table through <paramref name="read"/> (table name → CSV text, null if missing).</summary>
        public static MasterData Parse(Func<string, string> read)
        {
            var md = new MasterData();
            foreach (var row in md.Rows(read, "config", "key", "value"))
            {
                md.Config[row["key"]] = row["value"];
            }

            foreach (var row in md.Rows(read, "riders", "id", "name", "role", "rarity", "color", "power", "charge", "power_by_level"))
            {
                md.Riders.Add(new RiderDef
                {
                    id = row["id"],
                    name = row["name"],
                    role = row["role"],
                    rarity = md.ParseEnum<Rarity>(row, "rarity"),
                    color = md.Number(row, "color"),
                    power = md.ParseEnum<PowerKind>(row, "power"),
                    charge = md.Number(row, "charge"),
                    powerByLevel = row["power_by_level"].Split(';').Select(s => md.Number(s.Trim(), $"rider {row["id"]} power_by_level")).ToArray(),
                });
            }

            foreach (var row in md.Rows(read, "rate_tables", "table", "rarity", "weight"))
            {
                if (!md.RateTables.TryGetValue(row["table"], out var weights))
                {
                    md.RateTables[row["table"]] = weights = new Dictionary<Rarity, int>();
                }

                weights[md.ParseEnum<Rarity>(row, "rarity")] = md.Number(row, "weight");
            }

            foreach (var row in md.Rows(read, "banners", "id", "name", "featured", "rate_table", "pity", "featured_share"))
            {
                md.Banners.Add(new BannerDef
                {
                    id = row["id"],
                    name = row["name"],
                    featured = row["featured"],
                    rateTable = row["rate_table"],
                    pity = md.Number(row, "pity"),
                    featuredShare = md.Number(row, "featured_share"),
                });
            }

            foreach (var row in md.Rows(read, "rarities", "rarity", "duplicate_shards"))
            {
                md.DuplicateShards[md.ParseEnum<Rarity>(row, "rarity")] = md.Number(row, "duplicate_shards");
            }

            foreach (var row in md.Rows(read, "rider_levels", "level", "shards"))
            {
                md.LevelUpShards[md.Number(row, "level")] = md.Number(row, "shards");
            }

            foreach (var row in md.Rows(read, "areas", "area", "name", "story"))
            {
                md.Areas.Add(new AreaDef { id = md.Number(row, "area"), name = row["name"], story = row["story"] });
            }

            foreach (var row in md.Rows(read, "renovation", "task", "area", "name", "stars"))
            {
                md.Tasks.Add(new TaskDef { id = row["task"], area = md.Number(row, "area"), name = row["name"], stars = md.Number(row, "stars") });
            }

            return md;
        }

        /// <summary>Everything wrong with the tables; empty when they're fine. Parse problems come first.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>(_problems);
            void Check(bool ok, string problem)
            {
                if (!ok)
                {
                    problems.Add(problem);
                }
            }

            foreach (var key in new[] { "start_gems", "lives_max", "life_seconds", "win_coins", "pull_cost", "ten_pull_cost", "area_gems" })
            {
                Check(Int(key) > 0, $"config: {key} must be a positive number");
            }

            Check(Rider(Text("free_rider")) != null, $"config: free_rider '{Text("free_rider")}' is not a rider");

            foreach (var dupe in Riders.GroupBy(r => r.id).Where(g => g.Count() > 1))
            {
                problems.Add($"riders: id '{dupe.Key}' is used {dupe.Count()} times");
            }

            foreach (var r in Riders)
            {
                Check(r.color >= 0 && r.color <= 5, $"riders: {r.id} has colour {r.color} (0–5)");
                Check(r.charge > 0, $"riders: {r.id} needs a positive charge");
                Check(r.powerByLevel.Length == 5 && r.powerByLevel.All(p => p > 0), $"riders: {r.id} needs 5 positive power_by_level values");
            }

            foreach (var table in RateTables)
            {
                Check(table.Value.Values.Sum() == 100, $"rate_tables: {table.Key} weights add up to {table.Value.Values.Sum()}, not 100");
                foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                {
                    Check(table.Value.ContainsKey(rarity), $"rate_tables: {table.Key} has no {rarity} row");
                    Check(!table.Value.ContainsKey(rarity) || Riders.Any(r => r.rarity == rarity), $"rate_tables: {table.Key} can roll {rarity}, but no rider has it");
                }
            }

            Check(Banners.Count > 0, "banners: no banner");
            foreach (var b in Banners)
            {
                var featured = Rider(b.featured);
                Check(featured != null && featured.rarity == Rarity.SSR, $"banners: {b.id} features '{b.featured}', which must be an SSR rider");
                Check(RateTables.ContainsKey(b.rateTable), $"banners: {b.id} uses unknown rate table '{b.rateTable}'");
                Check(b.pity >= 1 && b.pity <= 100, $"banners: {b.id} pity {b.pity} must be 1–100 (rare items within about 100 pulls)");
                Check(b.featuredShare >= 0 && b.featuredShare <= 100, $"banners: {b.id} featured_share must be 0–100");
            }

            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                Check(DuplicateShards.TryGetValue(rarity, out int s) && s > 0, $"rarities: {rarity} needs duplicate_shards");
            }

            for (int level = 2; level <= 5; level++)
            {
                Check(LevelUpShards.TryGetValue(level, out int s) && s > 0, $"rider_levels: level {level} needs a shard cost");
            }

            foreach (var dupe in Tasks.GroupBy(t => t.id).Where(g => g.Count() > 1))
            {
                problems.Add($"renovation: task '{dupe.Key}' is used {dupe.Count()} times");
            }

            foreach (var t in Tasks)
            {
                Check(Areas.Any(a => a.id == t.area), $"renovation: {t.id} is in unknown area {t.area}");
                Check(t.stars > 0, $"renovation: {t.id} must cost at least 1 star");
            }

            foreach (var a in Areas)
            {
                Check(Tasks.Count(t => t.area == a.id) == 6, $"areas: {a.name} has {Tasks.Count(t => t.area == a.id)} tasks, not 6");
            }

            return problems;
        }

        private IEnumerable<Dictionary<string, string>> Rows(Func<string, string> read, string table, params string[] columns)
        {
            string text = read(table);
            if (text == null)
            {
                _problems.Add($"{table}: file is missing");
                yield break;
            }

            var lines = Csv.Parse(text);
            if (lines.Count == 0)
            {
                _problems.Add($"{table}: file is empty");
                yield break;
            }

            var header = lines[0];
            foreach (var column in columns)
            {
                if (!header.Contains(column))
                {
                    _problems.Add($"{table}: no '{column}' column");
                    yield break;
                }
            }

            for (int i = 1; i < lines.Count; i++)
            {
                var row = new Dictionary<string, string>();
                for (int c = 0; c < header.Count; c++)
                {
                    row[header[c]] = c < lines[i].Count ? lines[i][c] : "";
                }

                yield return row;
            }
        }

        private int Number(Dictionary<string, string> row, string column) => Number(row[column], column);

        private int Number(string text, string what)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            {
                return n;
            }

            _problems.Add($"{what}: '{text}' is not a whole number");
            return 0;
        }

        private T ParseEnum<T>(Dictionary<string, string> row, string column) where T : struct
        {
            if (System.Enum.TryParse(row[column], out T value))
            {
                return value;
            }

            _problems.Add($"{column}: unknown value '{row[column]}'");
            return default;
        }
    }

    /// <summary>Minimal CSV: commas between fields, double quotes around fields that hold commas or quotes ("" inside).</summary>
    public static class Csv
    {
        public static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == ',')
                {
                    row.Add(field.ToString().Trim());
                    field.Clear();
                }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    EndRow();
                }
                else
                {
                    field.Append(c);
                }
            }

            EndRow();
            return rows;

            void EndRow()
            {
                row.Add(field.ToString().Trim());
                field.Clear();
                if (row.Count > 1 || row[0].Length > 0)
                {
                    rows.Add(row);
                }

                row = new List<string>();
            }
        }
    }
}
