using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public partial class Game1 : Game
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const float SegmentWidth = 1200f;
    private const float RunnerX = 210f;
    private const float Gravity = 2200f;
    private const float JumpVelocity = -760f;
    private const float DoubleJumpVelocity = -650f;
    private const float ForegroundGroundY = 610f;
    private const int CoinsScoreWeight = 45;
    private const int BoostScoreWeight = 180;

    private readonly GraphicsDeviceManager _graphics;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly string _savePath = Path.Combine(AppContext.BaseDirectory, "SaveData", "users.json");

    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private KeyboardState _previousKeyboard;

    private SaveFile _saveFile = new();
    private UserProfile? _currentUser;
    // TODO NEXT: Move screen ownership into ScreenManager once LoginScreen, MainMenuScreen,
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

    private readonly string[] _mainMenuOptions = ["Start Game", "Settings", "Logout"];
    private readonly StageDefinition[] _stages =
    [
        new(1, "Overgrown Gate", "Broken stone, low traps, safer routes.", 1650, 2500, 3350),
        new(2, "Serpent Causeway", "Longer branches and faster hazards.", 2200, 3300, 4550),
        new(3, "Sunken Idol", "Dense relic halls with risky shortcuts.", 2800, 4250, 5800)
    ];
    private readonly StageFactory _stageFactory = new();
    private readonly RowDepthMapper _rowDepthMapper = new();
    private readonly WorldScroller _worldScroller = new();

    private string _typedUserId = "PLAYER1";
    private string _typedPassword = "0000";
    private string _menuMessage = "Enter a user id and 4 digit password, then press Enter.";

    private StageDefinition _activeStage = null!;
    private Stage _activeStageData = new();
    // TODO NEXT: Move run state into GameplayScreen/Player/Stage so Game1 only routes update/draw calls.
    private List<MapSegment> _segments = [];
    private MapSegment _activeSegment = null!;
    private float _segmentProgress;
    private float _distance;
    private float _playerJumpOffset;
    private float _playerVelocityY;
    private bool _canDoubleJump;
    private float _slideTimer;
    private float _ropeTimer;
    private float _scoreBoostTimer;
    private float _invulnerableTimer;
    private int _lives;
    private int _coins;
    private int _boosters;
    private int _score;
    private bool _runWon;
    private int _playerRow = Constants.MiddleLayer;
    private string _gameOverTitle = "";
    private string _gameOverDetail = "";
    private Random _runRandom = new(1);

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = WindowWidth;
        _graphics.PreferredBackBufferHeight = WindowHeight;
        _graphics.ApplyChanges();

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        LoadSaveFile();
        TryRestoreLastUser();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
        {
            Exit();
        }

        switch (_screen)
        {
            case GameScreen.MainMenu:
                UpdateMainMenu(keyboard);
                break;
            case GameScreen.StageSelect:
                UpdateStageSelect(keyboard);
                break;
            case GameScreen.Playing:
                UpdatePlaying(keyboard, (float)gameTime.ElapsedGameTime.TotalSeconds);
                break;
            case GameScreen.GameOver:
                UpdateGameOver(keyboard);
                break;
        }

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(11, 45, 37));
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        switch (_screen)
        {
            case GameScreen.MainMenu:
                DrawMainMenu();
                break;
            case GameScreen.StageSelect:
                DrawStageSelect();
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
                DrawHud();
                break;
            case GameScreen.GameOver:
                DrawGameOver();
                break;
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }
}
