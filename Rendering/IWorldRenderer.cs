using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public interface IWorldRenderer
{
    // Draws the supplied grid world into the target viewport.
    void Draw(SpriteBatch spriteBatch, GridWorld world, Rectangle viewport);
}
