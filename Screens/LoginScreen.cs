using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class LoginScreen : IScreen
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public void Update(GameTime gameTime)
    {
    }

    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
