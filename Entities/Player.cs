using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public sealed class Player : Entity
{
    public PlayerState State { get; set; } = PlayerState.Running;
    public MovementComponent Movement { get; } = new();
    public HealthComponent Health { get; } = new(3);
    public int Coins { get; set; }
    public int Score { get; set; }

    // Moves the player using its velocity before refreshing base entity state.
    public override void Update(GameTime gameTime)
    {
        Position += Movement.Velocity * (float)gameTime.ElapsedGameTime.TotalSeconds;
        base.Update(gameTime);
    }
}
