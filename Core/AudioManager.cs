using Microsoft.Xna.Framework.Media;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace jungle_runners_finalproject;

public sealed class AudioManager
{
    public bool IsMuted { get; private set; }
    public float MusicVolume { get; set; } = 0.8f;
    public float EffectsVolume { get; set; } = 0.9f;
    private readonly List<SoundEffectInstance> _activeEffects = new();

    // Flips the global muted state for future music and sound effects.
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    // Stops all active audio once songs and effects are wired into the manager.
    public void StopAll()
    {
        MediaPlayer.Stop();
        foreach (var effect in _activeEffects)
        {
            effect.Stop();
            effect.Dispose();
    
        }
        _activeEffects.Clear();
    }

    public void RegisterEffect(SoundEffectInstance effect)
    {
        _activeEffects.Add(effect);
        if (IsMuted)
        {
            effect.Volume = 0f;
        
        }
        else
        {
            effect.Volume = EffectsVolume;
        }
    }
}
