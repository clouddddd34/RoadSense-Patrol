using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RoadSensePatrol;

public sealed class GameForm : Form
{
    private enum MenuPage { Home, Rankings, Guide }
    private static readonly Color Navy = Hex("#0F172A"), Panel = Hex("#1E293B"), Border = Hex("#334155");
    private static readonly Color Teal = Hex("#14B8A6"), Cyan = Hex("#06B6D4"), Amber = Hex("#EAB308");
    private static readonly Color Red = Hex("#EF4444"), Green = Hex("#10B981"), White = Hex("#F8FAFC"), Muted = Hex("#CBD5E1");
    private readonly GameConfig config = GameConfig.Load();
    private readonly GameModel game;
    private readonly LeaderboardStore board = new();
    private readonly SoundBank audio = new();
    private readonly Image? roadsidePoster = LoadRoadsidePoster();
    private readonly Image? brandLogo = LoadBrandLogo();
    private readonly Image? majorPotholeSprite = LoadMajorPotholeSprite();
    private CrashSceneFrames crashScene = new();
    private readonly CarSkinArt carSkins = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double previousTime, accumulator, idleTime, roadScroll;
    private bool fullscreen, scoreSubmitted, mouseBrake, mouseScan;
    private bool menuOpen;
    private bool selectingCar;
    private int selectedCar = 1;
    private MenuPage menuPage = MenuPage.Home;
    private PointF pointer = new(-100, -100);
    private string typedName = "";
    private float shake;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    public GameForm()
    {
        game = new(config);
        game.SoundRequested += cue => audio.Play(cue == "crash" ? CrashSoundName(selectedCar) : cue);
        this.Text = $"RoadSense Patrol v{typeof(GameForm).Assembly.GetName().Version?.ToString(3)}";
        Icon = LoadAppIcon() ?? Icon;
        ClientSize = new Size(1280, 768);
        MinimumSize = new Size(960, 576);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Navy;
        DoubleBuffered = true;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        KeyDown += OnKeyDown;
        KeyPress += OnKeyPress;
        KeyUp += OnKeyUp;
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += (_, e) => { pointer = ToGame(e.Location); Invalidate(); };
        MouseLeave += (_, _) => { pointer = new(-100, -100); Invalidate(); };
        Deactivate += (_, _) => { game.AccelerateHeld = false; ReleasePointerButtons(); };
        timer.Tick += Tick;
        timer.Start();
        if (config.FullscreenOnStart) ToggleFullscreen();
    }

    private void Tick(object? sender, EventArgs e)
    {
        SyncPhysicalBoostKey();
        double now = clock.Elapsed.TotalSeconds;
        accumulator += Math.Min(0.1, now - previousTime);
        previousTime = now;
        while (accumulator >= 1.0 / 60)
        {
            if (!menuOpen && !selectingCar) game.Update(1f / 60f);
            idleTime += 1.0 / 60;
            if (!menuOpen && !selectingCar) roadScroll += game.Phase == GamePhase.Attract ? 3.6 :
                3.6 * Math.Max(0.45f, game.EffectiveSpeed / config.BaseSpeed);
            shake = Math.Max(0, shake - 1f / 60f);
            if (game.FlashSeconds > 0) shake = 0.15f;
            if (!selectingCar && game.Phase == GamePhase.Results && game.ResultIdle >= 20)
            {
                if (!scoreSubmitted && board.Qualifies(game.Score)) SubmitScore(automatic: true);
                game.Attract();
            }
            accumulator -= 1.0 / 60;
        }
        Invalidate();
    }

    private void SyncPhysicalBoostKey()
    {
        if (!IsHandleCreated) return;
        bool foreground = GetForegroundWindow() == Handle;
        bool boostDown = foreground && (GetAsyncKeyState((int)Keys.W) < 0 ||
            GetAsyncKeyState((int)Keys.Up) < 0);
        SyncBoostKey(foreground, boostDown);
    }

    private void SyncBoostKey(bool foreground, bool boostDown)
    {
        if (!foreground || menuOpen || selectingCar)
        {
            game.AccelerateHeld = false;
            return;
        }
        if (game.Phase is GamePhase.Countdown or GamePhase.Playing)
            game.AccelerateHeld = boostDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.Shift && e.KeyCode == Keys.Q) { Close(); return; }
        if (e.Control && e.Shift && e.KeyCode == Keys.R)
        {
            if (MessageBox.Show(this, "Clear all local RoadSense Patrol scores?", "Admin reset",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) board.Clear();
            return;
        }
        if (e.KeyCode == Keys.F11) { ToggleFullscreen(); return; }
        if (selectingCar)
        {
            if (e.KeyCode is Keys.Left or Keys.A) selectedCar = (selectedCar + 2) % 3;
            else if (e.KeyCode is Keys.Right or Keys.D) selectedCar = (selectedCar + 1) % 3;
            else if (e.KeyCode is Keys.Enter or Keys.Space) ConfirmCarSelection();
            else if (e.KeyCode == Keys.Escape) selectingCar = false;
            return;
        }
        if (game.Phase is GamePhase.Crashing or GamePhase.CrashOutro)
        {
            if (e.KeyCode is Keys.Space or Keys.Enter) game.SkipCrash();
            return;
        }
        if (e.KeyCode == Keys.Escape) { menuOpen = !menuOpen; menuPage = MenuPage.Home; game.SetBrake(false); game.ScanHeld = game.AccelerateHeld = false; return; }
        if (menuOpen)
        {
            if (e.KeyCode == Keys.R) menuPage = MenuPage.Rankings;
            else if (e.KeyCode == Keys.H) menuPage = MenuPage.Guide;
            else if (e.KeyCode is Keys.Enter or Keys.Space) menuOpen = false;
            return;
        }
        if (game.Phase == GamePhase.Attract)
        {
            if (!e.Control && !e.Alt && e.KeyCode != Keys.ShiftKey)
            {
                StartGame();
            }
            return;
        }
        if (game.Phase == GamePhase.Countdown)
        {
            if (e.KeyCode is Keys.W or Keys.Up) game.PressBoost();
            return;
        }
        if (game.Phase == GamePhase.Results)
        {
            game.TouchResults();
            if (e.KeyCode == Keys.Back && typedName.Length > 0 && !scoreSubmitted)
                typedName = typedName[..^1];
            else if (e.KeyCode == Keys.Enter && !scoreSubmitted && board.Qualifies(game.Score)) SubmitScore();
            else if ((e.KeyCode is Keys.Space or Keys.Enter) &&
                (scoreSubmitted || !board.Qualifies(game.Score))) StartGame();
            return;
        }
        if (game.Phase != GamePhase.Playing) return;
        if (e.KeyCode == Keys.M) { audio.Muted = !audio.Muted; return; }
        if (e.KeyCode is Keys.A or Keys.Left) game.ChangeLane(-1);
        if (e.KeyCode is Keys.D or Keys.Right) game.ChangeLane(1);
        if (e.KeyCode is Keys.S or Keys.Down) game.SetBrake(true);
        if (e.KeyCode == Keys.B) game.ScanHeld = true;
        if (e.KeyCode is Keys.W or Keys.Up) game.PressBoost();
    }

