namespace ArknightsFrontline.Common
{
    public enum TeamId
    {
        Blue = 0,
        Red = 1
    }

    public enum Altitude
    {
        Ground = 0,
        Air = 1
    }

    public enum UnitKind
    {
        Operator = 0,
        Minion = 1,
        Tower = 2
    }

    public enum OperatorType
    {
        Exusiai = 0,
        Eyjafjalla = 1,
        SilverAsh = 2,
        NiuLai = 3
    }

    public enum MatchState
    {
        Preparing = 0,
        Running = 1,
        Finished = 2
    }

    public enum MatchOutcome
    {
        None = 0,
        BlueVictory = 1,
        RedVictory = 2,
        Draw = 3
    }
}
