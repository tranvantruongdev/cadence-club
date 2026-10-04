using System.Collections.Generic;
using Template.Core.Data;
using Template.Data;
using UnityEditor;

namespace Template.EditorTools.MasterData
{
    /// <summary>Example importer: MasterData/sample_items.csv → Assets/_Project/Data/SampleItems.asset.</summary>
    public sealed class SampleItemImporter : IMasterDataImporter
    {
        private const string AssetPath = "Assets/_Project/Data/SampleItems.asset";

        public string FileName => SampleItemTable.FileName;

        public void Import(CsvTable table, List<string> errors)
        {
            var items = SampleItemTable.Read(table, errors);
            if (errors.Count > 0)
            {
                return;
            }

            var database = MasterDataMenu.LoadOrCreate<SampleItemDatabase>(AssetPath);
            database.Replace(items);
            EditorUtility.SetDirty(database);
        }
    }
}
