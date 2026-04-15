using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class SpriteComponent
{
    public Texture2D? Texture { get; set; }
    public Rectangle? SourceRectangle { get; set; }
    public Color Tint { get; set; } = Color.White;

    // Draws the configured texture when one is assigned.
    public void Draw(SpriteBatch spriteBatch, Vector2 position)
    {
        if (Texture is null)
        {
            return;
        }

        spriteBatch.Draw(Texture, position, SourceRectangle, Tint);
    }
}
