using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class FrontViewRenderer : IWorldRenderer
{
    // Draws a world from the front runner perspective.
    public void Draw(SpriteBatch spriteBatch, GridWorld world, Rectangle viewport)
    {
        // TODO: Move Game1.DrawFrontView/DrawFrontTile here after gameplay rendering is split from Game1.
        // TODO: Support barricade, jump, slide, rope, and expanded run sprites here.
    }
}
