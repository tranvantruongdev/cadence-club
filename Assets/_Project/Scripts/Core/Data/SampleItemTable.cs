using System;
using System.Collections.Generic;

namespace Template.Core.Data
{
    /// <summary>
    /// Example master-data table. Shows the pattern every game uses: parse rows from CSV,
    /// validate them in pure C# (unit-tested), and let an Editor importer write the asset.
    /// </summary>
    [Serializable]
    public struct SampleItem
    {
        public string id;
        public string name;
        public string rarity;
        public int price;
    }

    public static class SampleItemTable
    {
        public const string FileName = "sample_items.csv";
        private static readonly HashSet<string> Rarities = new HashSet<string>(StringComparer.Ordinal) { "R", "SR", "SSR" };

        /// <summary>Parses and validates all rows; collects every problem instead of stopping at the first.</summary>
        public static List<SampleItem> Read(CsvTable table, List<string> errors)
        {
            var items = new List<SampleItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in table.Rows)
            {
                try
                {
                    var item = new SampleItem
                    {
                        id = row.Get("id"),
                        name = row.Get("name"),
                        rarity = row.Get("rarity"),
                        price = row.GetInt("price"),
                    };

                    if (item.id.Length == 0)
                    {
                        errors.Add($"line {row.LineNumber}: id is empty.");
                        continue;
                    }

                    if (!seen.Add(item.id))
                    {
                        errors.Add($"line {row.LineNumber}: duplicate id '{item.id}'.");
                        continue;
                    }

                    if (!Rarities.Contains(item.rarity))
                    {
                        errors.Add($"line {row.LineNumber}: rarity '{item.rarity}' must be R, SR or SSR.");
                        continue;
                    }

                    if (item.price < 0)
                    {
                        errors.Add($"line {row.LineNumber}: price can't be negative.");
                        continue;
                    }

                    items.Add(item);
                }
                catch (CsvFormatException e)
                {
                    errors.Add(e.Message);
                }
            }

            return items;
        }
    }
}
