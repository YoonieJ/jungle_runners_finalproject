namespace jungle_runners_finalproject;

public abstract class Item : Entity
{
    public bool IsCollected { get; private set; }

    // Marks the item collected and removes it from active gameplay.
    public virtual void Collect(Player player)
    {
        IsCollected = true;
        IsActive = false;
    }
}
