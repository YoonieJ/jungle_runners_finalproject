using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace jungle_runners_finalproject;

public partial class Game1 : Game
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const float RunnerX = 210f;
    private const float Gravity = 2200f;
    private const float JumpVelocity = -760f;
    private const float DoubleJumpVelocity = -650f;
    private const int CoinsScoreWeight = 45;
    private const float ScoreBoostDuration = 5f;
    private const float ScoreBoostMultiplier = 10f;
    private const float RunnerFrameTime = 0.14f;
    private const float SlideDuration = 0.55f;
    private const float RopeDuration = 1.8f;
    private const float PlayerStandingVisualHeight = 176f;
    private const float PlayerStandingVisualWidth = 92f;
    private const float PlayerJumpVisualScale = 1.12f;
    private const float PlayerRopeVisualScale = 1.2f;
    private const float PlayerRopeVisualHeight = 360f;
    private const int PlayerRunFrameCount = 8;
    private const byte SpriteTrimAlphaThreshold = 32;
    private static readonly float[] PlayerSlideFrameWeights = [1f, 1f, 2.35f, 1f, 1f, 1f];
    private const float DamageFlashDuration = 0.18f;
    private const float DamageShakeDuration = 0.22f;
    private const float DamageShakeMagnitude = 7f;
    private const int MaxRopeCharges = 2;
    private static readonly Rectangle[] PlayerRunFrameSources =
    [
        new(43, 206, 217, 303),
        new(340, 206, 185, 303),
        new(578, 206, 272, 303),
        new(905, 206, 170, 303),
        new(1152, 206, 177, 303),
        new(1375, 206, 261, 303),
        new(1696, 206, 175, 303),
        new(1941, 206, 183, 303)
    ];
    private static readonly float PlayerDesignedFrameScale = PlayerStandingVisualHeight / PlayerRunFrameSources[0].Height;

    private readonly GraphicsDeviceManager _graphics;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly string _savePath = Path.Combine(AppContext.BaseDirectory, "SaveData", "users.json");

    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private Texture2D _mainMenuBackground = null!;
    private Texture2D[] _stageSelectBackgrounds = [];
    private Texture2D[] _lockedStageSelectBackgrounds = [];
    private Texture2D[] _gameplayBackgrounds = [];
    private Texture2D _coinTexture = null!;
    private Texture2D _extraLifeTexture = null!;
    private Texture2D _shieldTexture = null!;
    private Texture2D _ropeTexture = null!;
    private Texture2D _mysteryBoxTexture = null!;
    private Texture2D _obstacleTexture = null!;
    private Texture2D _scoreBoosterTexture = null!;
    private Texture2D _meteorTexture = null!;
    private Texture2D _stageArrowTexture = null!;
    private Texture2D _bossArrowTexture = null!;
    private Texture2D _bossBodyTexture = null!;
    private Texture2D _bossBoulderTexture = null!;
    private Texture2D _playerRunSheet = null!;
    private Texture2D[] _playerSlideTextures = [];
    private Texture2D[] _playerJumpTextures = [];
    private Texture2D _playerSwingTexture = null!;
    private Rectangle[] _playerRunFrames = [];
    private Rectangle[] _playerSlideFrames = [];
    private Rectangle[] _playerJumpFrames = [];
    private Rectangle _playerSwingSource;
    private SpriteFont _minecraftFont = null!;
    private KeyboardState _previousKeyboard;

    private SaveFile _saveFile = new();
    private UserProfile? _currentUser;
    private GameScreen _screen = GameScreen.MainMenu;
    private Difficulty _selectedDifficulty = Difficulty.Medium;
    private ViewMode _viewMode = ViewMode.Front;
    private bool _soundEnabled = true;
    private bool _awaitingRouteChoice;
    private int _routeChoiceIndex;
    private bool _nextStageUnlockedThisRun;

    private readonly LoginScreen _loginScreen = new();
    private readonly MainMenuScreen _mainMenuScreen = new();
    private readonly StageSelectScreen _stageSelectScreen = new();
    private readonly GameOverScreen _gameOverScreen = new();
    private readonly RunState _runState = new();

    // TODO: Add more stages here, then pair each stage with RPG-style dialogue and route metadata.
    private readonly StageDefinition[] _stages =
    [
        new(1, "Overgrown Gate", "Broken stone, low traps, safer routes.", 1650, 2500, 3350),
        new(2, "Serpent Causeway", "Longer branches and faster hazards.", 2200, 3300, 4550),
        new(3, "Sunken Idol", "Dense relic halls with risky shortcuts.", 2800, 4250, 5800)
    ];
    private readonly StageFactory _stageFactory = new();
    private readonly RowDepthMapper _rowDepthMapper = new();
    private readonly WorldScroller _worldScroller = new();

    private string _typedUserId { get => _loginScreen.UserId; set => _loginScreen.UserId = value; }
    private string _menuMessage { get => _loginScreen.Message; set => _loginScreen.Message = value; }
    private int _mainMenuSelection { get => _mainMenuScreen.SelectedIndex; set => _mainMenuScreen.SelectedIndex = value; }
    private int _selectedStage { get => _stageSelectScreen.SelectedStageIndex; set => _stageSelectScreen.SelectedStageIndex = value; }
    private string _gameOverTitle { get => _gameOverScreen.Title; set => _gameOverScreen.Title = value; }
    private string _gameOverDetail { get => _gameOverScreen.Detail; set => _gameOverScreen.Detail = value; }

    private StageDefinition _activeStage { get => _runState.ActiveStage; set => _runState.ActiveStage = value; }
    private Stage _activeStageData { get => _runState.ActiveStageData; set => _runState.ActiveStageData = value; }
    private List<MapSegment> _segments { get => _runState.Segments; set => _runState.Segments = value; }
    private MapSegment _activeSegment { get => _runState.ActiveSegment; set => _runState.ActiveSegment = value; }
    private float _segmentProgress { get => _runState.SegmentProgress; set => _runState.SegmentProgress = value; }
    private float _distance { get => _runState.Distance; set => _runState.Distance = value; }
    private float _distanceScore { get => _runState.DistanceScore; set => _runState.DistanceScore = value; }
    private float _playerJumpOffset { get => _runState.PlayerJumpOffset; set => _runState.PlayerJumpOffset = value; }
    private float _playerVelocityY { get => _runState.PlayerVelocityY; set => _runState.PlayerVelocityY = value; }
    private bool _canDoubleJump { get => _runState.CanDoubleJump; set => _runState.CanDoubleJump = value; }
    private bool _isDoubleJumping { get => _runState.IsDoubleJumping; set => _runState.IsDoubleJumping = value; }
    private float _slideTimer { get => _runState.SlideTimer; set => _runState.SlideTimer = value; }
    private float _ropeTimer { get => _runState.RopeTimer; set => _runState.RopeTimer = value; }
    private Vector2 _ropeSwingPivot { get => _runState.RopeSwingPivot; set => _runState.RopeSwingPivot = value; }
    private float _runAnimationTimer { get => _runState.RunAnimationTimer; set => _runState.RunAnimationTimer = value; }
    private float _scoreBoostTimer { get => _runState.ScoreBoostTimer; set => _runState.ScoreBoostTimer = value; }
    private float _invulnerableTimer { get => _runState.InvulnerableTimer; set => _runState.InvulnerableTimer = value; }
    private float _damageFlashTimer { get => _runState.DamageFlashTimer; set => _runState.DamageFlashTimer = value; }
    private float _screenShakeTimer { get => _runState.ScreenShakeTimer; set => _runState.ScreenShakeTimer = value; }
    private int _lives { get => _runState.Lives; set => _runState.Lives = value; }
    private int _coins { get => _runState.Coins; set => _runState.Coins = value; }
    private int _boosters { get => _runState.Boosters; set => _runState.Boosters = value; }
    private int _coinScore { get => _runState.CoinScore; set => _runState.CoinScore = value; }
    private int _stageItemShieldCharges { get => _runState.StageItemShieldCharges; set => _runState.StageItemShieldCharges = value; }
    private int _ropeItemCharges { get => _runState.RopeItemCharges; set => _runState.RopeItemCharges = value; }
    private int _score { get => _runState.Score; set => _runState.Score = value; }
    private bool _runWon { get => _runState.RunWon; set => _runState.RunWon = value; }
    private int _playerRow { get => _runState.PlayerRow; set => _runState.PlayerRow = value; }
    private Random _runRandom { get => _runState.Random; set => _runState.Random = value; }
    private HashSet<string> _collectedItemsThisRun => _runState.CollectedItemsThisRun;

    private readonly AudioManager _audioManager = new();

    private FrontViewRenderer _frontViewRenderer = null!;
    private TopViewRenderer _topViewRenderer = null!;

    // Configures the game window, content root, and base MonoGame settings.
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = WindowWidth;
        _graphics.PreferredBackBufferHeight = WindowHeight;
        _graphics.ApplyChanges();

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    // Loads saved user settings and prepares the base MonoGame game state.
    protected override void Initialize()
    {
        LoadSaveFile();
        base.Initialize();
    }

    // Creates the sprite batch and a reusable one-pixel texture used by placeholder drawing.
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _mainMenuBackground = Content.Load<Texture2D>("jungle_runners_main");
        _stageSelectBackgrounds =
        [
            Content.Load<Texture2D>("jungle_runners_main_stage1"),
            Content.Load<Texture2D>("jungle_runners_main_stage2"),
            Content.Load<Texture2D>("jungle_runners_main_stage3")
        ];
        _lockedStageSelectBackgrounds = BuildGrayscaleCopies(_stageSelectBackgrounds);
        _gameplayBackgrounds =
        [
            Content.Load<Texture2D>("jungle_runners_stage1"),
            Content.Load<Texture2D>("jungle_runners_stage2"),
            Content.Load<Texture2D>("jungle_runners_stage3")
        ];
        _coinTexture = Content.Load<Texture2D>("stage_coin");
        _extraLifeTexture = Content.Load<Texture2D>("stage_extra_life");
        _shieldTexture = Content.Load<Texture2D>("stage_shield");
        _ropeTexture = Content.Load<Texture2D>("stage_rope");
        _mysteryBoxTexture = Content.Load<Texture2D>("stage_mystery_box");
        _obstacleTexture = Content.Load<Texture2D>("stage_obstacle");
        _scoreBoosterTexture = Content.Load<Texture2D>("stage_score_booster");
        _meteorTexture = Content.Load<Texture2D>("stage_meteor");
        _stageArrowTexture = Content.Load<Texture2D>("stage_arrow");
        _bossArrowTexture = Content.Load<Texture2D>("boss_arrow");
        _bossBodyTexture = Content.Load<Texture2D>("boss_body");
        _bossBoulderTexture = Content.Load<Texture2D>("boss_boulder");
        _playerRunSheet = Content.Load<Texture2D>("player_adventurer_run");
        _playerSlideTextures =
        [
            Content.Load<Texture2D>("player_slide1"),
            Content.Load<Texture2D>("player_slide2"),
            Content.Load<Texture2D>("player_slide3"),
            Content.Load<Texture2D>("player_slide4"),
            Content.Load<Texture2D>("player_slide5"),
            Content.Load<Texture2D>("player_slide6")
        ];
        _playerJumpTextures =
        [
            Content.Load<Texture2D>("player_jump1"),
            Content.Load<Texture2D>("player_jump2"),
            Content.Load<Texture2D>("player_jump3"),
            Content.Load<Texture2D>("player_jump4"),
            Content.Load<Texture2D>("player_jump5"),
            Content.Load<Texture2D>("player_jump6"),
            Content.Load<Texture2D>("player_jump7"),
            Content.Load<Texture2D>("player_jump8")
        ];
        _playerSwingTexture = Content.Load<Texture2D>("player_swing");
        _playerRunFrames = (Rectangle[])PlayerRunFrameSources.Clone();
        _playerSlideFrames = BuildFullTextureSources(_playerSlideTextures);
        _playerJumpFrames = BuildFullTextureSources(_playerJumpTextures);
        _playerSwingSource = BuildTrimmedTextureSource(_playerSwingTexture);
        _minecraftFont = Content.Load<SpriteFont>("Fonts/Minecraft");

        _audioManager.LoadContent(Content);
        _audioManager.PlaySongForLevel(0);

        _frontViewRenderer = new FrontViewRenderer(
            _rowDepthMapper,
            _pixel,
            _coinTexture,
            _extraLifeTexture,
            _shieldTexture,
            _ropeTexture,
            _mysteryBoxTexture,
            _obstacleTexture,
            _stageArrowTexture,
            _bossArrowTexture);

        _topViewRenderer = new TopViewRenderer(
            _pixel,
            _coinTexture,
            _extraLifeTexture,
            _shieldTexture,
            _ropeTexture,
            _mysteryBoxTexture,
            _obstacleTexture,
            _stageArrowTexture,
            _bossArrowTexture);
    }

    // Routes per-frame input and simulation work to the active prototype screen.
    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();
        float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
        {
            Exit();
        }

        switch (_screen)
        {
            case GameScreen.MainMenu:
                UpdateMainMenu(keyboard, deltaSeconds);
                break;
            case GameScreen.StageSelect:
                UpdateStageSelect(keyboard);
                break;
            case GameScreen.HowToPlay:
                UpdateHowToPlay(keyboard);
                break;
            case GameScreen.Playing:
                UpdatePlaying(keyboard, deltaSeconds);
                break;
            case GameScreen.GameOver:
                UpdateGameOver(keyboard);
                break;
        }

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    // Clears the back buffer and draws whichever screen is currently active.
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(11, 45, 37));
        Vector2 screenShakeOffset = _screen == GameScreen.Playing ? GetScreenShakeOffset() : Vector2.Zero;
        Matrix transformMatrix = _screen == GameScreen.Playing
            ? Matrix.CreateTranslation(screenShakeOffset.X, screenShakeOffset.Y, 0f)
            : Matrix.Identity;

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transformMatrix);

        switch (_screen)
        {
            case GameScreen.MainMenu:
                DrawMainMenu();
                break;
            case GameScreen.StageSelect:
                DrawStageSelect();
                break;
            case GameScreen.HowToPlay:
                DrawHowToPlay();
                break;
            case GameScreen.Playing:
                if (_viewMode == ViewMode.Front)
                {
                    DrawFrontView();
                }
                else
                {
                    DrawTopView();
                }
                DrawBossEncounter();
                DrawHud();
                break;
            case GameScreen.GameOver:
                DrawGameOver();
                break;
        }

        _spriteBatch.End();

        if (_screen == GameScreen.Playing && _damageFlashTimer > 0f)
        {
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            DrawDamageFlash();
            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }
}
