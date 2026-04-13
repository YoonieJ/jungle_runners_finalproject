using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public abstract class Entity
{
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; } = new(48f, 48f);
    public bool IsActive { get; set; } = true;
    public SpriteComponent Sprite { get; } = new();
    public ColliderComponent Collider { get; } = new();

    public Rectangle Bounds => new(
        (int)Position.X,
        (int)Position.Y,
        (int)Size.X,
        (int)Size.Y);

    public virtual void Update(GameTime gameTime)
    {
        Collider.Bounds = Bounds;
    }

    public virtual void Draw(SpriteBatch spriteBatch)
    {
        Sprite.Draw(spriteBatch, Position);
    }
}
