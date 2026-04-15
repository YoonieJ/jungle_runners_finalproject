namespace jungle_runners_finalproject;

public sealed class ScoreBooster : Collectible
{
    public int BonusScore { get; set; } = 180;

    // Adds the booster score bonus to the player when collected.
    public override void Collect(Player player)
    {
        base.Collect(player);
        player.Score += BonusScore;
    }
}
