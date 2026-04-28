using System.Collections.Generic;

namespace jungle_runners_finalproject;

public sealed class SaveData
{
    public Dictionary<string, UserProfile> Users { get; set; } = [];
    public string LastUserId { get; set; } = string.Empty;
}

public sealed class SaveFile
{
    public Dictionary<string, UserProfile> Users { get; set; } = [];
    public string LastUserId { get; set; } = string.Empty;
}
