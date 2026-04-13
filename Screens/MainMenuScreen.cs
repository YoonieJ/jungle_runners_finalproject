using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class MainMenuScreen : IScreen
{
    public int SelectedIndex { get; set; }

    public void Update(GameTime gameTime)
    {
        // TODO NEXT: Move main menu navigation out of Game1.Menu and into this screen.
    }

    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
