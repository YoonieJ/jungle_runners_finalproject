namespace jungle_runners_finalproject;

public sealed class ScoreBooster : Collectible
{
    public int BonusScore { get; set; } = 180;

    public override void Collect(Player player)
    {
        base.Collect(player);
        player.Score += BonusScore;
    }
}
