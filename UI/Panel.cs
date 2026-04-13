using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class Panel
{
    public Rectangle Bounds { get; set; }
    public Color BackgroundColor { get; set; } = new(0, 0, 0, 180);

    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
