using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;



namespace GADE_Semester_Two_POE
{
    // ---------------- Enums ----------------
    public enum Direction { Up = 0, Right = 1, Down = 2, Left = 3, None = 4 }
    public enum GameState { InProgress, Complete, GameOver }
    public enum TileType { Empty, Wall, Hero, Exit }

    // ---------------- Position ----------------
    public class Position
    {
        private int x, y;
        public Position(int x, int y) { this.x = x; this.y = y; }
        public int X { get => x; set => x = value; }
        public int Y { get => y; set => y = value; }
    }

    // ---------------- Base Tile ----------------
    public abstract class Tile
    {
        private Position pos;
        protected Tile(Position pos) { this.pos = pos; }
        public int X { get => pos.X; set => pos.X = value; }
        public int Y { get => pos.Y; set => pos.Y = value; }
        public Position Pos { get => pos; set => pos = value; }
        public abstract char Display { get; }
    }

    // ---------------- Tile Implementations ----------------
    public class EmptyTile : Tile
    {
        public EmptyTile(Position pos) : base(pos) { }
        public override char Display => '.';
    }

    public class WallTile : Tile
    {
        public WallTile(Position pos) : base(pos) { }
        public override char Display => '#';
    }

    public class ExitTile : Tile
    {
        public ExitTile(Position pos) : base(pos) { }
        public override char Display => '≡';
    }

    // ---------------- Character Base ----------------
    public abstract class CharacterTile : Tile
    {
        private int hitPoints;
        private int maxHitPoints;
        private int attackPower;
        protected Tile[] vision; // 4 slots (Up, Right, Down, Left)

        protected CharacterTile(Position pos, int hp, int atk) : base(pos)
        {
            hitPoints = hp;
            maxHitPoints = hp;
            attackPower = atk;
            vision = new Tile[4];
        }

        public Tile[] Vision => vision;
        public int HitPoints => hitPoints;
        public int AttackPower => attackPower;
        public bool IsDead => hitPoints <= 0;

        public void UpdateVision(Level lvl)
        {
            Tile SafeGet(int x, int y)
            {
                if (x < 0 || x >= lvl.Width || y < 0 || y >= lvl.Height) return null;
                return lvl.Tiles[x, y];
            }

            vision[(int)Direction.Up] = SafeGet(X, Y - 1);
            vision[(int)Direction.Right] = SafeGet(X + 1, Y);
            vision[(int)Direction.Down] = SafeGet(X, Y + 1);
            vision[(int)Direction.Left] = SafeGet(X - 1, Y);
        }

        public void TakeDamage(int amt)
        {
            hitPoints -= amt;
            if (hitPoints < 0) hitPoints = 0;
        }

        public void Attack(CharacterTile target) => target.TakeDamage(attackPower);
    }

    // ---------------- Hero ----------------
    public class HeroTile : CharacterTile
    {
        public HeroTile(Position pos) : base(pos, 40, 5) { }
        public override char Display => IsDead ? 'X' : '▼';
    }

    // ---------------- Level ----------------
    public class Level
    {
        private Tile[,] tiles;
        private int width, height;
        private HeroTile hero;
        private ExitTile exit;
        private static readonly Random rng = new Random();

        public Tile[,] Tiles => tiles;
        public int Width => width;
        public int Height => height;
        public HeroTile Hero => hero;
        public ExitTile Exit => exit;

        public Level(int width, int height, HeroTile carryOverHero = null)
        {
            this.width = width;
            this.height = height;
            tiles = new Tile[width, height];
            InitialiseTiles();

            // Place hero
            var heroPos = GetRandomEmptyPosition();
            if (carryOverHero == null)
                hero = (HeroTile)CreateTile(TileType.Hero, heroPos);
            else
            {
                carryOverHero.X = heroPos.X; carryOverHero.Y = heroPos.Y;
                hero = carryOverHero;
                tiles[hero.X, hero.Y] = hero;
            }

            // Place exit
            var exitPos = GetRandomEmptyPosition();
            exit = (ExitTile)CreateTile(TileType.Exit, exitPos);

            hero.UpdateVision(this);
        }

