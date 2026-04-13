using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public interface IWorldRenderer
{
    void Draw(SpriteBatch spriteBatch, GridWorld world, Rectangle viewport);
}
