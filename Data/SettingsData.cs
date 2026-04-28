namespace jungle_runners_finalproject;

public sealed class SettingsData
{
    public bool SoundEnabled { get; set; } = true;
    public Difficulty Difficulty { get; set; } = Difficulty.Medium;
    public ViewMode ViewMode { get; set; } = ViewMode.Front;
    public bool ShowDebugGrid { get; set; }
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard
}

public enum ViewMode
{
    Front,
    Top
}
