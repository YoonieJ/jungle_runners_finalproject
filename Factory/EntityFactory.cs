namespace jungle_runners_finalproject;

public sealed class EntityFactory
{
    public Player CreatePlayer()
    {
        return new Player();
    }

    public Coin CreateCoin()
    {
        return new Coin();
    }

    public Obstacle CreateObstacle()
    {
        return new Obstacle();
    }
}
