namespace jungle_runners_finalproject;

public sealed class AudioManager
{
    public bool IsMuted { get; private set; }
    public float MusicVolume { get; set; } = 0.8f;
    public float EffectsVolume { get; set; } = 0.9f;

    // Flips the global muted state for future music and sound effects.
    public void ToggleMute()
    {
        SetMute(!IsMuted);
    }

    // Stops all active audio once songs and effects are wired into the manager.
    public void StopAll()
    {
        // TODO: Stop active music and looping effects after SoundEffect/Song instances are wired in.
    }
}
