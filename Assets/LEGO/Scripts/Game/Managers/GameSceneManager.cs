using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.LEGO.Game
{
    public class GameSceneManager : MonoBehaviour
    {
        [Tooltip("The Scene to load when the player goes back to the intro menu.")]
        public string IntroScene;

        [Tooltip("The Scene to load when the player wins.")]
        public string WinScene;

        [Tooltip("The Scene to load when the player loses.")]
        public string LoseScene;

        [Tooltip("All the levels of the game.")]
        public List<string> MainLevels;

        static GameSceneManager s_Instance;

        void Awake()
        {
            if (s_Instance == null)
            {
                s_Instance = this;
                DontDestroyOnLoad(gameObject);

                EventManager.AddListener<GameStateChangeEvent>(OnGameStateChange);
                EventManager.AddListener<MenuEvent>(OnUserMenuAction);
            }
            else if (s_Instance != this)
            {
                Destroy(gameObject);
            }
        }

        void OnGameStateChange(GameStateChangeEvent evt)
        {
            if (evt.NewGameState == GameState.Menu)
            {
                switch (evt.CurrentGameState)
                {
                    case GameState.Win:
                        SceneManager.LoadScene(WinScene);
                        break;
                    case GameState.Lose:
                        SceneManager.LoadScene(LoseScene);
                        break;
                    default:
                        SceneManager.LoadScene(IntroScene);
                        break;
                }
            }
        }

        void OnUserMenuAction(MenuEvent evt)
        {
            switch (evt.MenuEventAction)
            {
                case  MenuEventAction.Play:
                    SceneManager.LoadScene(MainLevels[0]);
                    break;
                case MenuEventAction.ReturnToIntroMenu:
                    SceneManager.LoadScene(IntroScene);
                    break;
            }
        }

        void OnDestroy()
        {
            if (s_Instance == this)
            {
                EventManager.RemoveListener<GameStateChangeEvent>(OnGameStateChange);
                EventManager.RemoveListener<MenuEvent>(OnUserMenuAction);
            }
        }
    }
}
