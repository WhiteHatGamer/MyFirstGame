using Cinemachine;
using System.Collections;
using UnityEngine;

namespace Unity.LEGO.Game
{
    // The Root component for the game.
    // It sets the game state and broadcasts events to notify the different systems of a game state change.

    public class GameFlowManager : MonoBehaviour
    {
        [SerializeField, Tooltip("The delay in seconds between the game is won and the game over scene is loaded.")]
        float m_GameOverWinSceneDelay = 5.0f;

        [SerializeField, Tooltip("The delay in seconds between the game is lost and the game over scene is loaded.")]
        float m_GameOverLoseSceneDelay = 3.0f;

        [SerializeField, Tooltip("The delay in seconds until we activate the controller look inputs")]
        float m_StartGameLockedControllerTimer = 0.3f;

        GameState m_GameState = GameState.Play;

        float m_GameOverSceneTime;

        CinemachineFreeLook m_FreeLookCamera;

        string m_ControllerAxisXName;
        string m_ControllerAxisYName;

        void Awake()
        {
            EventManager.AddListener<GameOverEvent>(OnGameOver);

            m_FreeLookCamera = FindObjectOfType<CinemachineFreeLook>();
#if !UNITY_EDITOR
            Cursor.lockState = CursorLockMode.Locked;
#endif

            // Enable camera depth texture to ensure fog works even without shadows.
            Camera.main.depthTextureMode = DepthTextureMode.Depth;

            // Backup and lock look rotation
            m_ControllerAxisXName = m_FreeLookCamera.m_XAxis.m_InputAxisName;
            m_ControllerAxisYName = m_FreeLookCamera.m_YAxis.m_InputAxisName;
            m_FreeLookCamera.m_XAxis.m_InputAxisName = "";
            m_FreeLookCamera.m_YAxis.m_InputAxisName = "";
        }

        void Start()
        {
            StartCoroutine(StartGameLockLookRotation());
        }

        IEnumerator StartGameLockLookRotation()
        {
            while (m_StartGameLockedControllerTimer > 0.0f)
            {
                m_StartGameLockedControllerTimer -= Time.deltaTime;
                if (m_StartGameLockedControllerTimer < 0.0f)
                {
                    m_FreeLookCamera.m_XAxis.m_InputAxisName = m_ControllerAxisXName;
                    m_FreeLookCamera.m_YAxis.m_InputAxisName = m_ControllerAxisYName;
                }
                yield return new WaitForEndOfFrame();
            }
        }

        void Update()
        {
            if (m_GameState != GameState.Play)
            {
                if (Time.time >= m_GameOverSceneTime)
                {
                    SetGameState(GameState.Menu);
                }
            }
        }

        void OnGameOver(GameOverEvent evt)
        {
            if (m_GameState == GameState.Play)
            {
                // Stop colliding the camera.
                var cinemachineCollider = m_FreeLookCamera.GetComponent<CinemachineCollider>();
                if (cinemachineCollider)
                {
                    cinemachineCollider.enabled = false;
                }

                // Set game state and handle the camera accordingly.
                if (evt.Win)
                {
                    m_GameOverSceneTime = Time.time + m_GameOverWinSceneDelay;

                    SetGameState(GameState.Win);

                    // Zoom in on the player.
                    StartCoroutine(ZoomInOnPlayer());
                }
                else
                {
                    m_GameOverSceneTime = Time.time + m_GameOverLoseSceneDelay;

                    SetGameState(GameState.Lose);

                    // Stop following the player.
                    m_FreeLookCamera.Follow = null;
                }
            }
        }

        void SetGameState(GameState newGameState)
        {
#if !UNITY_EDITOR
            Cursor.lockState = newGameState != GameState.Play ? CursorLockMode.None : CursorLockMode.Locked;
#endif

            // Broadcast game state change,
            GameStateChangeEvent evt = Events.GameStateChangeEvent;
            evt.CurrentGameState = m_GameState;
            evt.NewGameState = newGameState;
            EventManager.Broadcast(evt);

            m_GameState = newGameState;
        }

        void OnDestroy()
        {
            EventManager.RemoveListener<GameOverEvent>(OnGameOver);
        }

        IEnumerator ZoomInOnPlayer()
        {
            // Disable controller look rotation
            m_FreeLookCamera.m_XAxis.m_InputAxisValue = 0.0f;
            m_FreeLookCamera.m_YAxis.m_InputAxisValue = 0.0f;
            m_FreeLookCamera.m_XAxis.m_InputAxisName = "";
            m_FreeLookCamera.m_YAxis.m_InputAxisName = "";

            // Backup Middle Rig Zoom Factor 
            var zoomFactor = 1.0f;
            float middleRigZoomFactor = m_FreeLookCamera.m_Orbits[1].m_Radius;

            while (zoomFactor > 0.3f)
            {
                m_FreeLookCamera.m_YAxis.Value = Mathf.Lerp(m_FreeLookCamera.m_YAxis.Value, 0.6f, 3.0f * Time.deltaTime);    // Ensure the vertical axis reset to a reasonable value (0.6 is the default prefab value) with a simple lerp

                zoomFactor -= 0.1f * Time.deltaTime;
                m_FreeLookCamera.m_Orbits[1].m_Radius = middleRigZoomFactor * zoomFactor;

                yield return new WaitForEndOfFrame();
            }
        }
    }
}
