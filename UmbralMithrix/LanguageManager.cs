using System.IO;
using UnityEngine;

namespace UmbralMithrix
{
    static class LanguageManager
    {
        public static void Register(string searchFolder, string langFolderName = "lang")
        {
            string langFolderPath = Path.Combine(searchFolder, langFolderName);
            if (Directory.Exists(langFolderPath))
            {
                RoR2.Language.collectLanguageRootFolders += folders =>
                {
                    folders.Add(langFolderPath);
                };
            }
            else
            {
                Log.Error($"Lang folder not found: {langFolderPath}");
            }
        }
    }
}
