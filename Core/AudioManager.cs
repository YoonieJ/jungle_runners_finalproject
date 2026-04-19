using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Content;

namespace jungle_runners_finalproject;

public sealed class AudioManager
{
    public bool IsMuted { get; private set; }
    public float MusicVolume { get; set; } = 0.8f;
    public float EffectsVolume { get; set; } = 0.9f;

    private Song? _startMusic;
    private Song? _easyMusic;
    private Song? _mediumMusic;
    private Song? _hardMusic;

    public void LoadContent(ContentManager Content)
    {
        _startMusic = Content.Load<Song>("sonican-lo-fi-music-loop-sentimental-jazzy-love-473154");
        _easyMusic = Content.Load<Song>("redproductions-african-rhythm-africa-groovy-sport-stomping-music-20622");
        _mediumMusic = Content.Load<Song>("os_music-jungle-percussion-beat-royalty-free-music-482338");
        _hardMusic = Content.Load<Song>("williamhector-cinematic-action-jungle-drums-loop-125bpm-345139");

        MediaPlayer.IsRepeating = true;
        MediaPlayer.Volume = MusicVolume;
    }

    public void PlaySongForLevel(int level)
    {
        if (IsMuted) return;

        Song songToPlay = level switch
        {
            0 => _startMusic,
            1 => _easyMusic,
            2 => _mediumMusic,
            3 => _hardMusic,
            _ => _startMusic
        };

        if (MediaPlayer.Queue.ActiveSong != songToPlay)
        {
            MediaPlayer.Play(songToPlay);
        }
    }

    public void SetMute(bool mute)
    {
        IsMuted = mute;
        MediaPlayer.Volume = IsMuted ? 0f : MusicVolume;
    }

    // Flips the global muted state for future music and sound effects.
    public void ToggleMute()
    {
        SetMute(!IsMuted);
    }

    // Stops all active audio once songs and effects are wired into the manager.
    public void StopAll()
    {
        MediaPlayer.Stop();
    }
}