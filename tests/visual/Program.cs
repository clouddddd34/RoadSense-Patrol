using System.Drawing;
using System.Reflection;
using System.Media;
using System.Windows.Forms;
using RoadSensePatrol;

Application.EnableVisualStyles();
var resources = typeof(GameForm).Assembly;
foreach (string suffix in new[] { "crash.wav", "crash-2.wav", "crash-3.wav" })
{
    using var crashWave = resources.GetManifestResourceStream(resources.GetManifestResourceNames()
        .Single(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))!;
    using var crashPlayer = new SoundPlayer(crashWave);
    crashPlayer.Load();
}
var crashSoundName = typeof(GameForm).GetMethod("CrashSoundName", BindingFlags.Static |
    BindingFlags.NonPublic)!;
string SoundFor(int car) => (string)crashSoundName.Invoke(null, [car])!;
if (SoundFor(0) != "crash" || SoundFor(1) != "crash2" || SoundFor(2) != "crash3")
    throw new Exception("Crash sounds must follow the selected car");
if (!resources.GetManifestResourceNames().Any(n => n.EndsWith("roadsense-app.ico", StringComparison.OrdinalIgnoreCase)))
    throw new Exception("RoadSense window icon is missing");
using (var scene = new CrashSceneFrames())
{
    if (Math.Abs(CrashSceneFrames.DurationSeconds - GameModel.CrashDurationSeconds) > .001f ||
        scene.FrameAt(0).Width != 1020 || scene.FrameAt(9.6f).Height < 400)
        throw new Exception("Embedded crash scene frames or timing are invalid");
}
using (var lightning = new CrashSceneFrames(1))
using (var doc = new CrashSceneFrames(2))
{
    if (lightning.ClipDurationSeconds < 27 || doc.ClipDurationSeconds < 7 ||
        lightning.FrameAt(0).Width < 800 || lightning.FrameAt(27.5f).Height < 350 ||
        doc.FrameAt(0).Width < 650 || doc.FrameAt(7.1f).Height < 400)
        throw new Exception("The Lightning McQueen or Doc Hudson crash scene is missing or incomplete");
}
var inputHandler = typeof(GameForm).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic,
    null, [typeof(object), typeof(KeyEventArgs)], null)!;
var keyUpHandler = typeof(GameForm).GetMethod("OnKeyUp", BindingFlags.Instance | BindingFlags.NonPublic,
    null, [typeof(object), typeof(KeyEventArgs)], null)!;
