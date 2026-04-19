using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class TopViewRenderer : IWorldRenderer
{
    // Draws a world from the overhead grid perspective.
    public void Draw(SpriteBatch spriteBatch, GridWorld world, Rectangle viewport)
    {
        // TODO: Move Game1.DrawTopView/DrawTopGrid here so both camera modes share renderer data.
        // TODO: Show route choices, recommended coin paths, and boss-stage warnings in top view.
    }
}
