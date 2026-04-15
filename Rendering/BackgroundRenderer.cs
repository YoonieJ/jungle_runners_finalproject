using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class BackgroundRenderer
{
    public Color ClearColor { get; set; } = new(11, 45, 37);

    // Draws the stage background once background art is added.
    public void Draw(SpriteBatch spriteBatch, Rectangle viewport)
    {
    }
}
