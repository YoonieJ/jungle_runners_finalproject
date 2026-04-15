using System.Collections.Generic;

namespace jungle_runners_finalproject;

public sealed class UserProfile
{
    public string UserId { get; set; } = string.Empty;
    public int BestScore { get; set; }
    public int Lives { get; set; } = 3;
    public SettingsData Settings { get; set; } = new();
    public Dictionary<int, StageProgress> StageProgress { get; set; } = [];
    public HashSet<string> CollectedItems { get; set; } = [];
    public List<ScoreEntry> Scores { get; set; } = [];
}
