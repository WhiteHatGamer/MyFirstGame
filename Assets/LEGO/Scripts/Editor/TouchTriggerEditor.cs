using UnityEditor;
using UnityEngine;
using Unity.LEGO.Behaviours.Triggers;

namespace Unity.LEGO.EditorExt
{
    [CustomEditor(typeof(TouchTrigger), true)]
    public class TouchTriggerEditor : SensoryTriggerEditor
    {
        protected override void CreateGUI()
        {
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            EditorGUILayout.PropertyField(m_ScopeProp);
            TargetPropGUI();
            EditorGUILayout.PropertyField(m_SenseProp);
            if ((SensoryTrigger.Sense)m_SenseProp.enumValueIndex == SensoryTrigger.Sense.SpecificBricks)
            {
                EditorGUILayout.PropertyField(m_SpecificSenseBricksProp, new GUIContent("Specific Bricks"));
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(m_RepeatProp);
        }
    }
}
