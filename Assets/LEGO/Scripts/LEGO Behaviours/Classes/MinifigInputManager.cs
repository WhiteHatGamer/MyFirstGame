using System.Collections;
using UnityEngine;
using Unity.LEGO.Game;
using Unity.LEGO.Minifig;

namespace Unity.LEGO.Behaviours
{
    public class MinifigInputManager : MonoBehaviour
    {
        MinifigController m_MinifigController;

        void Awake()
        {
            m_MinifigController = GetComponent<MinifigController>();

            EventManager.AddListener<GameStateChangeEvent>(OnGameStateChangeEvent);
            EventManager.AddListener<OptionsMenuEvent>(OnOptionsMenuEvent);
        }

        void OnGameStateChangeEvent(GameStateChangeEvent evt)
        {
            // Only enable input if game state is play.
            m_MinifigController.SetInputEnabled(evt.NewGameState == GameState.Play);

            // If we have won, turn to the camera and do a little celebration!
            if (evt.CurrentGameState == GameState.Play && evt.NewGameState == GameState.Win)
            {
                m_MinifigController.TurnTo(Camera.main.transform.position);

                var randomCelebration = Random.Range(0, 3);
                switch (randomCelebration)
                {
                    case 0:
                        {
                            m_MinifigController.PlaySpecialAnimation(MinifigController.SpecialAnimation.AirGuitar);
                            break;
                        }
                    case 1:
                        {
                            m_MinifigController.PlaySpecialAnimation(MinifigController.SpecialAnimation.Flexing);
                            break;
                        }
                    case 2:
                        {
                            m_MinifigController.PlaySpecialAnimation(MinifigController.SpecialAnimation.Dance);
                            break;
                        }
                }
            }
        }

        void OnOptionsMenuEvent(OptionsMenuEvent evt)
        {
            // Only enable input if options menu is not active.
            // Delay update by one frame to prevent input the frame the options menu is closed.
            StartCoroutine(DoUpdateInput(!evt.Active));
        }

        IEnumerator DoUpdateInput(bool enabled)
        {
            yield return new WaitForEndOfFrame();

            m_MinifigController.SetInputEnabled(enabled);
        }

        void OnDestroy()
        {
            EventManager.RemoveListener<GameStateChangeEvent>(OnGameStateChangeEvent);
            EventManager.RemoveListener<OptionsMenuEvent>(OnOptionsMenuEvent);
        }
    }
}
