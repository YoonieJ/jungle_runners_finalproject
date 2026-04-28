using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public sealed class Button
{
    public Rectangle Bounds { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsHovered { get; private set; }

    // Updates hover state and reports a pressed click inside the button bounds.
    public bool Update(MouseState mouse)
    {
        IsHovered = Bounds.Contains(mouse.Position);
        return IsHovered && mouse.LeftButton == ButtonState.Pressed;
    }

    // Draws the button once UI rendering is wired in.
    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
