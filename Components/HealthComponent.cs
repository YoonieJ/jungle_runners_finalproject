using System;

namespace jungle_runners_finalproject;

public sealed class HealthComponent
{
    // Initializes current health to the same value as maximum health.
    public HealthComponent(int maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
    }

    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    // Reduces health without allowing it to drop below zero.
    public void Damage(int amount)
    {
        CurrentHealth = Math.Max(0, CurrentHealth - amount);
    }

    // Restores health without allowing it to exceed the maximum.
    public void Heal(int amount)
    {
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
    }
}
