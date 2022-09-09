﻿// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using UnityEngine;
using UnityEditor;

namespace LEGOModelImporter
{
    public class SceneBrickBuilderSettings : EditorWindow
    {
        const string sceneBrickBuildingSettingsMenuPath = "LEGO Tools/Brick Building Settings";

        //[MenuItem(sceneBrickBuildingSettingsMenuPath, priority = 30)]
        private static void ShowSettingsWindow()
        {
            SceneBrickBuilderSettings settings = (SceneBrickBuilderSettings)EditorWindow.GetWindow(typeof(SceneBrickBuilderSettings));
            settings.Show();
        }

        //[MenuItem(sceneBrickBuildingSettingsMenuPath, validate = true)]
        private static bool ValidateBrickBuildingSettings()
        {
            return !EditorApplication.isPlaying;
        }

        private void OnGUI()
        {
            var snapDistance = EditorGUILayout.FloatField("Sticky Snap Distance", SceneBrickBuilder.GetStickySnapDistance());
            SceneBrickBuilder.SetStickySnapDistance(snapDistance);

            var maxTries = EditorGUILayout.IntSlider("Max Tries Per Brick", SceneBrickBuilder.GetMaxTriesPerBrick(), 1, 20);
            SceneBrickBuilder.SetMaxTriesPerBrick(maxTries);
        }

    }
}