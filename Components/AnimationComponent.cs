using System.Collections.Generic;

namespace jungle_runners_finalproject;

public sealed class AnimationComponent
{
    private readonly Dictionary<string, int> _framesByName = new();

    public string CurrentAnimation { get; private set; } = string.Empty;
    public int CurrentFrame { get; private set; }
    public float FrameTime { get; set; } = 0.12f;

    public void Add(string name, int frameCount)
    {
        _framesByName[name] = frameCount;
    }

    public void Play(string name)
    {
        if (CurrentAnimation == name)
        {
            return;
        }

        CurrentAnimation = name;
        CurrentFrame = 0;
    }
}
