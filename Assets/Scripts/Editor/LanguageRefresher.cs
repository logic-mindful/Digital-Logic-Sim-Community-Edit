using System.Linq;
using DLS.Game;
using UnityEditor;

namespace DLS.Editor
{
    // Allows instant refreshing of the language file.
    public class LanguageRefresher : AssetPostprocessor
    {
        static string languageFilePath = "Assets/Resources/Language.json";
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            if (!EditorApplication.isPlaying) return;
            if (importedAssets.Count() < 1) return;
            if (importedAssets[0] == languageFilePath) Language.Refresh();
        }
    }
}