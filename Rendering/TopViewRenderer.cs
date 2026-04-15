using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class TopViewRenderer : IWorldRenderer
{
    // Draws a world from the overhead grid perspective.
    public void Draw(SpriteBatch spriteBatch, GridWorld world, Rectangle viewport)
    {
        // TODO NEXT: Move DrawTopView/DrawTopGrid logic here so both camera modes share the same world data.
    }
}
