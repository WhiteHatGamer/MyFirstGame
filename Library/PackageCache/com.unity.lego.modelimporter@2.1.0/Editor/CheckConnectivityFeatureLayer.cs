// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System.IO;
using UnityEngine;
using UnityEditor;

namespace LEGOModelImporter
{

    public class CheckConnectivityFeatureLayer
    {
        static readonly string connectivityFeatureLayerAttemptPrefsKey = "com.unity.lego.modelimporter.attemptCreatingMissingConnectivityFeatureLayer";

        [InitializeOnLoadMethod]
        static void DoCheckConnectivityFeatureLayer()
        {
            // Do not perform the check when playing.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var existingConnectivityFeatureLayer = LayerMask.NameToLayer(Connection.connectivityFeatureLayerName);
            var attempt = EditorPrefs.GetBool(connectivityFeatureLayerAttemptPrefsKey, true);
            if (existingConnectivityFeatureLayer == -1 && attempt)
            {
                EditorApplication.delayCall += CreateConnectivityLayer;
            }
        }

        static void CreateConnectivityLayer()
        {
            SerializedObject tagManagerObject = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>(Path.Combine("ProjectSettings", "TagManager.asset")));

            if (tagManagerObject == null)
            {
                EditorUtility.DisplayDialog("Connectivity feature layer required by LEGO packages", "Could not set up layer used for connectivity features automatically. Please add a layer called '" + Connection.connectivityFeatureLayerName + "'", "Ok");
                EditorPrefs.SetBool(connectivityFeatureLayerAttemptPrefsKey, false);
                return;
            }

            SerializedProperty layersProp = tagManagerObject.FindProperty("layers");
            if (layersProp == null || !layersProp.isArray)
            {
                EditorUtility.DisplayDialog("Connectivity feature layer required by LEGO packages", "Could not set up layer used for connectivity features automatically. Please add a layer called '" + Connection.connectivityFeatureLayerName + "'", "Ok");
                EditorPrefs.SetBool(connectivityFeatureLayerAttemptPrefsKey, false);
                return;
            }

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(i);
                if (layerProp.stringValue == "")
                {
                    layerProp.stringValue = Connection.connectivityFeatureLayerName;
                    EditorUtility.DisplayDialog("Connectivity feature layer required by LEGO packages", "Set up layer used for connectivity features called '" + Connection.connectivityFeatureLayerName + "' at index " + i, "Ok");
                    break;
                }
            }

            tagManagerObject.ApplyModifiedProperties();
        }
    }

}