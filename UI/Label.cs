using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class Label
{
    public string Text { get; set; } = string.Empty;
    public Vector2 Position { get; set; }
    public Color Color { get; set; } = Color.White;

    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
