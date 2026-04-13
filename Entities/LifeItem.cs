namespace jungle_runners_finalproject;

public sealed class LifeItem : Collectible
{
    public int HealAmount { get; set; } = 1;

    public override void Collect(Player player)
    {
        base.Collect(player);
        player.Health.Heal(HealAmount);
    }
}
