namespace jungle_runners_finalproject;

public sealed class LifeItem : Collectible
{
    public int HealAmount { get; set; } = 1;

    // Heals the player after the life item is collected.
    public override void Collect(Player player)
    {
        base.Collect(player);
        player.Health.Heal(HealAmount);
    }
}
