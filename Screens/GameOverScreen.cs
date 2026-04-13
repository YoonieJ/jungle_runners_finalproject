using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class GameOverScreen : IScreen
{
    public string Title { get; set; } = "Game Over";
    public string Detail { get; set; } = string.Empty;

    public void Update(GameTime gameTime)
    {
    }

    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
