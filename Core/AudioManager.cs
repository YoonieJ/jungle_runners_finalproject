namespace jungle_runners_finalproject;

public sealed class AudioManager
{
    public bool IsMuted { get; private set; }
    public float MusicVolume { get; set; } = 0.8f;
    public float EffectsVolume { get; set; } = 0.9f;

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    public void StopAll()
    {
        // TODO NEXT: Stop active music and looping effects after SoundEffect/Song instances are wired in.
    }
}
