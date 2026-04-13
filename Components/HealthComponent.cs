using System;

namespace jungle_runners_finalproject;

public sealed class HealthComponent
{
    public HealthComponent(int maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
    }

    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    public void Damage(int amount)
    {
        CurrentHealth = Math.Max(0, CurrentHealth - amount);
    }

    public void Heal(int amount)
    {
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
    }
}
