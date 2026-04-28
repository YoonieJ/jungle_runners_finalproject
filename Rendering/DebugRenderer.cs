using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class DebugRenderer
{
    public bool Enabled { get; set; }

    // Draws a debug rectangle around a gameplay bounds area when enabled.
    public void DrawBounds(SpriteBatch spriteBatch, Rectangle bounds)
    {
    }
}
