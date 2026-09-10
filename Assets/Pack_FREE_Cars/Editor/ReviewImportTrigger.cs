using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;


namespace AssetReview.Editor
{
    public class ReviewImportTrigger : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (File.Exists("Assets/DEV_DISABLE_REVIEW_TRIGGER.txt"))
                return;

            foreach (var asset in importedAssets)
            {
                Debug.Log("[ReviewTrigger] Imported: " + asset);
            }

            bool importedFolder = importedAssets.Any(asset =>
                asset.Contains("Pack_FREE_Cars/Editor") && Path.GetExtension(asset) != ".cs");

            if (importedFolder && Directory.Exists("Assets/Pack_FREE_Cars/Editor"))
            {
                Debug.Log("[ReviewTrigger] Triggering popup");
                EditorApplication.delayCall += ReviewWindow.ShowNow;
            }
        }
    }
}
