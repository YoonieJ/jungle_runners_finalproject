using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public sealed class Projectile : Entity
{
    public Vector2 Velocity { get; set; }
    public int Damage { get; set; } = 1;

    // Moves the projectile by velocity before refreshing its collider.
    public override void Update(GameTime gameTime)
    {
        Position += Velocity * (float)gameTime.ElapsedGameTime.TotalSeconds;
        base.Update(gameTime);
    }
}
