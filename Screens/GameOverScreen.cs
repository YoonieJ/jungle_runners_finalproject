using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class GameOverScreen : IScreen
{
    public string Title { get; set; } = "Game Over";
    public string Detail { get; set; } = string.Empty;

    // Updates future game-over input and transition behavior.
    public void Update(GameTime gameTime)
    {
        // TODO: Show the current user's round result, scoreboard, stars, and next-stage unlock state.
    }

    // Draws the future standalone game-over screen.
    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
