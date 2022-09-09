namespace Unity.LEGO.Game
{
    // The Game Events used across the Game.
    // Anytime there is a need for a new event, it should be added here.

    public static class Events
    {
        public static MenuEvent MenuEvent = new MenuEvent();
        public static OptionsMenuEvent OptionsMenuEvent = new OptionsMenuEvent();
        public static GameStateChangeEvent GameStateChangeEvent = new GameStateChangeEvent();
        public static ObjectiveAdded ObjectiveAddedEvent = new ObjectiveAdded();
        public static GameOverEvent GameOverEvent = new GameOverEvent();
        public static LookSensitivityUpdateEvent LookSensitivityUpdateEvent = new LookSensitivityUpdateEvent();
    }

    // UI Events.
    public class MenuEvent : GameEvent
    {
        public MenuEventAction MenuEventAction = MenuEventAction.Play;
    }

    public class OptionsMenuEvent: GameEvent
    {
        public bool Active;
    }

    // Gameflow Events.
    public class GameStateChangeEvent : GameEvent
    {
        public GameState CurrentGameState;
        public GameState NewGameState;
    }

    // LEGOBehaviour Events.
    public class ObjectiveAdded : GameEvent
    {
        public IObjective Objective;
    }

    public class GameOverEvent : GameEvent
    {
        public bool Win;
    }

    public class LookSensitivityUpdateEvent : GameEvent
    {
        public float Value;
    }
}
