using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class LoginScreen : IScreen
{
    public string UserId { get; set; } = string.Empty;

    // Updates the future user-id-only login screen.
    public void Update(GameTime gameTime)
    {
        // TODO: Move user id input and profile loading out of Game1.Menu and into this screen.
    }

    // Draws the future user-id login UI.
    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
