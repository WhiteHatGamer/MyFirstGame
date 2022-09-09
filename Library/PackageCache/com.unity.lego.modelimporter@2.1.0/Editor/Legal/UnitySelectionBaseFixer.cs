// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LEGOModelImporter
{
    /**
    * This class is a "fake", global, Editor: it jumps into memory once, and sits
    * in the background globally listening to selections, so that it can patch
    * Unity's broken (and officially: we won't fix it) implementation of [SelectionBase] ( https://issuetracker.unity3d.com/issues/selectionbase-attribute-on-parent-gameobject-does-nothing-when-prefab-child-exists?_ga=2.116620868.357796006.1588679946-5671058.1583932696)
    * This version is based on: https://forum.unity.com/threads/in-editor-select-the-parent-instead-of-an-object-in-the-messy-hierarchy-it-creates.543479/#post-5691667
*/
    [InitializeOnLoad]
    public class UnitySelectionBaseFixer : Editor
    {
        static List<UnityEngine.Object> newSelection = null;
        static UnityEngine.Object[] lastSelection = new UnityEngine.Object[] {};
        static readonly Type sceneView = typeof(SceneView);
        static readonly Type hierarchyWindow = Type.GetType("UnityEditor.SceneHierarchyWindow, UnityEditor");

        static UnitySelectionBaseFixer()
        {
            Selection.selectionChanged += OnSelectionChanged;

            // you can't modify selections while in OnSelectionChanged(), so do it in Update() instead
            EditorApplication.update += OnSceneUpdate;
        }

        public static void OnSelectionChanged()
        {
            // Only modify user selection if selected from the SceneView or Hierarchy
            if (!SceneView.mouseOverWindow) { return; }
            Type windowHovered = SceneView.mouseOverWindow.GetType();

            if (!windowHovered.Equals(sceneView) && !windowHovered.Equals(hierarchyWindow)) { return; }

            //  Look through them all, adjusting as needed
            List<UnityEngine.Object> futureSelection = new List<UnityEngine.Object>();
            bool changed = false;
            foreach (GameObject go in Selection.GetFiltered<GameObject>(SelectionMode.Unfiltered))
            {
                changed = changed | AdjustIfNeeded(go, lastSelection, futureSelection);
            }

            if (!changed)
            {
                futureSelection = null;
            }

            newSelection = futureSelection;
            // Remember this selection so we can compare the next selection to it
            lastSelection = Selection.objects;
        }

        static bool AdjustIfNeeded(GameObject go, object[] lastSelection, List<UnityEngine.Object> newSelection)
        {
            GameObject parentWithGlobalSelectionBase = ParentWithGlobalSelectionBase(go);
            if (parentWithGlobalSelectionBase)
            {
                newSelection.Add(parentWithGlobalSelectionBase.gameObject);
                return true;
            }

            newSelection.Add(go);
            return false;
        }

        public static void OnSceneUpdate()
        {
            if (newSelection == null) { return; }

            Selection.objects = newSelection.ToArray();
            newSelection = null;
        }

        public static GameObject ParentWithGlobalSelectionBase(GameObject go)
        {
            if (go.transform.parent == null) { return null; }
            foreach (Component component in go.transform.parent.GetComponentsInParent<MonoBehaviour>(false))
            {
                if (component.GetType().GetCustomAttributes(typeof(SelectionBaseFixed), true).Length > 0)
                {
                    return component.gameObject;
                }
            }
            return null;
        }
    }
}
