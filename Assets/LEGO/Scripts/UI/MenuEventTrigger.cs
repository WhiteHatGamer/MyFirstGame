using Unity.LEGO.Game;
using UnityEngine;

namespace Unity.LEGO.UI
{
    // A script to trigger a menu event.

    public class MenuEventTrigger : MonoBehaviour
    {
        [SerializeField, Tooltip("The menu event to trigger.")]
        MenuEventAction MenuEventAction = default;

        public void TriggerMenuEvent()
        {
            MenuEvent evt = Events.MenuEvent;
            evt.MenuEventAction = MenuEventAction;
            EventManager.Broadcast(evt);
        }
    }
}
