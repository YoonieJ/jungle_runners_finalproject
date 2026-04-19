namespace jungle_runners_finalproject;

public sealed class ScoreBooster : Collectible
{
    public int BonusScore { get; set; } = 180;

    // Legacy entity behavior; the current tile system starts a timed x10 score boost instead.
    // TODO: Replace this flat bonus with the same timed boost effect if entity pickups are re-enabled.
    public override void Collect(Player player)
    {
        base.Collect(player);
        player.Score += BonusScore;
    }
}
