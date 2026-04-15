using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class Hud
{
    public int Lives { get; set; }
    public int Coins { get; set; }
    public int Score { get; set; }

    // Draws the HUD values once the standalone HUD renderer is wired in.
    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
