using UnityEditor;

namespace Unity.LEGO.EditorExt
{
    public class LEGOAssetPostprocessor : AssetPostprocessor
    {
        void OnPreprocessAsset()
        {
            AssetDatabase.importPackageStarted -= OnImportPackageStarted;
            AssetDatabase.importPackageStarted += OnImportPackageStarted;
        }

        void OnImportPackageStarted(string packageName)
        {
            UnityEngine.Debug.Log("OnImportPackageStarted > " + packageName);
            if (packageName.ToLower().Contains("microasset")) { return; } //[TODO] find a better way to exclude all microgames packages coming from the asset tool
            if (packageName.ToLower().Contains("lego")) { return; } //[TODO] find a better way to recognize official LEGO packages
            ShowWarningImportDialog(packageName);
        }

        static bool ShowWarningImportDialog(string packageName)
        {
            string title = "Warning";
            string message = string.Format("The package {0} is not an official LEGO package. Publishing a game with those assets might break the Terms of Service", packageName);
            string yesButtonText = "I understand";

            return EditorUtility.DisplayDialog(title, message, yesButtonText);
        }
    }
}
