namespace jungle_runners_finalproject;

public sealed class WorldScroller
{
    public float OffsetX { get; private set; }
    public float Speed { get; set; } = 260f;

    public void Reset()
    {
        OffsetX = 0f;
    }

    public void Update(float deltaSeconds)
    {
        OffsetX += Speed * deltaSeconds;
    }
}
