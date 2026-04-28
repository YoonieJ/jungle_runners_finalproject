namespace jungle_runners_finalproject;

public sealed class Boss : Entity
{
    public HealthComponent Health { get; } = new(8);
    public int ContactDamage { get; set; } = 1;
}
