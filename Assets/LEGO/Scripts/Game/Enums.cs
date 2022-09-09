namespace Unity.LEGO.Game
{
    // A simple class to get a specific place where we put all sorts of Enums.

    public enum MenuEventAction
    {
        Play,
        ReturnToIntroMenu
    }

    public enum GameState
    {
        Play,
        Win,
        Lose,
        Menu
    }

    public enum ObjectiveProgressType
    {
        None,
        Amount,
        Time
    }
}
