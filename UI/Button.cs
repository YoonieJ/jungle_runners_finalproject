using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public sealed class Button
{
    public Rectangle Bounds { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsHovered { get; private set; }

    public bool Update(MouseState mouse)
    {
        IsHovered = Bounds.Contains(mouse.Position);
        return IsHovered && mouse.LeftButton == ButtonState.Pressed;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
    }
}
