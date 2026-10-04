using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Template.Core.Data;
using UnityEditor;
using UnityEngine;

namespace Template.EditorTools.MasterData
{
    /// <summary>
    /// One importer per CSV in /MasterData. Parse and validate in Core (unit-tested),
    /// then write a ScriptableObject here. Importers are found automatically.
    /// </summary>
    public interface IMasterDataImporter
    {
        string FileName { get; }

        /// <summary>Add every problem to <paramref name="errors"/>; the asset is only written when there are none.</summary>
        void Import(CsvTable table, List<string> errors);
    }

    public static class MasterDataMenu
    {
        /// <summary>CSV source of truth, outside Assets so designers can edit it with any tool.</summary>
        public const string SourceFolder = "MasterData";

        [MenuItem("Template/Import Master Data", priority = 20)]
        public static void ImportAll()
        {
            var importers = TypeCache.GetTypesDerivedFrom<IMasterDataImporter>()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Select(t => (IMasterDataImporter)Activator.CreateInstance(t))
                .ToList();

            int failed = 0;
            foreach (var importer in importers)
            {
                string path = Path.Combine(SourceFolder, importer.FileName);
                var errors = new List<string>();
                if (!File.Exists(path))
                {
                    errors.Add($"missing file {path}");
                }
                else
                {
                    try
                    {
                        importer.Import(CsvTable.Parse(File.ReadAllText(path), importer.FileName), errors);
                    }
                    catch (CsvFormatException e)
                    {
                        errors.Add(e.Message);
                    }
                }

                foreach (string error in errors)
                {
                    Debug.LogError($"[MasterData] {importer.FileName}: {error}");
                }

                if (errors.Count > 0)
                {
                    failed++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(failed == 0
                ? $"[MasterData] Imported {importers.Count} table(s)."
                : $"[MasterData] {failed} of {importers.Count} table(s) have errors; their assets were not updated.");
        }

        /// <summary>Loads the asset at <paramref name="assetPath"/>, creating it (and its folder) if needed.</summary>
        public static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath) ?? "Assets");
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }
    }
}
