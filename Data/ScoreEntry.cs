using System;

namespace jungle_runners_finalproject;

public sealed class ScoreEntry
{
    public int StageNumber { get; set; }
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