using (var inputForm = new GameForm())
{
    var inputModel = (GameModel)typeof(GameForm).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(inputForm)!;
    inputHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.W)]);
    if (!(bool)typeof(GameForm).GetField("selectingCar", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(inputForm)!) throw new Exception("Start must open car selection");
    inputHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.Right)]);
    if ((int)typeof(GameForm).GetField("selectedCar", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(inputForm)! != 2) throw new Exception("Arrow keys must change the selected car");
    inputHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.Enter)]);
    var selectedScene = (CrashSceneFrames)typeof(GameForm).GetField("crashScene", BindingFlags.Instance |
        BindingFlags.NonPublic)!.GetValue(inputForm)!;
    if (selectedScene.CarIndex != 2 ||
        Math.Abs(inputModel.CrashSceneDurationSeconds - selectedScene.ClipDurationSeconds) > .001f)
        throw new Exception("Doc Hudson selection must load the third crash scene and its duration");
    inputHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.W)]);
    if (inputModel.Phase != GamePhase.Countdown || !inputModel.AccelerateHeld)
        throw new Exception("W must start the game and stay held through countdown");
    for (int i = 0; i < 190; i++) inputModel.Update(1f / 60f);
    inputModel.Update(1f / 60f);
    if (inputModel.Phase != GamePhase.Playing || inputModel.EffectiveSpeed <= inputModel.Speed ||
        inputModel.EffectiveSpeed >= inputModel.Speed * 1.1f)
        throw new Exception("W key must begin a gradual boost once gameplay begins");
    for (int i = 0; i < 89; i++) inputModel.Update(1f / 60f);
    if (Math.Abs(inputModel.CarY - GameModel.BoostFrontY) > .01f ||
        inputModel.EffectiveSpeed < inputModel.Speed * 1.74f)
        throw new Exception("Holding W must reach the top and full boost");
    keyUpHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.W)]);
    inputModel.Update(1f / 60f);
    if (inputModel.AccelerateHeld || !inputModel.IsBoosting ||
        inputModel.EffectiveSpeed >= inputModel.Speed * 1.75f)
        throw new Exception("Releasing W must begin gradual deceleration");
    for (int i = 0; i < 89; i++) inputModel.Update(1f / 60f);
    if (inputModel.IsBoosting || Math.Abs(inputModel.EffectiveSpeed - inputModel.Speed) > .01f)
        throw new Exception("W boost must end and restore cruise speed");
    var syncBoostKey = typeof(GameForm).GetMethod("SyncBoostKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
    syncBoostKey.Invoke(inputForm, [true, true]);
    for (int i = 0; i < 90; i++) inputModel.Update(1f / 60f);
    if (!inputModel.AccelerateHeld || Math.Abs(inputModel.CarY - GameModel.BoostFrontY) > .01f)
        throw new Exception("Polling a physically held W must move the car to the top");
    syncBoostKey.Invoke(inputForm, [false, true]);
    for (int i = 0; i < 90; i++) inputModel.Update(1f / 60f);
    if (inputModel.AccelerateHeld || Math.Abs(inputModel.EffectiveSpeed - inputModel.Speed) > .01f)
        throw new Exception("Losing game focus must release W and restore cruise speed");
    inputModel.Attract();
    inputHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.Up)]);
    inputHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.Enter)]);
    inputHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.Up)]);
    if (inputModel.Phase != GamePhase.Countdown || !inputModel.AccelerateHeld)
        throw new Exception("Up Arrow must start the game and enable forward boost");
    for (int i = 0; i < 280; i++) inputModel.Update(1f / 60f);
    if (inputModel.Phase != GamePhase.Playing ||
        Math.Abs(inputModel.CarY - GameModel.BoostFrontY) > .01f ||
        inputModel.EffectiveSpeed < inputModel.Speed * 1.74f)
        throw new Exception("Holding Up Arrow must move the car to the top and increase speed");
    keyUpHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.Up)]);
    for (int i = 0; i < 90; i++) inputModel.Update(1f / 60f);
    if (inputModel.AccelerateHeld || Math.Abs(inputModel.CarY - GameModel.PlayerY) > .01f ||
        Math.Abs(inputModel.EffectiveSpeed - inputModel.Speed) > .01f)
        throw new Exception("Releasing Up Arrow must return the car and speed to cruise");
    inputModel.Attract();
    inputHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.Enter)]);
    inputHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.Right)]);
    inputHandler.Invoke(inputForm, [null, new KeyEventArgs(Keys.Enter)]);
    if (((CrashSceneFrames)typeof(GameForm).GetField("crashScene", BindingFlags.Instance |
        BindingFlags.NonPublic)!.GetValue(inputForm)!).CarIndex != 0 ||
        Math.Abs(inputModel.CrashSceneDurationSeconds - CrashSceneFrames.DurationSeconds) > .001f)
        throw new Exception("The King selection must load the first crash scene");
}
using var form = new GameForm();
if (!form.Text.Contains("v0.7.2")) throw new Exception("Window title must identify the build being tested");
var model = (GameModel)typeof(GameForm).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
void Save(string name)
{
    using var bitmap = new Bitmap(form.Width, form.Height);
    form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
    bitmap.Save(Path.Combine(AppContext.BaseDirectory, name));
    Console.WriteLine(name);
}
Save("start.png");
var keyHandler = typeof(GameForm).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic,
    null, [typeof(object), typeof(KeyEventArgs)], null)!;
