using UnityEditor;
using UnityEngine;
using Unity.LEGO.Behaviours.Triggers;

namespace Unity.LEGO.EditorExt
{
    [CustomEditor(typeof(SensoryTrigger), true)]
    public abstract class SensoryTriggerEditor : TriggerEditor
    {
        protected SerializedProperty m_SenseProp;
        protected SerializedProperty m_SpecificSenseBricksProp;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_SenseProp = serializedObject.FindProperty("m_Sense");
            m_SpecificSenseBricksProp = serializedObject.FindProperty("m_SpecificSenseBricks");
        }
    }
}
