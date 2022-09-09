// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System.IO;
using System;
using UnityEngine;
using UnityEditor;

namespace LEGOMaterials
{
    public static class MaterialUtility
    {
        public static string materialsPath = "Packages/com.unity.lego.materials/Materials";
        public static string biDir = "BI";
        public static string legacyDir = "Legacy";

        public enum MaterialExistence
        {
            None,
            Legacy,
            Current
        }

        // FIXME Remove when colour palette experiments are over.
        //[MenuItem("LEGO Tools/Dev/Use BI Materials")]
        private static void ToggleBI()
        {
            EditorPrefs.SetBool("com.unity.lego.modelimporter.useBI", !EditorPrefs.GetBool("com.unity.lego.modelimporter.useBI", false));
        }

        // FIXME Remove when colour palette experiments are over.
        //[MenuItem("LEGO Tools/Dev/Use BI Materials", true)]
        private static bool ValidateToggeBI()
        {
            Menu.SetChecked("LEGO Tools/Dev/Use BI Materials", EditorPrefs.GetBool("com.unity.lego.modelimporter.useBI", false));
            return true;
        }

        public static MaterialExistence CheckIfMaterialExists(MouldingColour.Id id)
        {
            if (File.Exists(Path.Combine(materialsPath, (int)id + ".mat")))
            {
                return MaterialExistence.Current;
            }

            if (File.Exists(Path.Combine(materialsPath, legacyDir, (int)id + ".mat")))
            {
                return MaterialExistence.Legacy;
            }
// FIXME Remove when colour palette experiments are over.
#if UNITY_EDITOR
            var useBI = EditorPrefs.GetBool("com.unity.lego.modelimporter.useBI");
            if (useBI)
            {
                if (File.Exists(Path.Combine(materialsPath, biDir, (int)id + ".mat")))
                {
                    return MaterialExistence.Current;
                }

                if (File.Exists(Path.Combine(materialsPath, biDir, legacyDir, (int)id + ".mat")))
                {
                    return MaterialExistence.Legacy;
                }
            }
#endif

            return MaterialExistence.None;
        }

        public static MaterialExistence CheckIfMaterialExists(string id)
        {
            try
            {
                return CheckIfMaterialExists((MouldingColour.Id)Enum.Parse(typeof(MouldingColour.Id), id));
            }
            catch
            {
                Debug.LogErrorFormat("Invalid moulding colour id {0}", id);
                return MaterialExistence.None;
            }
        }

        public static MaterialExistence CheckIfMaterialExists(int id)
        {
            return CheckIfMaterialExists(id.ToString());
        }

        public static Material LoadMaterial(MouldingColour.Id id, bool legacy)
        {
// FIXME Remove when colour palette experiments are over.
#if UNITY_EDITOR
            var useBI = EditorPrefs.GetBool("com.unity.lego.modelimporter.useBI");
            if (useBI)
            {
                var biMaterial = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(materialsPath, biDir, legacy ? legacyDir : "", (int)id + ".mat"));
                if (biMaterial)
                {
                    return biMaterial;
                }
            }
#endif

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(materialsPath, legacy ? legacyDir : "", (int)id + ".mat"));
#else
            return null;
#endif
        }

        public static Material LoadMaterial(string id, bool legacy)
        {
            try
            {
                return LoadMaterial((MouldingColour.Id)Enum.Parse(typeof(MouldingColour.Id), id), legacy);
            }
            catch
            {
                Debug.LogErrorFormat("Invalid moulding colour id {0}", id);
                return null;
            }
        }

        public static Material LoadMaterial(int id, bool legacy)
        {
            return LoadMaterial(id.ToString(), legacy);
        }
    }

}