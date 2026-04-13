namespace jungle_runners_finalproject;

public sealed class Coin : Collectible
{
    public int Value { get; set; } = 1;
    public int ScoreValue { get; set; } = 45;

    public override void Collect(Player player)
    {
        base.Collect(player);
        player.Coins += Value;
        player.Score += ScoreValue;
    }
}
