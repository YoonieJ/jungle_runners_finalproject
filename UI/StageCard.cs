using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class StageCard
{
    public Rectangle Bounds { get; set; }
    public StageDefinition Stage { get; set; } = new(1, string.Empty, string.Empty, 0, 0, 0);
    public bool IsSelected { get; set; }

    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
