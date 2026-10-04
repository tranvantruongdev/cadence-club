using TMPro;
using UnityEditor;
using UnityEngine;

namespace Template.EditorTools.Setup
{
    /// <summary>
    /// TextMeshPro's shaders and default font ship in its "Essential Resources" package. Import them once,
    /// silently, so project setup and CI never wait for the import dialog.
    /// </summary>
    public static class TextSetup
    {
        public const string EssentialsFolder = "Assets/TextMesh Pro";

        public static bool HasEssentials => AssetDatabase.IsValidFolder(EssentialsFolder);

        [MenuItem("Template/Import TextMeshPro Essentials")]
        public static void EnsureEssentials()
        {
            if (HasEssentials)
            {
                return;
            }

            if (Application.isBatchMode)
            {
                // ImportPackage only queues the import in batch mode; the editor would exit first. Use the CLI flag,
                // which imports synchronously. The folder is committed, so this is needed once per repo.
                Debug.LogError("[Template] TextMeshPro Essential Resources are missing. Import them once with: Unity -batchmode -projectPath . " +
                               "-importPackage \"<Library/PackageCache/com.unity.ugui@*>/Package Resources/TMP Essential Resources.unitypackage\" -quit");
                return;
            }

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
            Debug.Log("[Template] Importing TextMeshPro Essential Resources.");
        }
    }
}
