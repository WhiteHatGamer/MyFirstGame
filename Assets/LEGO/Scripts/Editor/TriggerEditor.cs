using UnityEditor;
using UnityEngine;
using Unity.LEGO.Behaviours.Actions;
using Unity.LEGO.Behaviours.Triggers;

namespace Unity.LEGO.EditorExt
{
    [CustomEditor(typeof(Trigger), true)]
    public abstract class TriggerEditor : LEGOBehaviourEditor
    {
        protected Trigger m_Trigger;

        protected SerializedProperty m_RepeatProp;

        SerializedProperty m_TargetProp;
        SerializedProperty m_SpecificTargetActionsProp;

        Action m_FocusedAction = null;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_Trigger = (Trigger)target;

            m_RepeatProp = serializedObject.FindProperty("m_Repeat");
            m_TargetProp = serializedObject.FindProperty("m_Target");
            m_SpecificTargetActionsProp = serializedObject.FindProperty("m_SpecificTargetActions");
        }

        protected virtual void OnSceneGUI()
        {
            if (Event.current.type == EventType.Repaint)
            {
                DrawConnections(m_Trigger, m_Trigger.GetTargetedActions(), true, Color.cyan, m_FocusedAction);
            }
        }

        protected void TargetPropGUI()
        {
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            EditorGUILayout.PropertyField(m_TargetProp);
            if ((Trigger.Target)m_TargetProp.enumValueIndex == Trigger.Target.SpecificActions)
            {
                if (EditorGUILayout.PropertyField(m_SpecificTargetActionsProp, new GUIContent("Specific Actions"), false))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_SpecificTargetActionsProp.FindPropertyRelative("Array.size"));
                    for (var i = 0; i < m_SpecificTargetActionsProp.arraySize; ++i)
                    {
                        GUI.SetNextControlName("Action " + i);
                        EditorGUILayout.PropertyField(m_SpecificTargetActionsProp.GetArrayElementAtIndex(i));
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.EndDisabledGroup();

            var previousFocusedAction = m_FocusedAction;

            // Find the currently focused Action.
            var focusedControlName = GUI.GetNameOfFocusedControl();
            var lastSpace = focusedControlName.LastIndexOf(' ');
            if (focusedControlName.StartsWith("Action") && lastSpace >= 0)
            {
                var index = int.Parse(focusedControlName.Substring(lastSpace + 1));
                if (index < m_SpecificTargetActionsProp.arraySize)
                {
                    m_FocusedAction = (Action)m_SpecificTargetActionsProp.GetArrayElementAtIndex(index).objectReferenceValue;
                }
                else
                {
                    m_FocusedAction = null;
                }
            }
            else
            {
                m_FocusedAction = null;
            }

            if (m_FocusedAction != previousFocusedAction)
            {
                SceneView.RepaintAll();
            }
        }
    }
}
