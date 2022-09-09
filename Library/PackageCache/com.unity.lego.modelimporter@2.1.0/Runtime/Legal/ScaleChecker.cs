// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System.Collections.Generic;
using UnityEngine;

namespace LEGOModelImporter
{
    /// <summary>
    /// Ensures that an object is scaled consistently
    /// </summary>
    [HideInInspector]
    [ExecuteAlways]
    public class ScaleChecker : MonoBehaviour
    {
        /// <summary>
        /// All transforms whose changes will be tracked
        /// </summary>
        Dictionary<Transform, TransformData> trackedTransforms;
        struct TransformData
        {
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public Vector3 localScale;
        }

        Transform myTransform;
        Vector3 originalScale;
        bool alreadyShowedWarning;

#if UNITY_EDITOR

        //[NOTE] Uncomment all this in case you want to re-enable scale checks on children

        //public static List<ScaleChecker> scaleCheckers;
        //static ScaleChecker()
        //{
        //    scaleCheckers = new List<ScaleChecker>();
        //    UnityEditor.EditorApplication.update -= OnEditorUpdate;
        //    UnityEditor.EditorApplication.update += OnEditorUpdate;
        //}

        //public ScaleChecker()
        //{
        //    if (scaleCheckers.Contains(this)) { return; }
        //    scaleCheckers.Add(this);
        //}

        //void OnDestroy()
        //{
        //    if (!scaleCheckers.Contains(this)) { return; }
        //    scaleCheckers.Remove(this);
        //}

        //static void OnEditorUpdate()
        //{
        //    for (int i = scaleCheckers.Count - 1; i >= 0; i--)
        //    {
        //        if (scaleCheckers[i] == null)
        //        {
        //            scaleCheckers.RemoveAt(i);
        //            i--;
        //            continue;
        //        }
        //        /*
        //         * [NOTE] this might become really CPU intensive, consider if you really want it
        //         * enabled even for objects that are not being selected in this moment and whose transform values
        //         * are code-driven.
        //         * Also, it might prevent you from animating objects, since the Animator drives transform values
        //         * (in this case, we could add filters to the animated parts so they're not tracked)
        //         **/
        //        //[TODO] Decide if you want it or not. If so, we should extend it to make it work with children of children aswell
        //        //scaleCheckers[i].ForceDefaultTransformForChildren();
        //    }
        //}

        public void EditorUpdate()
        {
            if (!myTransform)
            {
                Setup();
            }
            EnsureDefaultScale();
        }

        void InitializeTrackedTransforms()
        {
            trackedTransforms = new Dictionary<Transform, TransformData>();
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                TrackTransformOf(transform.GetChild(i));
            }
        }

        void TrackTransformOf(Transform newTransform)
        {
            TransformData transformData = new TransformData();
            transformData.localPosition = newTransform.localPosition;
            transformData.localEulerAngles = newTransform.localEulerAngles;
            transformData.localScale = newTransform.localScale;
            trackedTransforms[newTransform] = transformData;
        }

        public void ForceDefaultTransformForChildren()
        {
            if (trackedTransforms == null)
            {
                InitializeTrackedTransforms();
            }
            int childCount = transform.childCount;
            Transform childTransform;
            for (int i = 0; i < childCount; i++)
            {
                childTransform = transform.GetChild(i);
                if (!childTransform.hasChanged) { continue; }
                if (!trackedTransforms.ContainsKey(childTransform))
                {
                    TrackTransformOf(childTransform);
                }
                childTransform.localPosition = trackedTransforms[childTransform].localPosition;
                childTransform.localEulerAngles = trackedTransforms[childTransform].localEulerAngles;
                childTransform.localScale = trackedTransforms[childTransform].localScale;
            }
        }

#endif

        void Awake()
        {
            Setup();
        }

#if !UNITY_EDITOR
        void Update()
        {
            EnsureDefaultScale();
        }

#endif
        void Setup()
        {
            myTransform = transform;
            alreadyShowedWarning = false;
            originalScale = transform.localScale;
            //InitializeTrackedTransforms(); //[NOTE] Uncomment in case you want to re-enable scale checks on children
        }

        void EnsureDefaultScale()
        {
            if (originalScale == myTransform.localScale)
            {
                return;
            }
            if (!alreadyShowedWarning)
            {
                Debug.LogError("Scaling of LEGO assets is not allowed");
                alreadyShowedWarning = true;
            }
            myTransform.localScale = originalScale;
        }
    }
}
