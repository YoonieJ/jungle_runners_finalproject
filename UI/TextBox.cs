using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class TextBox
{
    public Rectangle Bounds { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsFocused { get; set; }

    // Draws the text box once text input UI rendering is wired in.
    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
