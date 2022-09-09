using Unity.LEGO.Game;
using UnityEngine;

namespace Unity.LEGO.Gameplay
{
    public class GroundHazard : MonoBehaviour
    {
        void OnTriggerEnter(Collider other) 
        {
            if(other.gameObject.tag == "Player")
            {
                GameOverEvent evt = Events.GameOverEvent;
                evt.Win = false;
                EventManager.Broadcast(evt);
            }
        }
    }
}