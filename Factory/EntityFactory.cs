namespace jungle_runners_finalproject;

public sealed class EntityFactory
{
    // Creates a player with default components and stats.
    public Player CreatePlayer()
    {
        return new Player();
    }

    // Creates a default coin collectible.
    public Coin CreateCoin()
    {
        return new Coin();
    }

    // Creates a default obstacle hazard.
    public Obstacle CreateObstacle()
    {
        return new Obstacle();
    }
}