        private Tile CreateTile(TileType type, Position pos)
        {
            Tile t;

            switch (type)
            {
                case TileType.Empty:
                    t = new EmptyTile(pos);
                    break;

                case TileType.Wall:
                    t = new WallTile(pos);
                    break;

                case TileType.Hero:
                    t = new HeroTile(pos);
                    break;

                case TileType.Exit:
                    t = new ExitTile(pos);
                    break;

                default:
                    t = new EmptyTile(pos);
                    break;
            }

            // Place into the grid and return it
            tiles[pos.X, pos.Y] = t;
            return t;
        }

        private void InitialiseTiles()
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool isBoundary = (x == 0 || y == 0 || x == width - 1 || y == height - 1);
                    CreateTile(isBoundary ? TileType.Wall : TileType.Empty, new Position(x, y));
                }
            }
        }

        private Position GetRandomEmptyPosition()
        {
            while (true)
            {
                int x = rng.Next(1, width - 1);
                int y = rng.Next(1, height - 1);
                if (tiles[x, y] is EmptyTile) return new Position(x, y);
            }
        }

        public void SwopTiles(Tile a, Tile b)
        {
            int ax = a.X, ay = a.Y, bx = b.X, by = b.Y;
            tiles[ax, ay] = b; tiles[bx, by] = a;
            a.X = bx; a.Y = by; b.X = ax; b.Y = ay;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    sb.Append(tiles[x, y].Display);
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }

    // ---------------- GameEngine ----------------
    public class GameEngine
    {
        private Level currentLevel;
        private int numberOfLevels;
        private int currentLevelIndex = 1;
        private readonly Random rng = new Random();
        private const int MIN_SIZE = 10, MAX_SIZE = 20;
        private GameState state = GameState.InProgress;
        public GameState State => state;

        public GameEngine(int numLevels)
        {
            numberOfLevels = numLevels;
            int w = rng.Next(MIN_SIZE, MAX_SIZE + 1);
            int h = rng.Next(MIN_SIZE, MAX_SIZE + 1);
            currentLevel = new Level(w, h, null);
        }

        private bool MoveHero(Direction dir)
        {
            if (state != GameState.InProgress) return false;

            currentLevel.Hero.UpdateVision(currentLevel);
            Tile target = (dir != Direction.None) ? currentLevel.Hero.Vision[(int)dir] : null;
            if (target == null) return false;

            // ExitTile check
            if (target is ExitTile)
            {
                if (currentLevelIndex >= numberOfLevels)
                {
                    state = GameState.Complete;
                    return false;
                }
                NextLevel();
                return true;
            }

            if (target is EmptyTile)
            {
                currentLevel.SwopTiles(currentLevel.Hero, target);
                currentLevel.Hero.UpdateVision(currentLevel);
                return true;
            }
            return false;
        }

        public bool TriggerMovement(Direction dir) => MoveHero(dir);

        public void NextLevel()
        {
            currentLevelIndex++;
            var hero = currentLevel.Hero;
            int w = rng.Next(MIN_SIZE, MAX_SIZE + 1);
            int h = rng.Next(MIN_SIZE, MAX_SIZE + 1);
            currentLevel = new Level(w, h, hero);
        }

        public override string ToString()
        {
            if (state == GameState.Complete)
                return $"You cleared {currentLevelIndex} levels!\n🎉 Game Complete!";
            return currentLevel.ToString();
        }
    }

    // ---------------- Main Form ----------------
    public class Form1 : Form
    {
        private Label lblDisplay;
        private GameEngine engine;

        public Form1()
        {
            Text = "Text Adventure - Part 1";
            Width = 900; Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            lblDisplay = new Label
            {
                AutoSize = false,
                Font = new Font("Consolas", 14f),
                Location = new Point(20, 20),
                Size = new Size(820, 600),
            };
            Controls.Add(lblDisplay);

            KeyDown += OnKeyDown;

            engine = new GameEngine(10);
            UpdateDisplay();
        }

        private void UpdateDisplay() => lblDisplay.Text = engine.ToString();

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            Direction dir = Direction.None;
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.W) dir = Direction.Up;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) dir = Direction.Right;
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.S) dir = Direction.Down;
            else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) dir = Direction.Left;

            if (dir != Direction.None)
            {
                engine.TriggerMovement(dir);
                UpdateDisplay();
            }
        }
    }
}



