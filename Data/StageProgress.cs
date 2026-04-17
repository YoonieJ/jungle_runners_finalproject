namespace jungle_runners_finalproject;

public sealed class StageProgress
{
    public int StageNumber { get; set; }
    public bool IsUnlocked { get; set; }
    public bool IsCompleted { get; set; }
    public int BestScore { get; set; }
    public int BestDifficulty { get; set; }
    public int StarRating { get; set; }
}
