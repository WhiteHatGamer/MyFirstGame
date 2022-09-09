using UnityEditor;
using UnityEngine;
using Unity.LEGO.Behaviours.Actions;
using System.Linq;
using LEGOModelImporter;
using Unity.LEGO.Behaviours.Triggers;

namespace Unity.LEGO.Tutorials
{
    /// <summary>
    /// 
    /// </summary>
    [CreateAssetMenu(fileName = "DeletionCriteria", menuName = "Tutorials/LEGO/DeletionCriteria")]
    class DeletionCriteria : ScriptableObject
    {
        WinAction winAction;
        public TouchTrigger TouchTrigger { get; private set; }

        public void FindWinBrick()
        {
            winAction = FindObjectsOfType<WinAction>().Where(action => action.CompareTag("TutorialRequirement")).FirstOrDefault();
            if (!winAction)
            {
                Debug.LogError("In order to be completed, this tutorial expects exactly one 'WinAction' brick tagged as 'TutorialRequirement', to which a 'TouchTrigger' brick is connected");
                return;
            }

            TouchTrigger = winAction.GetTargetingTriggers().First() as TouchTrigger;
        }

        public bool HasBrickBeenDeleted()
        {
            if (TouchTrigger)
            {
                Selection.activeObject = TouchTrigger.gameObject;
                return false;
            }
            return true;
        }
    }
}
