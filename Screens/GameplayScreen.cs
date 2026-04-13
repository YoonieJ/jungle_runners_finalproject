using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class GameplayScreen : IScreen
{
    public Stage? Stage { get; set; }
    public Player Player { get; } = new();

    public void Update(GameTime gameTime)
    {
        Player.Update(gameTime);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        Player.Draw(spriteBatch);
    }
}
