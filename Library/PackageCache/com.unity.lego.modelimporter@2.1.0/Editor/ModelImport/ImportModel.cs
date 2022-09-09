// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace LEGOModelImporter
{

    public class ImportModel : EditorWindow
    {
        static LXFMLDoc lxfml;
        static Model.Pivot pivot;
        static string filePath;
        static Dictionary<int, GroupType> groupType;
        static Dictionary<int, bool> randomizeRotation;
        static Dictionary<int, bool> lightmapped;
        static Dictionary<int, bool> preferLegacy;
        static Dictionary<int, int> lod;
        static Vector2 scrollPosition;

        static Dictionary<string, List<string>> trackedErrors;

        static readonly string[] lodOptions = { "LOD 0", "LOD 1", "LOD 2" };

        [MenuItem("LEGO Tools/Import Model &%l", priority = 0)]
        public static void FindModelFile()
        {
            var path = EditorUtility.OpenFilePanelWithFilters("Select model file", "Packages/com.unity.lego.modelimporter/Models", new string[] { "LDraw files", "ldr", "Studio files", "io", "LXFML files", "lxfml", "LXF files", "lxf" });
            if (path.Length != 0)
            {
                lxfml = ReadFileLogic(path);
                if (lxfml != null)
                {
                    filePath = path;

                    groupType = new Dictionary<int, GroupType>(lxfml.groups.Length);
                    randomizeRotation = new Dictionary<int, bool>(lxfml.groups.Length);
                    lightmapped = new Dictionary<int, bool>(lxfml.groups.Length);
                    preferLegacy = new Dictionary<int, bool>(lxfml.groups.Length);
                    lod = new Dictionary<int, int>(lxfml.groups.Length);
                    foreach (var group in lxfml.groups)
                    {
                        groupType.Add(group.number, GroupType.Static);
                        randomizeRotation.Add(group.number, true);
                        lightmapped.Add(group.number, false);
                        preferLegacy.Add(group.number, false);
                        lod.Add(group.number, 0);
                    }

                    GetWindow<ImportModel>(true, "LEGO Model Importer");
                } else
                {
                    EditorUtility.DisplayDialog("Failed to read model from file", "If you're reading an IO file, please export it as LDR in Studio.\n\nIf you're reading an LXFML or LXF file, make sure that they are using version 5.6 or newer", "Ok");
                }
            }
        }

        private void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);

            pivot = (Model.Pivot)EditorGUILayout.EnumPopup("Pivot", pivot);

            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();

            GUILayout.Label("Groups", new GUIStyle(EditorStyles.boldLabel), new GUILayoutOption[] { GUILayout.Width(250) });

            GUILayout.EndHorizontal();

            var staticOrDynamicWhilePreferringLegacy = false;
            var dynamicOrHiddenWhileLightmapped = false;
            for (int i = 0; i < lxfml.groups.Length; i++)
            {
                GUILayout.BeginHorizontal();

                var number = lxfml.groups[i].number;

                GUILayout.Label(lxfml.groups[i].name, new GUILayoutOption[] { GUILayout.Width(250) });
                groupType[number] = (GroupType)EditorGUILayout.EnumPopup(groupType[number]);

                randomizeRotation[number] = EditorGUILayout.Toggle("Randomize Rotations", randomizeRotation[number]);

                lightmapped[number] = EditorGUILayout.Toggle("Lightmapped", lightmapped[number]);

                preferLegacy[number] = EditorGUILayout.Toggle("Prefer Legacy Parts", preferLegacy[number]);

                lod[number] = EditorGUILayout.Popup("LOD", lod[number], lodOptions);

                if ((groupType[number] == GroupType.Dynamic || groupType[number] == GroupType.Ignore) && lightmapped[number])
                {
                    dynamicOrHiddenWhileLightmapped = true;
                }

                if ((groupType[number] == GroupType.Static || groupType[number] == GroupType.Dynamic) && preferLegacy[number])
                {
                    staticOrDynamicWhilePreferringLegacy = true;
                }

                GUILayout.EndHorizontal();
            }

            if (lxfml.groups.Length > 1)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label("Set All Group Types To", new GUILayoutOption[] { GUILayout.Width(250) });

                foreach (var value in (GroupType[])Enum.GetValues(typeof(GroupType)))
                {
                    if (GUILayout.Button(value.ToString()))
                    {
                        for (int i = 0; i < lxfml.groups.Length; i++)
                        {
                            groupType[lxfml.groups[i].number] = value;
                        }
                    }
                }

                GUILayout.EndHorizontal();

                CreateSetAllBoolsUI("Set All Randomize Rotations To", randomizeRotation);
                CreateSetAllBoolsUI("Set All Lightmapped To", lightmapped);
                CreateSetAllBoolsUI("Set All Prefer Legacy To", preferLegacy);


                GUILayout.BeginHorizontal();

                GUILayout.Label("Set All LODs To", new GUILayoutOption[] { GUILayout.Width(250) });

                for(var i = 0; i <= 2; ++i)
                {
                    if (GUILayout.Button("LOD " + i))
                    {
                        for (int j = 0; j < lxfml.groups.Length; j++)
                        {
                            lod[lxfml.groups[j].number] = i;
                        }
                    }
                }

                GUILayout.EndHorizontal();
            }

            if (dynamicOrHiddenWhileLightmapped)
            {
                EditorGUILayout.HelpBox("Dynamic and ignored groups will not be lightmapped.", MessageType.Warning);
            }

            if (staticOrDynamicWhilePreferringLegacy)
            {
                EditorGUILayout.HelpBox("Legacy parts in static and dynamic groups might not contain colliders or connectivity information.", MessageType.Warning);
            }

            EditorGUILayout.Separator();

            var importPressed = GUILayout.Button("Import Model");
            if (importPressed)
            {
                ModelImporter.InstantiateModel(lxfml, filePath, pivot, groupType, randomizeRotation, lightmapped, preferLegacy, lod);
            }

            EditorGUILayout.Separator();

            // List tracked errors.
            foreach (var trackedError in trackedErrors)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                GUILayout.Label(trackedError.Key, new GUIStyle(EditorStyles.boldLabel) { wordWrap = true });

                foreach (var id in trackedError.Value)
                {
                    GUILayout.Label(id);
                }
                EditorGUILayout.EndVertical();
            }

            GUILayout.EndScrollView();

            if (importPressed)
            {
                this.Close();
            }
        }

        private static void CreateSetAllBoolsUI(string label, Dictionary<int, bool> values)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label(label, new GUILayoutOption[] { GUILayout.Width(250) });

            if (GUILayout.Button("True"))
            {
                for (int i = 0; i < lxfml.groups.Length; i++)
                {
                    values[lxfml.groups[i].number] = true;
                }
            }
            if (GUILayout.Button("False"))
            {
                for (int i = 0; i < lxfml.groups.Length; i++)
                {
                    values[lxfml.groups[i].number] = false;
                }
            }

            GUILayout.EndHorizontal();
        }

        public static LXFMLDoc ReadFileLogic(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();

            // Reset tracked errors.
            trackedErrors = new Dictionary<string, List<string>>();

            XmlDocument doc = null;

            switch (extension)
            {
                case ".lxfml":
                    {
                        doc = new XmlDocument();
                        doc.LoadXml(File.ReadAllText(path));
                        break;
                    }
                case ".lxf":
                    {
                        // Open LXF file.
                        using (var lxfArchive = ZipFile.OpenRead(path))
                        {
                            var entry = lxfArchive.GetEntry("IMAGE100.LXFML");
                            if (entry != null)
                            {
                                doc = new XmlDocument();
                                var lxfmlStream = entry.Open();
                                doc.Load(lxfmlStream);
                                lxfmlStream.Dispose();
                            }
                        }
                        break;
                    }
                case ".ldr":
                    {
                        var ldrStream = new FileStream(path, FileMode.Open);
                        doc = LDrawConverter.ConvertLDrawToLXFML(ldrStream, path);
                        ldrStream.Dispose();
                        trackedErrors = LDrawConverter.GetErrors();
                        break;
                    }
                case ".io":
                    {
                        // Cannot open IO file.

                        break;
                    }
            }

            if (doc != null)
            {
                var lxfml = new LXFMLDoc();

                if (LXFMLReader.ReadLxfml(doc, ref lxfml))
                {
                    if (lxfml.groups == null)
                    {
                        Debug.Log("No groups in " + path + " Creating default group.");
                        CreateDefaultGroup(lxfml);
                    }

                    return lxfml;
                }
            }

            return null;
        }

        private static void CreateDefaultGroup(LXFMLDoc lxfml)
        {
            lxfml.groups = new LXFMLDoc.BrickGroup[] { new LXFMLDoc.BrickGroup() };
            var group = lxfml.groups[0];
            group.name = "Default";
            group.number = 0;

            group.brickRefs = new int[lxfml.bricks.Count];
            for (var i = 0; i < lxfml.bricks.Count; ++i)
            {
                group.brickRefs[i] = lxfml.bricks[i].refId;
                group.bricks.Add(lxfml.bricks[i]);
            }
        }
    }

}