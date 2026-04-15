using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class LoginScreen : IScreen
{
    public string UserId { get; set; } = string.Empty;

    // Updates the future user-id-only login screen.
    public void Update(GameTime gameTime)
    {
        // TODO: Move the user id input currently implied by Game1 into this screen.
    }

    // Draws the future user-id login UI.
    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
