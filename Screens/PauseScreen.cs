using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class PauseScreen : IScreen
{
    public bool ResumeRequested { get; set; }

    // Updates future pause-menu input.
    public void Update(GameTime gameTime)
    {
    }

    // Draws the future pause overlay.
    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