    private void OnKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (game.Phase != GamePhase.Results || menuOpen || selectingCar || scoreSubmitted ||
            !board.Qualifies(game.Score)) return;
        if (typedName.Length >= LeaderboardStore.MaxNameLength) return;
        char character = e.KeyChar;
        if (char.IsLetterOrDigit(character) || character is ' ' or '-' or '\'' or '.')
        {
            typedName += character;
            game.TouchResults();
            e.Handled = true;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.S or Keys.Down) game.SetBrake(mouseBrake);
        if (e.KeyCode == Keys.B) game.ScanHeld = mouseScan;
        if (e.KeyCode is Keys.W or Keys.Up)
            game.AccelerateHeld = IsHandleCreated && GetForegroundWindow() == Handle &&
                GetAsyncKeyState((int)(e.KeyCode == Keys.W ? Keys.Up : Keys.W)) < 0;
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        Focus();
        if (game.Phase is GamePhase.Crashing or GamePhase.CrashOutro) { game.SkipCrash(); return; }
        var p = ToGame(e.Location);
        if (selectingCar)
        {
            if (Hit(p, 20, 16, 145, 48)) { selectingCar = false; return; }
            for (int i = 0; i < 3; i++)
            {
                float x = 22 + i * 394;
                if (Hit(p, x, 94, 366, 590))
                {
                    selectedCar = i;
                    if (Hit(p, x + 25, 615, 316, 52)) ConfirmCarSelection();
                    return;
                }
            }
            return;
        }
        if (menuOpen)
        {
            if (Hit(p, 24, 22, 96, 48) || Hit(p, 950, 42, 204, 62)) { menuOpen = false; menuPage = MenuPage.Home; return; }
            if (Hit(p, 252, 190, 216, 66)) { menuPage = MenuPage.Home; return; }
            if (Hit(p, 492, 190, 216, 66)) { menuPage = MenuPage.Rankings; return; }
            if (Hit(p, 732, 190, 216, 66)) { menuPage = MenuPage.Guide; return; }
            if (Hit(p, 480, 618, 240, 62)) { menuOpen = false; if (game.Phase is GamePhase.Attract or GamePhase.Results) StartGame(); return; }
            return;
        }
        if (Hit(p, 18, 16, 164, 55)) { menuOpen = true; menuPage = MenuPage.Home; game.SetBrake(false); game.ScanHeld = game.AccelerateHeld = false; return; }
        if (game.Phase == GamePhase.Attract) { StartGame(); return; }
        if (game.Phase == GamePhase.Results)
        {
            game.TouchResults();
            if (!scoreSubmitted && board.Qualifies(game.Score))
            {
                if (p.Y >= 545 && p.Y <= 605 && p.X >= 350 && p.X <= 850) { Focus(); return; }
                if (p.Y >= 620 && p.Y <= 680 && p.X >= 480 && p.X <= 720) { SubmitScore(); return; }
            }
            StartGame(); return;
        }
        if (game.Phase != GamePhase.Playing) return;
        if (p.X >= 1010 && p.Y >= 550 && p.Y <= 620) { mouseBrake = true; game.SetBrake(true); return; }
        if (p.X >= 1010 && p.Y >= 630 && p.Y <= 700) { mouseScan = true; game.ScanHeld = true; return; }
        if (p.X >= 340 && p.X <= 860) game.ChangeLane(p.X < 600 ? -1 : 1);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        ReleasePointerButtons();
    }

    private void ReleasePointerButtons()
    {
        mouseBrake = mouseScan = false;
        game.SetBrake(false); game.ScanHeld = false;
    }

    private void StartGame()
    {
        menuOpen = false;
        selectingCar = true;
        game.AccelerateHeld = false;
    }

    private void ConfirmCarSelection()
    {
        var nextCrashScene = new CrashSceneFrames(selectedCar);
        crashScene.Dispose();
        crashScene = nextCrashScene;
        game.SetCrashSceneDuration(crashScene.ClipDurationSeconds);
        selectingCar = false;
        scoreSubmitted = false;
        typedName = "";
        game.Start();
    }

    private static string CrashSoundName(int carIndex) => carIndex switch
    {
        1 => "crash2",
        2 => "crash3",
        _ => "crash"
    };

    private void SubmitScore(bool automatic = false)
    {
        if (scoreSubmitted) return;
        if (!automatic && string.IsNullOrWhiteSpace(typedName)) return;
        board.Add(typedName, game.Score);
        scoreSubmitted = true;
        audio.Play("results");
    }

    private void ToggleFullscreen()
    {
        fullscreen = !fullscreen;
        FormBorderStyle = fullscreen ? FormBorderStyle.None : FormBorderStyle.Sizable;
        WindowState = fullscreen ? FormWindowState.Maximized : FormWindowState.Normal;
        TopMost = fullscreen;
    }

    private PointF ToGame(Point point)
    {
        float scale = Math.Min(ClientSize.Width / 1200f, ClientSize.Height / 720f);
        return new((point.X - (ClientSize.Width - 1200 * scale) / 2) / scale,
            (point.Y - (ClientSize.Height - 720 * scale) / 2) / scale);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Navy);
        float scale = Math.Min(ClientSize.Width / 1200f, ClientSize.Height / 720f);
        g.TranslateTransform((ClientSize.Width - 1200 * scale) / 2, (ClientSize.Height - 720 * scale) / 2);
        g.ScaleTransform(scale, scale);
        if (selectingCar) { DrawCarSelection(g); return; }
        if (shake > 0) g.TranslateTransform((float)(Random.Shared.NextDouble() - 0.5) * 8, (float)(Random.Shared.NextDouble() - 0.5) * 8);
        if (game.Phase == GamePhase.CrashOutro)
        {
            float progress = Math.Clamp(game.CrashOutroProgress, 0, 1);
            if (progress < .5f)
            {
                DrawRoad(g);
                DrawSidebars(g);
                DrawCrash(g);
            }
            else DrawResults(g);
            float fade = progress < .5f ? progress * 2 : (1 - progress) * 2;
            fade = fade * fade * (3 - 2 * fade);
            Fill(g, Color.FromArgb((int)(255 * fade), Navy), 0, 0, 1200, 720);
        }
        else if (game.Phase == GamePhase.Results) DrawResults(g);
        else
        {
            DrawRoad(g);
            if (game.Phase == GamePhase.Attract) DrawAttract(g);
            else
            {
                DrawSidebars(g);
                if (game.Phase == GamePhase.Countdown) DrawCountdown(g);
                else if (game.Phase == GamePhase.Crashing) DrawCrash(g);
                else DrawPlayingOverlays(g);
            }
        }
        if (menuOpen) DrawMenu(g);
        else if (game.Phase is not (GamePhase.Crashing or GamePhase.CrashOutro)) DrawMenuButton(g);
    }

    private void DrawRoad(Graphics g)
    {
        Fill(g, Color.FromArgb(27, 68, 56), 200, 0, 800, 720);
        Fill(g, Color.FromArgb(35, 86, 62), 218, 0, 122, 720);
        Fill(g, Color.FromArgb(35, 86, 62), 860, 0, 122, 720);
        Fill(g, Color.FromArgb(48, 60, 68), 332, 0, 536, 720);
        Fill(g, Color.FromArgb(34, 45, 56), 340, 0, 520, 720);
        Fill(g, Color.FromArgb(204, 209, 194), 338, 0, 3, 720);
        Fill(g, Color.FromArgb(204, 209, 194), 859, 0, 3, 720);
        for (int y = -100; y < 800; y += 110)
        {
            float yy = (float)(y + roadScroll % 110);
            Fill(g, Color.FromArgb(190, 201, 210), 510, yy, 6, 58);
            Fill(g, Color.FromArgb(190, 201, 210), 683, yy, 6, 58);
        }
        DrawScenery(g);
        if (game.Phase == GamePhase.Attract)
        {
            DrawHazard(g, LaneCenter(1), (float)((idleTime * 95 + 250) % 800) - 80, HazardType.Pothole);
            DrawTraffic(g, 780, (float)((idleTime * 85 + 90) % 900) - 120, TrafficType.Car, 1);
            DrawTraffic(g, 420, (float)((idleTime * 65 + 460) % 900) - 150, TrafficType.Truck, 3);
        }
        else
        {
            foreach (var obj in game.Objects.Where(o => !o.Traffic.HasValue))
            {
                float x = LaneCenter(obj.Lane);
                if (obj.Hazard.HasValue && obj.Warned && !obj.Resolved)
                    DrawHazard(g, x, obj.Y, obj.Hazard.Value);
                if (obj.Pickup.HasValue) DrawPickup(g, x, obj.Y, obj.Pickup.Value);
            }
            foreach (var obj in game.Objects.Where(o => o.Traffic.HasValue))
                DrawTraffic(g, LaneCenter(obj.VisualLane ?? obj.Lane), obj.Y, obj.Traffic!.Value, obj.ColorIndex);
        }
        float carX = game.Phase == GamePhase.Attract
            ? 600 + (float)Math.Sin(idleTime * 1.2) * 130 : LaneCenter(game.CarLane);
        float carY = game.Phase == GamePhase.Attract ? GameModel.PlayerY : game.CarY;
        DrawScanner(g, carX, carY);
        if (game.Phase is GamePhase.Crashing or GamePhase.CrashOutro)
        {
            var state = g.Save();
            g.TranslateTransform(carX, carY + game.CrashProgress * 75);
            g.RotateTransform(game.CrashProgress * 210);
            DrawCar(g, 0, 0);
            g.Restore(state);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                float radius = 30 + game.CrashProgress * 125;
                FillEllipse(g, i % 2 == 0 ? Amber : Red, carX + (float)Math.Cos(angle) * radius - 6,
                    carY + (float)Math.Sin(angle) * radius - 6, 12, 12);
            }
        }
        else
        {
            var state = g.Save();
            g.TranslateTransform(carX, carY);
            g.RotateTransform(game.Phase == GamePhase.Playing ? (game.Lane - game.CarLane) * 6 : 0);
            DrawCar(g, 0, 0);
            g.Restore(state);
        }
    }

    private void DrawScenery(Graphics g)
    {
        float offset = (float)(roadScroll % 230);
        for (int y = -230; y < 920; y += 230)
        {
            DrawTree(g, 269, y + offset, 1f);
            DrawTree(g, 933, y + 110 + offset, .9f);
        }
        float posterY = (float)((roadScroll + 245) % 980) - 80;
        DrawPoster(g, 201, posterY);
    }

    private static float LaneCenter(float lane) => 426.67f + lane * 173.33f;

    private static Image? LoadRoadsidePoster()
    {
        var assembly = typeof(GameForm).Assembly;
        string? name = assembly.GetManifestResourceNames().FirstOrDefault(n =>
            n.EndsWith("roadsense-tax-poster-prototype.png", StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static Image? LoadBrandLogo()
    {
        var assembly = typeof(GameForm).Assembly;
        string? name = assembly.GetManifestResourceNames().FirstOrDefault(n =>
            n.EndsWith("roadsense-logo.png", StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static Icon? LoadAppIcon()
    {
        var assembly = typeof(GameForm).Assembly;
        string? name = assembly.GetManifestResourceNames().FirstOrDefault(n =>
            n.EndsWith("roadsense-app.ico", StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    private static Image? LoadMajorPotholeSprite()
    {
        var assembly = typeof(GameForm).Assembly;
        string? name = assembly.GetManifestResourceNames().FirstOrDefault(n =>
            n.EndsWith("major-pothole.png", StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private void DrawBrand(Graphics g, float x, float y, bool large = false)
    {
        float icon = large ? 72 : 52;
        FillRound(g, Color.FromArgb(18, 35, 51), x, y, icon, icon, 13);
        if (brandLogo is not null)
        {
            var old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(brandLogo, x + icon * .13f, y + icon * .13f, icon * .74f, icon * .74f);
            g.InterpolationMode = old;
        }
    }

    private void DrawTree(Graphics g, float x, float y, float scale)
    {
        FillEllipse(g, Color.FromArgb(65, 0, 0, 0), x - 25 * scale, y + 14 * scale, 55 * scale, 24 * scale);
        FillRound(g, Color.FromArgb(102, 76, 56), x - 5 * scale, y + 3 * scale, 10 * scale, 36 * scale, 3);
        FillEllipse(g, Color.FromArgb(23, 98, 70), x - 27 * scale, y - 31 * scale, 54 * scale, 56 * scale);
        FillEllipse(g, Color.FromArgb(34, 126, 77), x - 22 * scale, y - 33 * scale, 42 * scale, 40 * scale);
        FillEllipse(g, Color.FromArgb(63, 153, 84), x - 12 * scale, y - 31 * scale, 28 * scale, 24 * scale);
    }

    private void DrawPoster(Graphics g, float x, float y)
    {
        if (roadsidePoster is null) return;
        Fill(g, Color.FromArgb(73, 77, 82), x + 17, y + 71, 5, 37);
        Fill(g, Color.FromArgb(73, 77, 82), x + 105, y + 81, 5, 37);
        PointF[] corners = [new(x + 7, y), new(x + 135, y + 14), new(x, y + 93)];
        g.DrawImage(roadsidePoster, corners);
    }

    private void DrawScanner(Graphics g, float x, float y)
    {
        if (game.Phase == GamePhase.Playing)
        {
            float top = Math.Max(0, game.RadarStartY);
            float laneX = LaneCenter(game.Lane);
            bool wide = game.RadarWide;
            PointF[] beam = wide
                ? [new(342, top), new(858, top), new(x + 69, y - 29), new(x - 69, y - 29)]
                : [new(laneX - 79, top), new(laneX + 79, top),
                    new(x + 52, y - 29), new(x - 52, y - 29)];
            using var beamBrush = new SolidBrush(Color.FromArgb(wide ? 49 : 34, Teal));
            using var edgePen = new Pen(Color.FromArgb(wide ? 140 : 87, Cyan), wide ? 2.5f : 1.5f);
            g.FillPolygon(beamBrush, beam);
            g.DrawLine(edgePen, beam[0], beam[3]);
            g.DrawLine(edgePen, beam[1], beam[2]);
            g.DrawLine(edgePen, beam[0], beam[1]);
            return;
        }
        using var brush = new SolidBrush(Color.FromArgb(42, Teal));
        g.FillPie(brush, x - 165, y - 310, 330, 330, 205, 130);
        using var pen = new Pen(Color.FromArgb(95, Cyan), 2);
        g.DrawArc(pen, x - 155, y - 300, 310, 310, 205, 130);
        if (game.ScanHeld && game.Phase == GamePhase.Playing)
        {
            using var pulse = new Pen(Color.FromArgb(180, Cyan), 4);
            g.DrawArc(pulse, x - 225, y - 440, 450, 450, 205, 130);
        }
    }

    private void DrawCarSelection(Graphics g)
    {
        Fill(g, Color.FromArgb(6, 18, 36), 0, 0, 1200, 720);
        for (int i = 0; i < 15; i++)
        {
            float stripe = (float)((i * 117 + idleTime * 20) % 1450) - 120;
            using var pen = new Pen(Color.FromArgb(17, Cyan), 2);
            g.DrawLine(pen, stripe, 0, stripe - 180, 720);
        }
        DrawText(g, "CHOOSE YOUR RIDE", 39, White, 600, 30, true, false, FontStyle.Bold);
        DrawText(g, "PICK A CAR BEFORE THE PATROL BEGINS", 14, Color.FromArgb(115, 174, 213), 600, 79,
            true, false, FontStyle.Bold);
        UiButton(g, "← BACK", 20, 16, 145, 48, Cyan);
        string[] names = ["THE KING", "LIGHTNING McQUEEN", "DOC HUDSON"];
        string[] numbers = ["43", "95", "51"];
        for (int i = 0; i < 3; i++)
        {
            float x = 22 + i * 394;
            bool hover = Hit(pointer, x, 94, 366, 590);
            bool active = i == selectedCar;
            float lift = hover ? 5 + (float)Math.Sin(idleTime * 6) * 2 : 0;
            float y = 100 - lift;
            Color accent = active || hover ? Cyan : Color.FromArgb(34, 92, 142);
            FillRound(g, Color.FromArgb(active ? 75 : hover ? 40 : 15, Cyan), x - 5, y - 5, 376, 585, 20);
            FillRound(g, Color.FromArgb(11, 33, 59), x, y, 366, 575, 16);
            using (var rim = new Pen(accent, active ? 4 : hover ? 3 : 2))
                g.DrawRectangle(rim, x + 2, y + 2, 362, 571);
            using (var line = new Pen(Color.FromArgb(active ? 105 : 40, Cyan), 2))
            {
                g.DrawLine(line, x + 18, y + 28, x + 55, y + 28);
                g.DrawLine(line, x + 311, y + 28, x + 348, y + 28);
                g.DrawLine(line, x + 18, y + 544, x + 55, y + 544);
                g.DrawLine(line, x + 311, y + 544, x + 348, y + 544);
            }
            if (active || hover)
            {
                for (int beam = 0; beam < 2; beam++)
                {
                    float bx = x + (beam == 0 ? 44 : 317);
                    float drift = (float)Math.Sin(idleTime * 3 + beam) * 8;
                    using var glow = new Pen(Color.FromArgb(90, Cyan), 3);
                    g.DrawLine(glow, bx, y + 95 + drift, bx, y + 420 + drift);
                }
            }
            FillEllipse(g, Color.FromArgb(active ? 42 : 19, Cyan), x + 60, y + 420, 245, 68);
            var art = carSkins.Display(i);
            float carHeight = i == 1 ? 390 : 385;
            float carWidth = carHeight * art.Width / art.Height;
            float bob = hover ? (float)Math.Sin(idleTime * 5 + i) * 3 : 0;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(art, x + (366 - carWidth) / 2, y + 31 + bob, carWidth, carHeight);
            DrawText(g, names[i], 22, White, x + 183, y + 460, true, false, FontStyle.Bold);
            DrawText(g, $"NO. {numbers[i]}", 13, active ? Cyan : Muted, x + 183, y + 497, true, false,
                FontStyle.Bold);
            UiButton(g, active ? "SELECTED  •  START" : "SELECT", x + 25, 615 - lift, 316, 52,
                active ? Cyan : Color.FromArgb(59, 133, 188), active);
        }
        DrawText(g, "← → OR A / D TO CHOOSE     •     ENTER TO START", 13, Muted, 600, 687, true,
            false, FontStyle.Bold);
    }

    private void DrawCar(Graphics g, float x, float y)
    {
        FillEllipse(g, Color.FromArgb(65, 0, 0, 0), x - 34, y - 55, 75, 122);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(carSkins.Driving(selectedCar), x - 34, y - 61, 68, 122);
    }

    private void DrawTraffic(Graphics g, float x, float y, TrafficType kind, int colorIndex)
    {
        Color[] colors = [Color.FromArgb(64, 127, 213), Color.FromArgb(238, 177, 54),
            Color.FromArgb(165, 100, 203), Color.FromArgb(230, 103, 85),
            Color.FromArgb(198, 205, 212), Color.FromArgb(80, 165, 113)];
        Color body = colors[Math.Abs(colorIndex) % colors.Length];
        if (kind == TrafficType.Motorcycle)
        {
            FillEllipse(g, Color.FromArgb(65, 0, 0, 0), x - 23, y - 45, 52, 103);
            FillEllipse(g, Color.FromArgb(20, 25, 31), x - 11, y - 48, 22, 27);
            FillEllipse(g, Color.FromArgb(20, 25, 31), x - 11, y + 28, 22, 27);
            FillRound(g, body, x - 15, y - 35, 30, 74, 10);
            FillEllipse(g, Color.FromArgb(28, 42, 56), x - 15, y - 24, 30, 36);
            FillEllipse(g, Color.FromArgb(225, 231, 233), x - 10, y - 22, 20, 18);
            Fill(g, Color.FromArgb(22, 30, 37), x - 29, y - 15, 58, 5);
            FillEllipse(g, Color.FromArgb(229, 191, 144), x - 24, y - 13, 9, 9);
            FillEllipse(g, Color.FromArgb(229, 191, 144), x + 15, y - 13, 9, 9);
            Fill(g, White, x - 5, y - 39, 10, 4);
            Fill(g, Red, x - 7, y + 38, 14, 5);
        }
        else if (kind == TrafficType.Truck)
        {
            FillRound(g, Color.FromArgb(75, 0, 0, 0), x - 44, y - 70, 94, 165, 11);
            FillRound(g, Color.FromArgb(93, 106, 115), x - 40, y - 77, 80, 169, 9);
            FillRound(g, body, x - 38, y - 77, 76, 62, 9);
            FillRound(g, Color.FromArgb(37, 48, 60), x - 31, y - 57, 62, 20, 4);
            FillRound(g, Color.FromArgb(215, 221, 220), x - 31, y - 5, 62, 87, 4);
            Fill(g, Color.FromArgb(140, 148, 149), x - 29, y + 57, 58, 4);
            Fill(g, White, x - 33, y - 73, 13, 6); Fill(g, White, x + 20, y - 73, 13, 6);
            Fill(g, Red, x - 32, y + 84, 14, 6); Fill(g, Red, x + 18, y + 84, 14, 6);
        }
        else
        {
            FillRound(g, Color.FromArgb(74, 0, 0, 0), x - 39, y - 47, 84, 116, 16);
            FillRound(g, body, x - 35, y - 55, 70, 108, 14);
            FillRound(g, Color.FromArgb(31, 46, 63), x - 27, y - 28, 54, 28, 5);
            FillRound(g, Color.FromArgb(31, 46, 63), x - 26, y + 21, 52, 17, 4);
            Fill(g, White, x - 27, y - 51, 14, 5); Fill(g, White, x + 13, y - 51, 14, 5);
            Fill(g, Red, x - 27, y + 47, 14, 5); Fill(g, Red, x + 13, y + 47, 14, 5);
        }
    }

    private void DrawHazard(Graphics g, float x, float y, HazardType type)
    {
        if (type == HazardType.Hump)
        {
            FillEllipse(g, Color.FromArgb(66, 0, 0, 0), x - 49, y - 14, 98, 35);
            FillEllipse(g, Color.FromArgb(85, 109, 99), x - 48, y - 22, 96, 36);
            using var stripe = new Pen(Color.FromArgb(137, 145, 143), 4);
            for (int i = -2; i <= 2; i++) g.DrawLine(stripe, x + i * 17 - 6, y - 12, x + i * 17 + 6, y + 8);
        }
        else if (type == HazardType.MinorPothole)
        {
            PointF[] crack = [new(x - 8, y - 33), new(x - 13, y - 22), new(x - 3, y - 10),
                new(x - 9, y + 1), new(x + 3, y + 11), new(x - 3, y + 27), new(x + 6, y + 37)];
            using var shadow = new Pen(Color.FromArgb(9, 13, 18), 7) { LineJoin = LineJoin.Round };
            using var edge = new Pen(Color.FromArgb(104, 108, 104), 1.5f) { LineJoin = LineJoin.Round };
            g.DrawLines(shadow, crack);
            g.DrawLines(edge, crack);
            g.DrawLine(shadow, x - 3, y - 10, x + 19, y - 18);
            g.DrawLine(edge, x - 3, y - 10, x + 19, y - 18);
            g.DrawLine(shadow, x - 9, y + 1, x - 27, y - 3);
            g.DrawLine(edge, x - 9, y + 1, x - 27, y - 3);
            g.DrawLine(shadow, x + 3, y + 11, x + 21, y + 15);
            g.DrawLine(edge, x + 3, y + 11, x + 21, y + 15);
            FillEllipse(g, Color.FromArgb(103, 110, 108), x - 28, y + 27, 5, 3);
            FillEllipse(g, Color.FromArgb(77, 83, 85), x + 22, y - 27, 4, 3);
        }
        else if (type == HazardType.Patch)
        {
            // Uneven asphalt layers blend into the road without a box or warning outline.
            PointF[] baseLayer = [new(x - 45, y - 17), new(x - 35, y - 29), new(x - 14, y - 25),
                new(x + 5, y - 33), new(x + 24, y - 27), new(x + 42, y - 22),
                new(x + 47, y - 5), new(x + 36, y + 7), new(x + 43, y + 23),
                new(x + 22, y + 28), new(x + 3, y + 25), new(x - 20, y + 33),
                new(x - 41, y + 19), new(x - 35, y + 3)];
            PointF[] freshAsphalt = [new(x - 36, y - 15), new(x - 23, y - 22), new(x - 7, y - 17),
                new(x + 8, y - 27), new(x + 30, y - 18), new(x + 36, y - 3),
                new(x + 28, y + 8), new(x + 32, y + 18), new(x + 12, y + 19),
                new(x - 8, y + 15), new(x - 28, y + 24), new(x - 34, y + 8)];
            using (var oldEdge = new SolidBrush(Color.FromArgb(60, 65, 68))) g.FillPolygon(oldEdge, baseLayer);
            using (var asphalt = new SolidBrush(Color.FromArgb(78, 82, 82))) g.FillPolygon(asphalt, freshAsphalt);
            using var seam = new Pen(Color.FromArgb(43, 48, 51), 3) { LineJoin = LineJoin.Round };
            g.DrawLines(seam, new PointF[] { new(x - 9, y - 27), new(x - 12, y - 10),
                new(x - 8, y + 5), new(x - 12, y + 23) });
            FillEllipse(g, Color.FromArgb(108, 110, 107), x - 33, y - 17, 5, 3);
            FillEllipse(g, Color.FromArgb(105, 108, 106), x + 18, y - 12, 7, 3);
            FillEllipse(g, Color.FromArgb(91, 96, 96), x + 22, y + 12, 4, 3);
            FillEllipse(g, Color.FromArgb(104, 105, 102), x - 24, y + 12, 6, 3);
        }
        else if (type == HazardType.Pothole)
        {
            if (majorPotholeSprite is not null)
            {
                var old = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(majorPotholeSprite, x - 82, y - 65, 164, 130);
                g.InterpolationMode = old;
                return;
            }
            // An asymmetric opening with a thick, fractured rim like the reference.
            PointF[] rim = [new(x - 72, y - 19), new(x - 65, y - 37), new(x - 52, y - 48),
                new(x - 35, y - 47), new(x - 22, y - 60), new(x - 4, y - 53),
                new(x + 13, y - 58), new(x + 29, y - 49), new(x + 49, y - 52),
                new(x + 58, y - 35), new(x + 70, y - 19), new(x + 65, y + 3),
                new(x + 70, y + 20), new(x + 55, y + 39), new(x + 40, y + 48),
                new(x + 18, y + 56), new(x + 2, y + 52), new(x - 19, y + 61),
                new(x - 40, y + 50), new(x - 60, y + 37), new(x - 68, y + 15)];
            PointF[] cavity = [new(x - 59, y - 16), new(x - 52, y - 27), new(x - 43, y - 35),
                new(x - 26, y - 35), new(x - 17, y - 47), new(x - 1, y - 41),
                new(x + 12, y - 46), new(x + 25, y - 38), new(x + 38, y - 40),
                new(x + 47, y - 28), new(x + 56, y - 13), new(x + 53, y + 5),
                new(x + 57, y + 17), new(x + 43, y + 30), new(x + 31, y + 39),
                new(x + 15, y + 46), new(x, y + 43), new(x - 16, y + 48),
                new(x - 33, y + 39), new(x - 48, y + 28), new(x - 56, y + 12)];
            var shadow = rim.Select(p => new PointF(p.X + 3, p.Y + 7)).ToArray();
            using (var brush = new SolidBrush(Color.FromArgb(15, 20, 28))) g.FillPolygon(brush, shadow);
            using (var glow = new Pen(Color.FromArgb(45, 31, 39, 49), 12) { LineJoin = LineJoin.Round })
                g.DrawPolygon(glow, rim);
            using (var edge = new Pen(Color.FromArgb(85, 94, 104), 3) { LineJoin = LineJoin.Round })
                g.DrawPolygon(edge, rim);
            using (var brush = new SolidBrush(Color.FromArgb(38, 44, 54))) g.FillPolygon(brush, rim);
            for (int i = 0; i < rim.Length; i++)
            {
                int next = (i + 1) % rim.Length;
                PointF[] facet = [rim[i], rim[next], cavity[next], cavity[i]];
                using var brush = new SolidBrush(i % 4 == 0 ? Color.FromArgb(82, 91, 103) :
                    i % 4 == 1 ? Color.FromArgb(24, 31, 43) :
                    i % 4 == 2 ? Color.FromArgb(58, 67, 80) : Color.FromArgb(36, 44, 56));
                g.FillPolygon(brush, facet);
            }
            using (var brush = new SolidBrush(Color.FromArgb(38, 48, 62))) g.FillPolygon(brush, cavity);
            using (var pen = new Pen(Color.FromArgb(10, 17, 27), 5) { LineJoin = LineJoin.Round })
                g.DrawPolygon(pen, cavity);
            using (var fissure = new Pen(Color.FromArgb(15, 24, 36), 2.5f) { LineJoin = LineJoin.Round })
            {
                g.DrawLines(fissure, new PointF[] { new(x - 25, y - 34), new(x - 19, y - 19), new(x - 18, y - 4) });
                g.DrawLines(fissure, new PointF[] { new(x + 28, y - 36), new(x + 22, y - 21), new(x + 28, y - 10) });
                g.DrawLines(fissure, new PointF[] { new(x + 11, y + 8), new(x + 17, y + 17), new(x + 30, y + 22) });
                g.DrawLines(fissure, new PointF[] { new(x - 43, y + 24), new(x - 31, y + 17), new(x - 24, y + 7) });
            }
            using (var fracture = new Pen(Color.FromArgb(15, 22, 32), 3) { LineJoin = LineJoin.Round })
            {
                g.DrawLines(fracture, new PointF[] { new(x - 61, y - 32), new(x - 75, y - 44), new(x - 70, y - 57) });
                g.DrawLines(fracture, new PointF[] { new(x + 54, y - 37), new(x + 67, y - 51), new(x + 72, y - 61) });
                g.DrawLines(fracture, new PointF[] { new(x + 51, y + 37), new(x + 67, y + 45), new(x + 72, y + 57) });
            }
            FillEllipse(g, Color.FromArgb(48, 57, 69), x - 84, y - 18, 10, 8);
            FillEllipse(g, Color.FromArgb(48, 57, 69), x + 74, y - 34, 12, 9);
            FillEllipse(g, Color.FromArgb(56, 65, 75), x - 66, y + 54, 14, 9);
            FillEllipse(g, Color.FromArgb(48, 57, 69), x + 52, y + 58, 11, 7);
        }
    }

    private void DrawPickup(Graphics g, float x, float y, PickupType pickup)
    {
        FillEllipse(g, Color.FromArgb(100, Green), x - 28, y - 28, 56, 56);
        FillEllipse(g, Green, x - 20, y - 20, 40, 40);
        string text = pickup == PickupType.RepairCrew ? "+" : pickup == PickupType.Shield ? "S" : "!";
        DrawText(g, text, 20, White, x, y, true, true, FontStyle.Bold);
    }

    private void DrawSidebars(Graphics g)
    {
        Fill(g, Navy, 0, 0, 200, 720);
        Fill(g, Navy, 1000, 0, 200, 720);
        Fill(g, Color.FromArgb(23, 42, 58), 196, 0, 4, 720);
        Fill(g, Color.FromArgb(23, 42, 58), 1000, 0, 4, 720);
        DrawBrand(g, 74, 92);
        FillRound(g, Panel, 12, 159, 176, 100, 15);
        Fill(g, Teal, 12, 176, 4, 63);
        DrawText(g, "SCORE", 12, Muted, 25, 173, style: FontStyle.Bold);
        DrawText(g, game.Score.ToString("N0"), 34, White, 23, 199, style: FontStyle.Bold);
        FillRound(g, Panel, 12, 270, 176, 96, 15);
        Fill(g, Cyan, 12, 286, 4, 60);
        DrawText(g, "CHAIN MULTIPLIER", 12, Muted, 25, 284, style: FontStyle.Bold);
        DrawText(g, $"x{game.Multiplier}", 34, Cyan, 23, 311, style: FontStyle.Bold);
        DrawText(g, $"{game.Streak} clean", 12, Muted, 95, 330);
        FillRound(g, Panel, 12, 377, 176, 84, 15);
        DrawText(g, "LIVES LEFT", 12, Muted, 25, 390, style: FontStyle.Bold);
        for (int i = 0; i < config.StartingLives; i++)
        {
            FillRound(g, i < game.Lives ? Red : Border, 25 + i * 49, 419, 37, 30, 8);
            DrawText(g, i < game.Lives ? "♥" : "×", 18, White, 43 + i * 49, 434, true, true, FontStyle.Bold);
        }
        DrawSpeedometer(g);
        DrawText(g, "A / D  STEER", 13, White, 20, 604, style: FontStyle.Bold);
        DrawText(g, game.AccelerateHeld ? "FORWARD BOOST" : "HOLD W / UP", 12, Amber, 20, 632, style: FontStyle.Bold);
        DrawText(g, "S  BRAKE   •   B  SCAN", 12, Muted, 20, 660);

        Color warningColor = game.WarningType == HazardType.Pothole ? Red :
            game.WarningType == HazardType.MinorPothole ? Amber :
            game.WarningType == HazardType.Patch ? Cyan :
            game.WarningType == HazardType.Hump ? Green :
            game.TrafficWarning ? Amber : Teal;
        DrawText(g, "SCANNER / ACTIVE", 13, Cyan, 1017, 29, style: FontStyle.Bold);
        FillRound(g, Panel, 1012, 69, 176, 192, 15);
        Fill(g, warningColor, 1012, 89, 4, 151);
        FillEllipse(g, warningColor, 1025, 89, 15, 15);
        DrawText(g, "ROAD WARNING", 12, Muted, 1048, 89, style: FontStyle.Bold);
        string warningLabel = game.WarningType == HazardType.Pothole ? "MAJOR\nPOTHOLE" :
            game.WarningType == HazardType.MinorPothole ? "MINOR\nPOTHOLE" :
            game.WarningType == HazardType.Patch ? "ROAD PATCH" :
            game.WarningType == HazardType.Hump ? "ROAD HUMP" :
            game.Warning;
        DrawText(g, warningLabel, 20, White, 1024, 139, width: 151, style: FontStyle.Bold);
        string advice = game.WarningType == HazardType.Pothole ? "CHANGE LANES" :
            game.TrafficWarning ? "CHANGE LANES" :
            game.WarningType is HazardType.MinorPothole or HazardType.Patch or HazardType.Hump ? "BRAKE OR DODGE" : "WATCH THE ROAD";
        DrawText(g, advice, 13, warningColor, 1024, 229, width: 150, style: FontStyle.Bold);
        FillRound(g, Panel, 1012, 272, 176, 169, 15);
        DrawText(g, "SCAN ENERGY", 12, Muted, 1024, 281, style: FontStyle.Bold);
        FillRound(g, Border, 1024, 308, 152, 12, 6);
        FillRound(g, Cyan, 1024, 308, 152 * game.ScanMeter, 12, 6);
        DrawText(g, "COOLDOWN", 11, Muted, 1024, 329, style: FontStyle.Bold);
        DrawText(g, game.ScanCooldownRemaining > 0 ? $"{game.ScanCooldownRemaining:0.0}s" : "READY",
            11, game.ScanCooldownRemaining > 0 ? Amber : Green, 1145, 329, style: FontStyle.Bold);
        FillRound(g, Border, 1024, 352, 152, 10, 5);
        FillRound(g, game.ScanCooldownRemaining > 0 ? Amber : Green,
            1024, 352, 152 * game.ScanCooldownProgress, 10, 5);
        DrawText(g, game.Shield > 0 ? $"SHIELD  {game.Shield}" : "SHIELD  —", 11, Green, 1024, 370, style: FontStyle.Bold);
        DrawText(g, game.RepairCrew > 0 ? $"REPAIR  {game.RepairCrew}" : "REPAIR  —", 11, Green, 1024, 392, style: FontStyle.Bold);
        DrawText(g, game.SignalBoost > 0 ? $"SIGNAL  {game.SignalBoost:0}s" : "B: FAR + SIDE LANES",
            11, game.SignalBoost > 0 ? Cyan : Muted, 1024, 413);
        DrawText(g, "ROAD DISTRESS", 12, Muted, 1018, 448, style: FontStyle.Bold);
        FillEllipse(g, Red, 1019, 471, 9, 9); DrawText(g, "MAJOR  /  DODGE", 10, White, 1038, 466);
        FillEllipse(g, Amber, 1019, 490, 9, 9); DrawText(g, "MINOR  /  BRAKE", 10, White, 1038, 485);
        FillEllipse(g, Green, 1019, 509, 9, 9); DrawText(g, "HUMP  /  BRAKE", 10, White, 1038, 504);
        FillEllipse(g, Cyan, 1019, 528, 9, 9); DrawText(g, "PATCH  /  BRAKE", 10, White, 1038, 523);
        UiButton(g, "BRAKE", 1012, 550, 176, 70, White, game.BrakeHeld);
        UiButton(g, game.ScanCooldownRemaining > 0 ? "COOLING" : game.ScanMeter < .25f ? "CHARGING" : "SCAN",
            1012, 630, 176, 70, game.ScanCooldownRemaining > 0 ? Amber : Cyan, game.IsScanning);
        DrawText(g, "TAP ROAD SIDES TO STEER", 12, White, 600, 686, true, true, FontStyle.Bold);
    }

    private static bool Hit(PointF p, float x, float y, float w, float h) =>
        p.X >= x && p.X <= x + w && p.Y >= y && p.Y <= y + h;

    private void DrawSpeedometer(Graphics g)
    {
        FillRound(g, Panel, 12, 472, 176, 117, 15);
        Color accent = game.IsBoosting ? Amber : Cyan;
        DrawText(g, "SPEEDOMETER", 12, Muted, 25, 483, style: FontStyle.Bold);
        DrawText(g, game.IsBoosting ? "BOOST" : "CRUISE", 11, accent,
            151, 484, true, false, FontStyle.Bold, 68);
        float fraction = Math.Clamp((game.DisplayKph - 60) / 70, 0, 1);
        using (var track = new Pen(Border, 7)) g.DrawArc(track, 42, 507, 116, 98, 155, 230);
        using (var progress = new Pen(accent, 7) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(progress, 42, 507, 116, 98, 155, 230 * fraction);
        double angle = (155 + 230 * fraction) * Math.PI / 180;
        using (var needle = new Pen(White, 2.5f) { EndCap = LineCap.Round })
            g.DrawLine(needle, 100, 556, 100 + (float)Math.Cos(angle) * 42, 556 + (float)Math.Sin(angle) * 42);
        FillEllipse(g, White, 96, 552, 8, 8);
        FillRound(g, Panel, 65, 532, 70, 36, 10);
        DrawText(g, $"{game.DisplayKph:0} km/h", 19, White, 100, 550, true, true, FontStyle.Bold);
        DrawText(g, $"CRUISE {game.CruiseKph:0} km/h", 10, Muted, 100, 570, true, true, FontStyle.Bold);
    }

    private void UiButton(Graphics g, string label, float x, float y, float w, float h, Color accent, bool active = false)
    {
        bool hover = Hit(pointer, x, y, w, h);
        float lift = hover ? 3 + (float)Math.Sin(idleTime * 7) * 1.5f : 0;
        FillRound(g, Color.FromArgb(65, accent), x, y + 5, w, h, 12);
        FillRound(g, active ? accent : hover ? Color.FromArgb(46, 71, 87) : Panel, x, y - lift, w, h, 12);
        using var pen = new Pen(hover || active ? accent : Border, hover ? 2.5f : 1.5f);
        g.DrawRectangle(pen, x + 1, y - lift + 1, w - 2, h - 2);
        DrawText(g, label, 18, active ? Navy : hover ? White : accent, x + w / 2, y - lift + h / 2,
            true, true, FontStyle.Bold);
    }

    private void DrawMenuButton(Graphics g)
    {
        UiButton(g, "☰  MENU", 18, 16, 164, 55, Cyan);
    }

    private void DrawMenu(Graphics g)
    {
        Fill(g, Navy, 0, 0, 1200, 720);
        Fill(g, Color.FromArgb(24, 54, 69), 0, 0, 14, 720);
        Fill(g, Teal, 0, 0, 14, 120);
        DrawBrand(g, 58, 44, large: true);
        DrawText(g, "PATROL / COMMAND CENTER", 15, Cyan, 61, 133, style: FontStyle.Bold);
        DrawText(g, "Pause. Learn the road. Chase the board.", 17, Muted, 61, 160);
        UiButton(g, "CLOSE", 950, 42, 204, 62, Cyan);
        UiButton(g, "OVERVIEW", 252, 190, 216, 66, menuPage == MenuPage.Home ? Teal : Muted,
            menuPage == MenuPage.Home);
        UiButton(g, "RANKINGS", 492, 190, 216, 66, menuPage == MenuPage.Rankings ? Teal : Muted,
            menuPage == MenuPage.Rankings);
        UiButton(g, "ROAD DISTRESS", 732, 190, 216, 66, menuPage == MenuPage.Guide ? Teal : Muted,
            menuPage == MenuPage.Guide);
        if (menuPage == MenuPage.Home)
        {
            FillRound(g, Panel, 250, 292, 700, 290, 18);
            Fill(g, Teal, 250, 323, 5, 229);
            DrawText(g, game.Phase == GamePhase.Playing ? "PATROL PAUSED" : "READY FOR THE ROAD?", 30,
                White, 286, 318, style: FontStyle.Bold);
            DrawText(g, "Three lives. No timer. Speed rises the longer you survive.", 18, Muted, 286, 374, width: 630);
            DrawText(g, "STEER", 15, Cyan, 286, 450, style: FontStyle.Bold);
            DrawText(g, "A / D or arrow keys", 16, White, 286, 481);
            DrawText(g, "BOOST / BRAKE", 15, Amber, 520, 450, style: FontStyle.Bold);
            DrawText(g, "W or Up / S", 16, White, 520, 481);
            DrawText(g, "SCAN", 15, Teal, 765, 450, style: FontStyle.Bold);
            DrawText(g, "Hold B", 16, White, 765, 481);
        }
        else if (menuPage == MenuPage.Rankings)
        {
            DrawText(g, "TOP 10  /  LOCAL ARCADE", 25, White, 250, 290, style: FontStyle.Bold);
            for (int i = 0; i < 10; i++)
            {
                float y = 338 + i * 25;
                if (i % 2 == 0) FillRound(g, Panel, 250, y - 2, 700, 24, 5);
                if (i < board.Entries.Count)
                {
                    DrawText(g, $"{i + 1:00}", 15, i == 0 ? Amber : Muted, 270, y, style: FontStyle.Bold);
                    DrawText(g, board.Entries[i].Name, 15, White, 350, y, style: FontStyle.Bold, width: 430);
                    DrawText(g, board.Entries[i].Score.ToString("N0"), 15, Cyan, 900, y, true, false, FontStyle.Bold);
                }
                else DrawText(g, $"{i + 1:00}     —", 14, Muted, 270, y);
            }
        }
        else
        {
            DrawText(g, "SPOT IT. CHOOSE THE SAFE RESPONSE.", 23, White, 250, 286, style: FontStyle.Bold, width: 720);
            DrawGuideCard(g, 250, 337, Red, "MAJOR POTHOLE", "Change lanes. A direct hit costs a life.");
            DrawGuideCard(g, 610, 337, Amber, "MINOR POTHOLE", "Brake or dodge to keep your streak.");
            DrawGuideCard(g, 250, 459, Green, "ROAD HUMP", "Brake before crossing the raised surface.");
            DrawGuideCard(g, 610, 459, Cyan, "ROAD PATCH", "Brake or dodge. Crossing it slows you.");
        }
        UiButton(g, game.Phase == GamePhase.Playing ? "RESUME PATROL" : "START PATROL",
            480, 618, 240, 62, Teal);
    }

    private static void DrawGuideCard(Graphics g, float x, float y, Color color, string title, string detail)
    {
        FillRound(g, Panel, x, y, 340, 104, 14);
        Fill(g, color, x, y + 18, 5, 68);
        DrawText(g, title, 18, color, x + 22, y + 17, style: FontStyle.Bold);
        DrawText(g, detail, 14, White, x + 22, y + 53, width: 290);
    }

    private void DrawPlayingOverlays(Graphics g)
    {
        if (game.FloatingSeconds > 0)
            DrawText(g, game.FloatingText, 28, game.FlashSeconds > 0 ? Red : Green,
                600, 405, true, true, FontStyle.Bold);
        if (game.FlashSeconds > 0) Fill(g, Color.FromArgb(55, Red), 200, 0, 800, 720);
        if (game.BonusFlashSeconds > 0) Fill(g, Color.FromArgb(35, Cyan), 200, 0, 800, 720);
    }

    private void DrawAttract(Graphics g)
    {
        Fill(g, Navy, 0, 0, 200, 720);
        Fill(g, Navy, 1000, 0, 200, 720);
        DrawBrand(g, 74, 94);
        DrawText(g, "HOW TO PLAY", 16, Teal, 18, 184, style: FontStyle.Bold);
        DrawText(g, "1  Spot the warning", 14, White, 18, 212);
        DrawText(g, "2  Dodge or brake", 14, White, 18, 257);
        DrawText(g, "3  Keep your streak", 14, White, 18, 302);
        DrawText(g, "THREE LIVES", 20, Red, 18, 390, style: FontStyle.Bold);
        DrawText(g, "Speed keeps rising.", 14, Muted, 18, 430);
        DrawText(g, "A / D   CHANGE LANE", 12, White, 18, 533);
        DrawText(g, "W / UP   SPEED BOOST", 12, Amber, 18, 565);
        DrawText(g, "S   BRAKE", 12, White, 18, 597);
        DrawText(g, "B   SCAN BOOST", 12, White, 18, 629);

        FillRound(g, Color.FromArgb(232, Navy), 275, 213, 650, 360, 22);
        Fill(g, Teal, 425, 213, 350, 4);
        DrawText(g, "ROADSENSE PATROL", 39, White, 600, 257, true, true, FontStyle.Bold);
        DrawText(g, "Spot hazards. React early. Drive safe.", 18, Cyan, 600, 307, true);
        float pulse = (float)(Math.Sin(idleTime * 3) * 5);
        FillRound(g, Color.FromArgb(80, Teal), 358 - pulse, 350 - pulse, 484 + pulse * 2, 102 + pulse * 2, 18);
        UiButton(g, "TAP OR PRESS ANY KEY", 365, 354, 470, 94, Teal);
        DrawText(g, "ENDLESS PATROL - THREE LIVES", 17, White, 600, 505, true, true, FontStyle.Bold);
        DrawText(g, "Hazards are simulated for demonstration.", 14, Muted, 600, 652, true, true);

        DrawText(g, "TOP 10", 19, Teal, 1016, 33, style: FontStyle.Bold);
        for (int i = 0; i < Math.Min(10, board.Entries.Count); i++)
        {
            var entry = board.Entries[i];
            DrawText(g, $"{i + 1:00}", 12, Muted, 1016, 75 + i * 28, style: FontStyle.Bold, width: 25);
            DrawText(g, entry.Name, 12, White, 1042, 75 + i * 28, width: 88);
            DrawText(g, entry.Score.ToString("N0"), 12, Cyan, 1132, 75 + i * 28, width: 65);
        }
        if (board.Entries.Count == 0) DrawText(g, "Your name could be here!", 13, Muted, 1016, 90, width: 165);
        DrawText(g, "RANK LADDER", 16, Teal, 1016, 427, style: FontStyle.Bold);
        DrawText(g, "ROOKIE DRIVER", 12, White, 1016, 470);
        DrawText(g, "ROAD SCOUT", 12, White, 1016, 503);
        DrawText(g, "HAZARD HUNTER", 12, White, 1016, 536);
        DrawText(g, "ROADSENSE PRO", 12, White, 1016, 569);
    }

    private void DrawCountdown(Graphics g)
    {
        Fill(g, Color.FromArgb(160, Navy), 200, 0, 800, 720);
        DrawText(g, game.CountdownNumber.ToString(), 94, Cyan, 600, 350, true, true, FontStyle.Bold);
        DrawText(g, "GET READY", 24, White, 600, 435, true, true, FontStyle.Bold);
    }

    private void DrawCrash(Graphics g)
    {
        Fill(g, Color.FromArgb(230, Navy), 0, 0, 1200, 720);
        FillRound(g, Panel, 56, 78, 1088, 568, 18);
        Fill(g, Red, 56, 78, 1088, 5);
        DrawText(g, "CRASH REPLAY", 27, White, 89, 101, style: FontStyle.Bold);
        DrawText(g, "FINAL LIFE LOST", 15, Red, 1015, 109, true, false, FontStyle.Bold);
        var frame = crashScene.FrameAt(game.CrashElapsed);
        float scale = Math.Min(1020f / frame.Width, 420f / frame.Height);
        float width = frame.Width * scale, height = frame.Height * scale;
        g.DrawImage(frame, 90 + (1020 - width) / 2, 156 + (420 - height) / 2, width, height);
        using (var border = new Pen(Border, 2)) g.DrawRectangle(border, 90, 156, 1020, 420);
        DrawText(g, "TAP, SPACE OR ENTER TO SKIP", 14, Muted, 600, 611, true, true, FontStyle.Bold);
    }

    private void DrawResults(Graphics g)
    {
        Fill(g, Navy, 0, 0, 1200, 720);
        DrawText(g, "PATROL COMPLETE", 29, White, 600, 34, true, true, FontStyle.Bold);
        DrawText(g, game.Score.ToString("N0"), 58, Cyan, 600, 108, true, true, FontStyle.Bold);
        DrawText(g, RankTitle(), 21, Teal, 600, 161, true, true, FontStyle.Bold);
        DrawText(g, $"ACCURACY {game.AccuracyPercent}%     BEST STREAK {game.BestStreak}     NEAR MISSES {game.NearMisses}",
            16, White, 600, 208, true, true, FontStyle.Bold);
        DrawText(g, $"TRAFFIC PASSED {game.TrafficPassed}     COLLISIONS {game.TrafficCollisions}",
            13, Muted, 600, 233, true, true, FontStyle.Bold);

        Card(g, 75, 248, 1050, 226);
        DrawText(g, "HAZARD ROUTE - LAST 25", 16, Muted, 100, 262, style: FontStyle.Bold);
        DrawText(g, "RED major   AMBER minor   CYAN patch   GREEN hump   X missed", 12, White, 794, 269, true, true);
        var recent = game.History.TakeLast(25).ToList();
        for (int lane = 0; lane < 3; lane++)
        {
            float y = 318 + lane * 53;
            FillRound(g, Border, 170, y + 8, 908, 3, 1);
            DrawText(g, $"LANE {lane + 1}", 13, Muted, 97, y - 1, style: FontStyle.Bold);
        }
        for (int i = 0; i < recent.Count; i++)
        {
            var h = recent[i];
            float x = 190 + i * 35.5f;
            float y = 326 + h.Lane * 53;
            Color color = h.Type == HazardType.Pothole ? Red : h.Type == HazardType.MinorPothole ? Amber :
                h.Type == HazardType.Patch ? Cyan : Green;
            FillEllipse(g, color, x - 10, y - 10, 20, 20);
            if (!h.Clean)
            {
                using var outline = new Pen(White, 2.5f);
                g.DrawEllipse(outline, x - 14, y - 14, 28, 28);
                g.DrawLine(outline, x - 5, y - 5, x + 5, y + 5);
                g.DrawLine(outline, x + 5, y - 5, x - 5, y + 5);
            }
        }

        var misses = game.History.Where(h => !h.Clean).ToList();
        if (misses.Count > 0)
        {
            var costly = misses.OrderByDescending(h => h.Type == HazardType.Pothole ? 3 :
                h.Type is HazardType.MinorPothole or HazardType.Hump ? 2 : 1).ThenByDescending(h => h.Time).First();
            string label = costly.Type == HazardType.Pothole ? "major pothole" :
                costly.Type == HazardType.MinorPothole ? "minor pothole" :
                costly.Type == HazardType.Patch ? "road patch" : "road hump";
            DrawText(g, $"COSTLIEST MISS: {label.ToUpperInvariant()} IN LANE {costly.Lane + 1}",
                15, Red, 600, 498, true, true, FontStyle.Bold);
        }
        else DrawText(g, "CLEAN PATROL - NO MISSED HAZARDS", 15, Green, 600, 498, true, true, FontStyle.Bold);
        if (config.ShowPrizeTarget)
            DrawText(g, game.Score >= config.PrizeScore ? "PRIZE TARGET REACHED!" : $"PRIZE TARGET {config.PrizeScore:N0}",
                13, Amber, 1050, 493, true, true, FontStyle.Bold);

        if (!scoreSubmitted && board.Qualifies(game.Score))
        {
            DrawText(g, "TOP 10! ENTER YOUR NAME", 16, Green, 600, 521, true, true, FontStyle.Bold);
            FillRound(g, Panel, 350, 545, 500, 60, 10);
            using (var outline = new Pen(Teal, 2)) g.DrawRectangle(outline, 351, 546, 498, 58);
            using (var font = new Font("Segoe UI", 23, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(typedName.Length > 0 ? White : Muted))
            using (var format = new StringFormat { FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center })
                g.DrawString(typedName.Length > 0 ? typedName : "Type your name", font, brush,
                    new RectangleF(370, 550, 460, 50), format);
            DrawText(g, $"{typedName.Length}/{LeaderboardStore.MaxNameLength} CHARACTERS  •  BACKSPACE TO EDIT",
                12, Muted, 600, 612, true, true);
            FillRound(g, !string.IsNullOrWhiteSpace(typedName) ? Green : Border, 480, 625, 240, 55, 10);
            DrawText(g, "SAVE SCORE", 18, White, 600, 649, true, true, FontStyle.Bold);
        }
        else
        {
            DrawText(g, scoreSubmitted ? "SCORE SAVED!" : "KEEP PRACTICING!", 20, Green, 600, 555, true, true, FontStyle.Bold);
            DrawText(g, "SPACE OR TAP TO PLAY AGAIN", 18, White, 600, 634, true, true, FontStyle.Bold);
        }
        DrawText(g, "AUTO RESET IN 20 SECONDS", 12, Muted, 600, 704, true, true);
    }

    private string RankTitle() => game.Score >= 6500 ? "ROADSENSE PRO" : game.Score >= 3500 ? "HAZARD HUNTER"
        : game.Score >= 1500 ? "ROAD SCOUT" : "ROOKIE DRIVER";

    private static Color Hex(string value) => ColorTranslator.FromHtml(value);
    private static void Fill(Graphics g, Color color, float x, float y, float w, float h)
    { using var brush = new SolidBrush(color); g.FillRectangle(brush, x, y, w, h); }
    private static void FillEllipse(Graphics g, Color color, float x, float y, float w, float h)
    { using var brush = new SolidBrush(color); g.FillEllipse(brush, x, y, w, h); }
    private static void FillRound(Graphics g, Color color, float x, float y, float w, float h, float radius)
    {
        if (w <= 0 || h <= 0) return;
        using var path = new GraphicsPath();
        float d = Math.Min(Math.Min(w, h), radius * 2);
        path.AddArc(x, y, d, d, 180, 90); path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90); path.AddArc(x, y + h - d, d, d, 90, 90); path.CloseFigure();
        using var brush = new SolidBrush(color); g.FillPath(brush, path);
    }
    private static void Card(Graphics g, float x, float y, float w, float h)
    {
        FillRound(g, Panel, x, y, w, h, 12);
        using var pen = new Pen(Border, 1.5f);
        g.DrawRectangle(pen, x, y, w, h);
    }
    private static void DrawText(Graphics g, string text, float size, Color color, float x, float y,
        bool center = false, bool middle = false, FontStyle style = FontStyle.Regular, float width = 0)
    {
        using var font = new Font("Segoe UI", size, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = center ? StringAlignment.Center : StringAlignment.Near,
            LineAlignment = middle ? StringAlignment.Center : StringAlignment.Near, Trimming = StringTrimming.EllipsisWord };
        g.DrawString(text, font, brush, new RectangleF(center ? x - (width > 0 ? width / 2 : 500) : x,
            middle ? y - size * 1.5f : y, width > 0 ? width : center ? 1000 : 400, middle ? size * 3 : size * 3.5f), format);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); audio.Dispose(); crashScene.Dispose(); roadsidePoster?.Dispose(); brandLogo?.Dispose(); majorPotholeSprite?.Dispose(); }
        base.Dispose(disposing);
    }
}
