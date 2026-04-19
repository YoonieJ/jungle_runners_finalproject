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
    private const int BoostScoreWeight = 180;
    private const float MenuKeyRepeatInitialDelay = 0.28f;
    private const float MenuKeyRepeatInterval = 0.09f;
    private const float RunnerFrameTime = 0.14f;
    private const float DamageFlashDuration = 0.18f;
    private const float DamageShakeDuration = 0.22f;
    private const float DamageShakeMagnitude = 7f;

    private readonly GraphicsDeviceManager _graphics;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly string _savePath = Path.Combine(AppContext.BaseDirectory, "SaveData", "users.json");

    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private Texture2D _mainMenuBackground = null!;
    private Texture2D[] _stageSelectBackgrounds = [];
    private Texture2D[] _gameplayBackgrounds = [];
    private Texture2D _coinTexture = null!;
    private Texture2D _extraLifeTexture = null!;
    private Texture2D _shieldTexture = null!;
    private Texture2D _mysteryBoxTexture = null!;
    private Texture2D _obstacleTexture = null!;
    private Texture2D _scoreBoosterTexture = null!;
    private Texture2D _meteorTexture = null!;
    private Texture2D _stageArrowTexture = null!;
    private Texture2D _bossArrowTexture = null!;
    private Texture2D _bossBodyTexture = null!;
    private Texture2D _bossBoulderTexture = null!;
    private Texture2D[] _playerRunFrames = [];
    private SpriteFont _minecraftFont = null!;
    private KeyboardState _previousKeyboard;

    private SaveFile _saveFile = new();
    private UserProfile? _currentUser;
    // TODO: Move screen ownership into ScreenManager once LoginScreen, MainMenuScreen,
    // StageSelectScreen, GameplayScreen, and GameOverScreen contain the current prototype behavior.
    private GameScreen _screen = GameScreen.MainMenu;
    private MenuFocus _menuFocus = MenuFocus.UserId;
    private int _mainMenuSelection;
    private int _selectedStage;
    private Difficulty _selectedDifficulty = Difficulty.Medium;
    private ViewMode _viewMode = ViewMode.Front;
    private bool _soundEnabled = true;
    private bool _awaitingRouteChoice;
    private int _routeChoiceIndex;
    private float _menuUpRepeatTimer;
    private float _menuDownRepeatTimer;

    private readonly string[] _mainMenuOptions = ["Start Game", "How to Play", "Sound", "Logout"];
    private readonly StageDefinition[] _stages =
    [
        new(1, "Overgrown Gate", "Broken stone, low traps, safer routes.", 1650, 2500, 3350),
        new(2, "Serpent Causeway", "Longer branches and faster hazards.", 2200, 3300, 4550),
        new(3, "Sunken Idol", "Dense relic halls with risky shortcuts.", 2800, 4250, 5800)
    ];
    private readonly StageFactory _stageFactory = new();
    private readonly RowDepthMapper _rowDepthMapper = new();
    private readonly WorldScroller _worldScroller = new();

    private string _typedUserId = string.Empty;
    private string _menuMessage = "Enter user id, then press Enter.";

    private StageDefinition _activeStage = null!;
    private Stage _activeStageData = new();
    // TODO: Move run state into GameplayScreen/Player/Stage so Game1 only routes update/draw calls.
    private List<MapSegment> _segments = [];
    private MapSegment _activeSegment = null!;
    private float _segmentProgress;
    private float _distance;
    private float _playerJumpOffset;
    private float _playerVelocityY;
    private bool _canDoubleJump;
    private float _slideTimer;
    private float _ropeTimer;
    private float _runAnimationTimer;
    private float _scoreBoostTimer;
    private float _invulnerableTimer;
    private float _damageFlashTimer;
    private float _screenShakeTimer;
    private int _lives;
    private int _coins;
    private int _boosters;
    private int _stageItemShieldCharges;
    private int _ropeItemCharges;
    private int _score;
    private bool _runWon;
    private int _playerRow = Constants.MiddleLayer;
    private string _gameOverTitle = "";
    private string _gameOverDetail = "";
    private Random _runRandom = new(1);
    private readonly HashSet<string> _collectedItemsThisRun = [];

    private readonly AudioManager _audioManager = new();

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
        _gameplayBackgrounds =
        [
            Content.Load<Texture2D>("jungle_runners_stage1"),
            Content.Load<Texture2D>("jungle_runners_stage2"),
            Content.Load<Texture2D>("jungle_runners_stage3")
        ];
        _coinTexture = Content.Load<Texture2D>("stage_coin");
        _extraLifeTexture = Content.Load<Texture2D>("stage_extra_life");
        _shieldTexture = Content.Load<Texture2D>("stage_shield");
        _mysteryBoxTexture = Content.Load<Texture2D>("stage_mystery_box");
        _obstacleTexture = Content.Load<Texture2D>("stage_obstacle");
        _scoreBoosterTexture = Content.Load<Texture2D>("stage_score_booster");
        _meteorTexture = Content.Load<Texture2D>("stage_meteor");
        _stageArrowTexture = Content.Load<Texture2D>("stage_arrow");
        _bossArrowTexture = Content.Load<Texture2D>("boss_arrow");
        _bossBodyTexture = Content.Load<Texture2D>("boss_body");
        _bossBoulderTexture = Content.Load<Texture2D>("boss_boulder");
        _playerRunFrames =
        [
            Content.Load<Texture2D>("player_1"),
            Content.Load<Texture2D>("player_2")
        ];
        _minecraftFont = Content.Load<SpriteFont>("Fonts/Minecraft");

        _audioManager.LoadContent(Content);
        _audioManager.PlaySongForLevel(0);
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