void Key(Keys key) => keyHandler.Invoke(form, [null, new KeyEventArgs(key)]);
Key(Keys.Enter);
Save("car-selection.png");
Key(Keys.Right);
Save("car-selection-doc.png");
Key(Keys.Escape);
Key(Keys.Escape);
Save("menu.png");
Key(Keys.R);
Save("rankings.png");
Key(Keys.H);
Save("distress-guide.png");
Key(Keys.Escape);
typeof(GameForm).GetField("selectedCar", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, 1);
typeof(GameForm).GetMethod("ConfirmCarSelection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
if (((CrashSceneFrames)typeof(GameForm).GetField("crashScene", BindingFlags.Instance |
    BindingFlags.NonPublic)!.GetValue(form)!).CarIndex != 1 ||
    model.CrashSceneDurationSeconds < 27)
    throw new Exception("Lightning McQueen selection must load the second crash scene");
model.Start();
for (int i = 0; i < 190; i++) model.Update(1f / 60f);
model.AccelerateHeld = true;
for (int i = 0; i < 65; i++) model.Update(1f / 60f);
Save("speed-boost.png");
model.AccelerateHeld = false;
model.Start();
for (int i = 0; i < 190; i++) model.Update(1f / 60f);
model.ChangeLane(1);
for (int i = 0; i < 6; i++) model.Update(1f / 60f);
Save("lane-transition.png");
model.Start();
for (int i = 0; i < 12 * 60; i++) model.Update(1f / 60f);
Save("gameplay.png");
typeof(GameForm).GetField("selectedCar", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, 2);
Save("doc-hudson-road.png");
typeof(GameForm).GetField("selectedCar", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, 1);
model.ScanHeld = true;
for (int i = 0; i < 30; i++) model.Update(1f / 60f);
model.ScanHeld = false;
model.Update(1f / 60f);
Save("scan-cooldown.png");
var hazardPreview = new[]
{
    new RoadObject { Lane = 0, Hazard = HazardType.MinorPothole, Y = 220, Warned = true },
    new RoadObject { Lane = 1, Hazard = HazardType.Pothole, Y = 220, Warned = true },
    new RoadObject { Lane = 2, Hazard = HazardType.Hump, Y = 220, Warned = true }
};
var originalObjects = model.Objects.ToArray();
model.Objects.Clear();
model.Objects.AddRange(hazardPreview);
model.Update(1f / 60f);
typeof(GameModel).GetProperty("FloatingSeconds")!.SetValue(model, 0f);
Save("hazards.png");
model.Objects.Clear();
model.Objects.Add(new RoadObject { Lane = 1, Hazard = HazardType.Patch, Y = 220, Warned = true });
model.Update(1f / 60f);
Save("patch.png");
model.Objects.Clear();
model.Objects.AddRange(originalObjects);
for (int i = 0; i < 4 * 60; i++) model.Update(1f / 60f);
Save("traffic.png");
model.Objects.Add(new RoadObject { Lane = 2, Traffic = TrafficType.Truck, ColorIndex = 3, Y = 260 });
Save("traffic-truck.png");
model.Objects.Add(new RoadObject { Lane = 0, Traffic = TrafficType.Motorcycle, ColorIndex = 1, Y = 310 });
Save("traffic-motorcycle.png");
for (int i = 0; i < 300 * 60 && model.Phase != GamePhase.Crashing; i++) model.Update(1f / 60f);
for (int i = 0; i < 55; i++) model.Update(1f / 60f);
Save("crash.png");
for (int i = 0; i < 1800 && model.Phase != GamePhase.CrashOutro; i++) model.Update(1f / 60f);
if (model.Phase != GamePhase.CrashOutro) throw new Exception("Crash scene did not reach fade transition");
for (int i = 0; i < 36; i++) model.Update(1f / 60f);
Save("crash-outro.png");
for (int i = 0; i < 600 && model.Phase != GamePhase.Results; i++) model.Update(1f / 60f);
if (model.Phase != GamePhase.Results) throw new Exception("Crash clip did not finish into results");
typeof(GameModel).GetProperty("Score")!.SetValue(model, 5000);
Save("results.png");
foreach (var letter in new[] { Keys.M, Keys.A, Keys.P })
    keyHandler.Invoke(form, [null, new KeyEventArgs(letter)]);
string typed = (string)typeof(GameForm).GetField("typedInitials", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
if (typed != "MAP") throw new Exception("Typing initials failed: " + typed);
Save("typed-results.png");
