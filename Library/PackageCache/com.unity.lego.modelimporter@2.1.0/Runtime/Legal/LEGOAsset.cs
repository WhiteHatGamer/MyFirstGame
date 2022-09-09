// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace LEGOModelImporter
{
    /// <summary>
    /// An asset with this script is considered to be a LEGO Asset
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(ScaleChecker))]
    [RequireComponent(typeof(LEGOComponentsEnforcer))]
    [SelectionBaseFixed]
    public class LEGOAsset : MonoBehaviour
    {
        ScaleChecker scaleChecker;
        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        bool performedSetup = false;
#if UNITY_EDITOR
        public static List<LEGOAsset> legoAssets;

        static LEGOAsset()
        {
            legoAssets = new List<LEGOAsset>();
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        public LEGOAsset()
        {
            if (legoAssets.Contains(this)) { return; }
            legoAssets.Add(this);
        }

        void OnDestroy()
        {
            SceneVisibilityManager.pickingChanged -= OnPickingFlagChanged;
            if (!legoAssets.Contains(this)) { return; }
            legoAssets.Remove(this);
        }

        void EditorUpdate()
        {
            if (!performedSetup)
            {
                Setup();
            }
            scaleChecker.EditorUpdate();
        }

        static void OnEditorUpdate()
        {
            for (int i = legoAssets.Count - 1; i >= 0; i--)
            {
                if (legoAssets[i] == null)
                {
                    legoAssets.RemoveAt(i);
                    i--;
                    continue;
                }
                if (UnityEditor.Selection.activeGameObject != legoAssets[i].gameObject) { continue; }
                legoAssets[i].EditorUpdate();
            }
        }

#endif

        void OnValidate()
        {
            if (!scaleChecker)
            {
                scaleChecker = GetComponent<ScaleChecker>();
                if (!scaleChecker)
                {
                    scaleChecker = gameObject.AddComponent<ScaleChecker>();
                }
            }
            scaleChecker.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
            if (!meshRenderer)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
            if (meshRenderer)
            {
                meshRenderer.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
            }
            if (!meshFilter)
            {
                meshFilter = GetComponent<MeshFilter>();
            }
            if (meshFilter)
            {
                meshFilter.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
            }
            hideFlags = HideFlags.NotEditable;
            HideChildren(true);
        }

        void Awake()
        {
            Setup();
        }

        void Setup()
        {
            performedSetup = true;
            HideChildren(true);
        }

        void HideChildren(bool hide)
        {
#if UNITY_EDITOR
            //[NOTE] We could enable those to be 100% sure that nobody can ever touch the flag,
            //but they would make the editor even slower
            //SceneVisibilityManager.pickingChanged -= OnPickingFlagChanged;
            //SceneVisibilityManager.pickingChanged += OnPickingFlagChanged;

            OnPickingFlagChanged();
            CreatePickingMesh();

            HideFlags flagsToUse = hide ? HideFlags.HideInHierarchy : HideFlags.None;
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                transform.GetChild(i).gameObject.hideFlags = flagsToUse;
            }
#endif
        }

#if UNITY_EDITOR
        void OnPickingFlagChanged()
        {
            SceneVisibilityManager.instance.DisablePicking(gameObject, true);
            SceneVisibilityManager.instance.EnablePicking(gameObject, false);
        }

        void CreatePickingMesh()
        {
            if (!GetComponent<MeshRenderer>())
            {
                var brick = GetComponent<Brick>();

                if (brick)
                {
                    var combineInstances = new List<CombineInstance>();

                    // Get all shells from parts and combine them.
                    foreach (var part in brick.parts)
                    {
                        if (part.legacy)
                        {
                            var partRenderer = part.GetComponent<MeshRenderer>();
                            if (partRenderer)
                            {
                                var mesh = partRenderer.GetComponent<MeshFilter>().sharedMesh;
                                var combineInstance = new CombineInstance();
                                combineInstance.mesh = mesh;
                                combineInstance.transform = transform.worldToLocalMatrix * part.transform.localToWorldMatrix;

                                combineInstances.Add(combineInstance);
                            }
                        }
                        else
                        {
                            var shell = part.transform.Find("Shell");
                            if (shell)
                            {
                                var mesh = shell.GetComponent<MeshFilter>().sharedMesh;
                                var combineInstance = new CombineInstance();
                                combineInstance.mesh = mesh;
                                combineInstance.transform = transform.worldToLocalMatrix * shell.localToWorldMatrix;

                                combineInstances.Add(combineInstance);
                            }

                            var colourChangeSurfaces = part.transform.Find("ColourChangeSurfaces");
                            if (colourChangeSurfaces)
                            {
                                foreach (Transform colourChangeSurface in colourChangeSurfaces)
                                {
                                    var mesh = colourChangeSurface.GetComponent<MeshFilter>().sharedMesh;
                                    var combineInstance = new CombineInstance();
                                    combineInstance.mesh = mesh;
                                    combineInstance.transform = transform.worldToLocalMatrix * colourChangeSurface.localToWorldMatrix;

                                    combineInstances.Add(combineInstance);
                                }
                            }
                        }
                    }

                    Mesh combinedMesh = null;
                    if (combineInstances.Count == 1)
                    {
                        // If there is just one mesh, simply use a reference to that rather than combining.
                        // We know that the one mesh will not be transformed, so it's safe to ignore the transform on the CombineInstance.
                        combinedMesh = combineInstances[0].mesh;
                    }
                    else if (combineInstances.Count > 1)
                    {
                        // Otherwise, if there's more than one, create and save a mesh asset (if it does not exist already).
                        // Then reference that mesh asset.
                        if (!PickingMeshUtils.CheckIfPickingMeshExists(name))
                        {
                            var newMesh = new Mesh();
                            newMesh.CombineMeshes(combineInstances.ToArray(), true, true, false);

                            PickingMeshUtils.SavePickingMesh(name, newMesh);
                            combinedMesh = PickingMeshUtils.LoadPickingMesh(name);
                        }
                        else
                        {
                            combinedMesh = PickingMeshUtils.LoadPickingMesh(name);
                        }
                    }
                    else
                    {
                        // If there were no meshes, we assume it is a minifig and use a box mesh.
                        combinedMesh = PickingMeshUtils.LoadMinifigPickingMesh();
                    }

                    var renderer = gameObject.AddComponent<MeshRenderer>();
                    renderer.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.lego.modelimporter/Materials/LEGO_AssetPicking.mat");
                    renderer.sharedMaterial.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
                    var filter = gameObject.AddComponent<MeshFilter>();
                    filter.sharedMesh = combinedMesh;
                    filter.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
                }
            }
        }
#endif
    }
}
