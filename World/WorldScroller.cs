namespace jungle_runners_finalproject;

public sealed class WorldScroller
{
    public float OffsetX { get; private set; }
    public float Speed { get; set; } = 260f;

    // Restarts horizontal scrolling at the beginning of the stage.
    public void Reset()
    {
        OffsetX = 0f;
    }

    // Advances horizontal scroll distance using the current speed.
    public void Update(float deltaSeconds)
    {
        OffsetX += Speed * deltaSeconds;
    }
}
