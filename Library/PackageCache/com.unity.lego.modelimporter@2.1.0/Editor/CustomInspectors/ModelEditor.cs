// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEditor.Experimental.SceneManagement;

namespace LEGOModelImporter
{
    [CustomEditor(typeof(Model))]
    public class ModelEditor : Editor
    {
        Model model;

        SerializedProperty absoluteFilePathProp;
        SerializedProperty relativeFilePathProp;

        private void OnEnable()
        {
            model = (Model)target;

            absoluteFilePathProp = serializedObject.FindProperty("absoluteFilePath");
            relativeFilePathProp = serializedObject.FindProperty("relativeFilePath");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Relative File Path", model.relativeFilePath, new GUIStyle(EditorStyles.label) { wordWrap = true });
            GUILayout.Space(16);
            EditorGUILayout.LabelField("Absolute File Path", model.absoluteFilePath, new GUIStyle(EditorStyles.label) { wordWrap = true });

            EditorGUILayout.EndVertical();

            GUILayout.Space(16);

            // Check if part of prefab instance and prevent reimport.
            var doReimport = false;
            if (PrefabUtility.IsPartOfAnyPrefab(target))
            {
                EditorGUILayout.HelpBox("You cannot reimport a prefab instance. Please perform reimporting on the prefab itself.", MessageType.Warning);
            }
            else
            {
                if (File.Exists(model.relativeFilePath) || File.Exists(model.absoluteFilePath))
                {
                    doReimport = GUILayout.Button("Reimport");
                    if (doReimport)
                    {
                        Undo.RegisterFullObjectHierarchyUndo(model.gameObject, "Reimport");
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Could not find original file.", MessageType.Warning);
                }

                if (GUILayout.Button("Reimport From New File"))
                {
                    var path = EditorUtility.OpenFilePanelWithFilters("Select model file", "Packages/com.unity.lego.modelimporter/Models", new string[] { "LDraw files", "ldr", "Studio files", "io", "LXFML files", "lxfml", "LXF files", "lxf" });
                    if (path.Length != 0)
                    {
                        Undo.RegisterFullObjectHierarchyUndo(model.gameObject, "Reimport");
                        absoluteFilePathProp.stringValue = path;
                        relativeFilePathProp.stringValue = PathUtils.GetRelativePath(Directory.GetCurrentDirectory(), path);
                        doReimport = true;
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (doReimport)
            {
                Debug.Log("Reimport " + model.relativeFilePath);
                var lxfml = ImportModel.ReadFileLogic(model.relativeFilePath);
                if (lxfml == null)
                {
                    lxfml = ImportModel.ReadFileLogic(model.absoluteFilePath);
                }
                if (lxfml == null)
                {
                    EditorUtility.DisplayDialog("Failed to read model from file", "If you're reading an IO file, please export it as LDR in Studio.\n\nIf you're reading an LXFML or LXF file, make sure that they are using version 5.6 or newer", "Ok");
                }
                else
                {
                    ModelImporter.ReimportModel(lxfml, model);
                }

                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null)
                {
                    EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                }
            }
        }
    }

}