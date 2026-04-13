using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public sealed class MovementComponent
{
    public Vector2 Velocity { get; set; }
    public Vector2 Acceleration { get; set; }
    public float MaxSpeed { get; set; } = 420f;

    public void ApplyAcceleration(float deltaSeconds)
    {
        Velocity += Acceleration * deltaSeconds;

        if (Velocity.LengthSquared() > MaxSpeed * MaxSpeed)
        {
            Velocity = Vector2.Normalize(Velocity) * MaxSpeed;
        }
    }
}
