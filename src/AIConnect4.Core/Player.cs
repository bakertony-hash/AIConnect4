namespace AIConnect4.Core;

public enum Player
{
    Red,
    Yellow,
}

public static class PlayerExtensions
{
    public static Player Opponent(this Player player) => player == Player.Red ? Player.Yellow : Player.Red;

    public static char Disc(this Player player) => player == Player.Red ? 'R' : 'Y';
}
