using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public sealed class GameplayScreen : IScreen
{
    private const float GameplayTileSpacing = 150f;
    private const float SpawnScreenOffset = 620f;
    private const float PlayerCollisionWidth = 64f;
    private const float JumpVelocity = -760f;
    private const float DoubleJumpVelocity = -650f;
    private const float SlideDuration = 0.55f;
    private const float RopeDuration = 1.8f;
    private const float ScoreBoostDuration = 5f;
    private const float InvulnerableDuration = 1.2f;

    private Stage? _stage;
    private MapSegment? _activeSegment;
    private KeyboardState _previousKeyboard;
    private float _segmentProgress;
    private float _slideTimer;
    private float _ropeTimer;
    private float _scoreBoostTimer;
    private float _invulnerableTimer;
    private int _boosters;
    private int _routeChoiceIndex;
    private bool _hasStarted;

    public Stage? Stage
    {
        get => _stage;
        set
        {
            _stage = value;
            _hasStarted = false;
            IsRunning = false;
            IsComplete = false;
        }
    }

    public Player Player { get; } = new();
    public WorldScroller WorldScroller { get; } = new();
    public int PlayerRow { get; private set; } = Constants.MiddleLayer;
    public float Distance => WorldScroller.OffsetX;
    public bool IsRunning { get; private set; }
    public bool IsComplete { get; private set; }
    public bool RunWon { get; private set; }
    public bool AwaitingRouteChoice { get; private set; }

    // Resets run state and starts the currently assigned stage.
    public void StartRun()
    {
        if (Stage is null)
        {
            throw new InvalidOperationException("A stage must be assigned before starting gameplay.");
        }

        Stage.CurrentNode = Stage.Graph.Start;
        _activeSegment = Stage.CurrentNode?.Segment ?? (Stage.Segments.Count > 0 ? Stage.Segments[0] : null);
        _segmentProgress = 0f;
        _slideTimer = 0f;
        _ropeTimer = 0f;
        _scoreBoostTimer = 0f;
        _invulnerableTimer = 0f;
        _boosters = 0;
        _routeChoiceIndex = 0;
        PlayerRow = _activeSegment?.PreferredRow ?? Constants.MiddleLayer;
        AwaitingRouteChoice = false;
        IsRunning = true;
        IsComplete = false;
        RunWon = false;
        _hasStarted = true;

        WorldScroller.Reset();
        WorldScroller.Speed = Constants.ScrollSpeed;
        Player.Position = new Vector2(Constants.DefaultRunnerX, 0f);
        Player.Size = new Vector2(PlayerCollisionWidth, 88f);
        Player.Movement.Velocity = Vector2.Zero;
        Player.Movement.Acceleration = Vector2.Zero;
        Player.Movement.MaxSpeed = Constants.DefaultGravity;
        Player.Health.Heal(Player.Health.MaxHealth);
        Player.Coins = 0;
        Player.Score = 0;
        Player.State = PlayerState.Running;
    }

    // Assigns a stage and starts it immediately.
    public void StartRun(Stage stage)
    {
        Stage = stage;
        StartRun();
    }

    // Updates gameplay entities once Game1's prototype run loop moves here.
    public void Update(GameTime gameTime)
    {
        if (!_hasStarted && Stage is not null)
        {
            StartRun();
        }

        KeyboardState keyboard = Keyboard.GetState();
        if (IsRunning)
        {
            UpdatePlaying(keyboard, (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        if (!AwaitingRouteChoice)
        {
            Player.Update(gameTime);
        }

        _previousKeyboard = keyboard;
    }

    // Draws gameplay entities once rendering ownership moves out of Game1.
    public void Draw(SpriteBatch spriteBatch)
    {
        Player.Draw(spriteBatch);
    }

    // Runs player input, stage scrolling, interactions, scoring, and completion checks.
    private void UpdatePlaying(KeyboardState keyboard, float deltaSeconds)
    {
        if (Stage is null)
        {
            return;
        }

        if (AwaitingRouteChoice)
        {
            UpdateRouteChoice(keyboard);
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.Left))
        {
            PlayerRow = Math.Max(Constants.FrontLayer, PlayerRow - 1);
        }

        if (IsNewKeyPress(keyboard, Keys.Right))
        {
            PlayerRow = Math.Min(Constants.BackLayer, PlayerRow + 1);
        }

        if (IsNewKeyPress(keyboard, Keys.Space))
        {
            if (IsOnGround)
            {
                Player.Movement.Velocity = new Vector2(0f, JumpVelocity);
                Player.Movement.Acceleration = new Vector2(0f, Constants.DefaultGravity);
                Player.State = PlayerState.Jumping;
            }
            else if (Player.State == PlayerState.Jumping)
            {
                Player.Movement.Velocity = new Vector2(0f, DoubleJumpVelocity);
                Player.State = PlayerState.Climbing;
            }
        }

        if (IsNewKeyPress(keyboard, Keys.Down))
        {
            _slideTimer = SlideDuration;
            Player.State = PlayerState.Sliding;
        }

        if (IsNewKeyPress(keyboard, Keys.R) && _ropeTimer <= 0f)
        {
            _ropeTimer = RopeDuration;
            _scoreBoostTimer = RopeDuration;
            _boosters++;
        }

        UpdatePlayerActionTimers(deltaSeconds);

        WorldScroller.Speed = Constants.ScrollSpeed * (_ropeTimer > 0f ? 1.6f : 1f);
        WorldScroller.Update(deltaSeconds);
        _segmentProgress += WorldScroller.Speed * deltaSeconds;

        ResolveGridInteractions();
        UpdateRouteProgress();

        Player.Score = (int)Distance + Player.Coins * Constants.CoinScoreValue + _boosters * Constants.BoosterScoreValue;

        float stageEnd = SpawnScreenOffset + Stage.World.Columns * GameplayTileSpacing - Constants.DefaultRunnerX;
        if (WorldScroller.OffsetX >= stageEnd)
        {
            CompleteRun(true);
        }
    }

    // Lets the player choose between available route branches.
    private void UpdateRouteChoice(KeyboardState keyboard)
    {
        if (Stage?.CurrentNode is null || Stage.CurrentNode.Next.Count == 0)
        {
            AwaitingRouteChoice = false;
            return;
        }

        int routeCount = Stage.CurrentNode.Next.Count;
        if (IsNewKeyPress(keyboard, Keys.Right))
        {
            _routeChoiceIndex = (_routeChoiceIndex + 1) % routeCount;
        }

        if (IsNewKeyPress(keyboard, Keys.Left))
        {
            _routeChoiceIndex = (_routeChoiceIndex + routeCount - 1) % routeCount;
        }

        if (IsNewKeyPress(keyboard, Keys.Enter))
        {
            AdvanceToRoute(Stage.CurrentNode.Next[_routeChoiceIndex]);
        }
    }

    // Advances automatic routes or pauses for player choice at branch nodes.
    private void UpdateRouteProgress()
    {
        if (Stage?.CurrentNode is null || _activeSegment is null || _segmentProgress < _activeSegment.Length)
        {
            return;
        }

        if (Stage.CurrentNode.Next.Count == 0)
        {
            return;
        }

        if (Stage.CurrentNode.Next.Count == 1)
        {
            AdvanceToRoute(Stage.CurrentNode.Next[0]);
            return;
        }

        AwaitingRouteChoice = true;
        _routeChoiceIndex = 0;
    }

    // Moves the run into the selected graph node and aligns the player with that route.
    private void AdvanceToRoute(StageNode nextNode)
    {
        if (Stage is null)
        {
            return;
        }

        Stage.CurrentNode = nextNode;
        _activeSegment = nextNode.Segment;
        _segmentProgress = 0f;
        PlayerRow = nextNode.Segment.PreferredRow;
        AwaitingRouteChoice = false;
        _routeChoiceIndex = 0;
    }

    // Updates short-lived jump, slide, rope, score boost, and invulnerability timers.
    private void UpdatePlayerActionTimers(float deltaSeconds)
    {
        if (!IsOnGround || Player.Movement.Velocity.Y < 0f)
        {
            Player.Movement.ApplyAcceleration(deltaSeconds);
        }

        if (Player.Position.Y >= 0f && Player.Movement.Velocity.Y > 0f)
        {
            Player.Position = new Vector2(Player.Position.X, 0f);
            Player.Movement.Velocity = Vector2.Zero;
            Player.Movement.Acceleration = Vector2.Zero;
            Player.State = _slideTimer > 0f ? PlayerState.Sliding : PlayerState.Running;
        }

        _slideTimer = Math.Max(0f, _slideTimer - deltaSeconds);
        _ropeTimer = Math.Max(0f, _ropeTimer - deltaSeconds);
        _scoreBoostTimer = Math.Max(0f, _scoreBoostTimer - deltaSeconds);
        _invulnerableTimer = Math.Max(0f, _invulnerableTimer - deltaSeconds);

        if (_slideTimer <= 0f && IsOnGround && Player.State == PlayerState.Sliding)
        {
            Player.State = PlayerState.Running;
        }
    }

    // Checks the runner against nearby tile content and applies pickups or damage.
    private void ResolveGridInteractions()
    {
        if (Stage is null)
        {
            return;
        }

        foreach (Tile tile in Stage.World.AllTiles)
        {
            if (!tile.HasContent || tile.Row != PlayerRow)
            {
                continue;
            }

            float x = GetTileScreenX(tile.Column);
            bool isAtRunner = x > Constants.DefaultRunnerX - PlayerCollisionWidth && x < Constants.DefaultRunnerX + PlayerCollisionWidth;
            if (!isAtRunner)
            {
                continue;
            }

            switch (tile.Content)
            {
                case TileContent.Coin:
                    Player.Coins++;
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.LifeItem:
                    Player.Health.Heal(1);
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.ScoreBooster:
                    _boosters++;
                    _scoreBoostTimer = ScoreBoostDuration;
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.Projectile:
                    if (_slideTimer <= 0f)
                    {
                        DamagePlayer();
                    }
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.Obstacle:
                case TileContent.Boss:
                    if (JumpOffset < 56f)
                    {
                        DamagePlayer();
                    }
                    tile.Content = TileContent.Empty;
                    break;
            }
        }
    }

    // Applies damage, temporary invulnerability, and the transition to game over.
    private void DamagePlayer()
    {
        if (_invulnerableTimer > 0f || _ropeTimer > 0f)
        {
            return;
        }

        Player.Health.Damage(1);
        Player.State = Player.Health.IsDead ? PlayerState.Dead : PlayerState.Hit;
        _invulnerableTimer = InvulnerableDuration;

        if (Player.Health.IsDead)
        {
            CompleteRun(false);
        }
    }

    // Marks the run as finished so callers can switch screens or save results.
    private void CompleteRun(bool won)
    {
        RunWon = won;
        IsRunning = false;
        IsComplete = true;
        Player.Movement.Velocity = Vector2.Zero;
        Player.Movement.Acceleration = Vector2.Zero;
        Player.State = won ? PlayerState.Idle : PlayerState.Dead;
    }

    // Converts a stage grid column into the current screen-space x position.
    private float GetTileScreenX(int column)
    {
        return SpawnScreenOffset + column * GameplayTileSpacing - WorldScroller.OffsetX;
    }

    // Detects a key press on the frame it transitions from up to down.
    private bool IsNewKeyPress(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    }

    private bool IsOnGround => Player.Position.Y >= 0f;
    private float JumpOffset => Math.Max(0f, -Player.Position.Y);
}
