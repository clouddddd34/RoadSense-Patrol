using RoadSensePatrol;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}

Check(ScoreRules.Multiplier(0) == 1 && ScoreRules.Multiplier(2) == 1 &&
      ScoreRules.Multiplier(3) == 2 && ScoreRules.Multiplier(99) == 8, "streak multiplier thresholds and cap");
Check(ScoreRules.ResponsePoints(0.5f) == 150 && ScoreRules.ResponsePoints(1.2f) == 75 &&
      ScoreRules.ResponsePoints(2.1f) == 25, "warning response timing");
Check(ScoreRules.IsNearMiss(0, 1) && !ScoreRules.IsNearMiss(0, 2) &&
      !ScoreRules.IsNearMiss(1, 1), "near miss adjacency");
var a = RouteGenerator.Generate(4242, 75);
var b = RouteGenerator.Generate(4242, 75);
var c = RouteGenerator.Generate(4243, 75);
Check(a.SequenceEqual(b) && !a.SequenceEqual(c), "seeded route determinism");
Check(a.All(e => e.Lane is >= 0 and <= 2) && a.Any(e => e.Pickup.HasValue), "route lanes and pickups");
Check(a.Any(e => e.Hazard == HazardType.Patch), "route includes road patches");
Check(a.Any(e => e.Traffic == TrafficType.Car && e.Time < 38) &&
      a.Any(e => e.Traffic == TrafficType.Truck && e.Time >= 38) &&
      a.Where(e => e.Traffic.HasValue).All(e => e.ColorIndex is >= 0 and < 6),
    "cars arrive first, trucks and color variants appear later");
var longRoute = RouteGenerator.Generate(4242, 140);
Check(longRoute.Any(e => e.Traffic == TrafficType.Motorcycle && e.Time >= 28) &&
      longRoute.All(e => e.Traffic != TrafficType.Motorcycle || e.Time >= 28),
    "motorcycles join traffic later in the run");
Check(longRoute.Count(e => e.Traffic.HasValue && e.Time >= 85) >
      longRoute.Count(e => e.Traffic.HasValue && e.Time < 35),
    "traffic density increases during progression");
var model = new GameModel(new GameConfig { BoothSeed = 4242 });
model.Start();
for (int i = 0; i < 15 * 60; i++) model.Update(1f / 60f);
Check(model.Speed > 205 && model.Lives <= 3, "speed rises during an endless run");
var speedModel = new GameModel(new GameConfig { BoothSeed = 4242, StartingLives = 999 });
speedModel.Start();
Check(Math.Abs(speedModel.CruiseKph - 80) < .01f && Math.Abs(speedModel.DisplayKph - 80) < .01f,
    "speedometer starts at 80 km/h instead of using world scroll units");
for (int i = 0; i < 63 * 60; i++) speedModel.Update(1f / 60f);
Check(speedModel.Speed >= 325 && speedModel.Speed < 350,
    "speed has a visible rise after one minute of survival");
float cruiseSpeed = speedModel.Speed;
speedModel.AccelerateHeld = true;
speedModel.Update(1f / 60f);
Check(speedModel.EffectiveSpeed > speedModel.Speed &&
      speedModel.EffectiveSpeed < speedModel.Speed * 1.03f && speedModel.Speed > cruiseSpeed,
    "W begins accelerating without jumping to maximum speed");
for (int i = 0; i < 29; i++) speedModel.Update(1f / 60f);
Check(speedModel.BoostFraction > .3f && speedModel.BoostFraction < .4f &&
      speedModel.CarY < GameModel.PlayerY && speedModel.CarY > GameModel.BoostFrontY,
    "W gradually raises speed and moves the car forward");
for (int i = 0; i < 60; i++) speedModel.Update(1f / 60f);
Check(Math.Abs(speedModel.CarY - GameModel.BoostFrontY) < .01f &&
      speedModel.EffectiveSpeed > speedModel.Speed * 1.74f,
    "holding W drives the whole car to the top edge at higher speed");
Check(speedModel.DisplayKph > speedModel.CruiseKph && speedModel.DisplayKph <= 125,
    "W shows a temporary higher km/h reading");
speedModel.AccelerateHeld = false;
float boostSpeedAtRelease = speedModel.EffectiveSpeed;
speedModel.Update(1f / 60f);
Check(speedModel.EffectiveSpeed < boostSpeedAtRelease &&
      speedModel.EffectiveSpeed > speedModel.Speed && speedModel.Speed > cruiseSpeed,
    "releasing W starts a gradual return to current progression speed");
for (int i = 0; i < 89; i++) speedModel.Update(1f / 60f);
Check(Math.Abs(speedModel.EffectiveSpeed - speedModel.Speed) < .01f &&
      Math.Abs(speedModel.DisplayKph - speedModel.CruiseKph) < .01f &&
      Math.Abs(speedModel.CarY - GameModel.PlayerY) < .01f,
    "W release restores the current cruise speed and car position");
var steeringModel = new GameModel(new GameConfig { BoothSeed = 88 });
steeringModel.Start();
for (int i = 0; i < 190; i++) steeringModel.Update(1f / 60f);
steeringModel.ChangeLane(1);
steeringModel.Update(.08f);
Check(steeringModel.CarLane > 1 && steeringModel.CarLane < 2,
    "car moves visibly between lanes");
for (int i = 0; i < 30; i++) steeringModel.Update(1f / 60f);
Check(Math.Abs(steeringModel.CarLane - 2) < .001f,
    "car finishes lane change smoothly");
steeringModel.ChangeLane(-1);
steeringModel.Objects.Add(new RoadObject { Lane = 2, Hazard = HazardType.Pothole, Y = GameModel.PlayerY - 1 });
steeringModel.Update(1f / 60f);
Check(steeringModel.Lives == 2,
    "collision follows the visible car during a lane change");
for (int i = 0; i < 30; i++) steeringModel.Update(1f / 60f);
steeringModel.Objects.Add(new RoadObject { Lane = 2, Hazard = HazardType.Pothole, Y = GameModel.PlayerY - 1 });
steeringModel.Update(1f / 60f);
Check(steeringModel.Lives == 2,
    "completed lane change clears the former lane");
var boostCollision = new GameModel(new GameConfig { BoothSeed = 14, StartingLives = 99 });
boostCollision.Start();
for (int i = 0; i < 190; i++) boostCollision.Update(1f / 60f);
boostCollision.AccelerateHeld = true;
for (int i = 0; i < 65; i++) boostCollision.Update(1f / 60f);
boostCollision.Objects.Add(new RoadObject { Lane = 1, Hazard = HazardType.Pothole, Y = boostCollision.CarY - 1 });
boostCollision.Update(1f / 60f);
Check(boostCollision.Lives == 98, "boost collisions follow the car at the top of the road");
bool crashed = false;
int crashFrames = 0;
int crashOutroFrames = 0;
int crashSoundRequests = 0;
model.SoundRequested += name => { if (name == "crash") crashSoundRequests++; };
for (int i = 0; i < 300 * 60 && model.Phase != GamePhase.Results; i++)
{
    model.Update(1f / 60f);
    crashed |= model.Phase == GamePhase.Crashing;
    if (model.Phase == GamePhase.Crashing) crashFrames++;
    if (model.Phase == GamePhase.CrashOutro) crashOutroFrames++;
}
Check(crashed && model.Phase == GamePhase.Results && model.Lives == 0 && model.Score >= 0 &&
      model.Detected > 0 && model.NearMisses == 0 && crashFrames >= 570 && crashOutroFrames >= 45,
    "three lost lives play the full crash scene and fade into results");
Check(model.AccuracyPercent is >= 0 and <= 100,
    "results accuracy remains valid when hidden hazards are handled");
Check(crashSoundRequests == 1, "final lost life plays crash sound once");
var skipModel = new GameModel(new GameConfig { BoothSeed = 19, StartingLives = 1 });
skipModel.Start();
for (int i = 0; i < 190; i++) skipModel.Update(1f / 60f);
skipModel.Objects.Add(new RoadObject { Lane = 1, Hazard = HazardType.Pothole, Y = skipModel.CarY - 1 });
skipModel.Update(1f / 60f);
Check(skipModel.Phase == GamePhase.Crashing, "last life opens crash replay");
skipModel.SkipCrash();
Check(skipModel.Phase == GamePhase.Results, "crash replay can be skipped to results");
var mechanics = new GameModel(new GameConfig { BoothSeed = 99 });
mechanics.Start();
for (int i = 0; i < 190; i++) mechanics.Update(1f / 60f);
mechanics.Objects.Add(new RoadObject { Lane = 1, Pickup = PickupType.Shield, Y = GameModel.PlayerY - 1 });
mechanics.Update(1f / 60f);
Check(mechanics.Shield == 1, "shield pickup activates");
mechanics.Objects.Add(new RoadObject { Lane = 1, Hazard = HazardType.Pothole, Y = GameModel.PlayerY - 1 });
mechanics.Update(1f / 60f);
Check(mechanics.Shield == 0 && mechanics.Lives == 3, "shield absorbs a major pothole hit");
mechanics.Objects.Add(new RoadObject { Lane = 1, Pickup = PickupType.RepairCrew, Y = GameModel.PlayerY - 1 });
mechanics.Update(1f / 60f);
mechanics.Objects.Add(new RoadObject { Lane = 1, Hazard = HazardType.Pothole, Y = 60 });
mechanics.Update(1f / 60f);
Check(mechanics.RepairCrew == 0 && mechanics.Lives == 3, "repair crew clears the next hazard");
mechanics.Objects.Add(new RoadObject { Lane = 1, Pickup = PickupType.SignalBoost, Y = GameModel.PlayerY - 1 });
mechanics.Update(1f / 60f);
Check(mechanics.SignalBoost > 4.9f, "signal pickup extends scan range");
mechanics.ScanHeld = true;
float beforeScan = mechanics.ScanMeter;
for (int i = 0; i < 60; i++) mechanics.Update(1f / 60f);
Check(mechanics.ScanMeter < beforeScan, "scan boost drains meter");
mechanics.ScanHeld = false;
mechanics.SetBrake(true);
mechanics.Objects.Add(new RoadObject { Lane = 1, Hazard = HazardType.Hump, Y = GameModel.PlayerY - 1 });
mechanics.Update(1f / 60f);
Check(mechanics.Lives == 3, "braking makes a road hump safe");
mechanics.SetBrake(false);
mechanics.Objects.Add(new RoadObject { Lane = 1, Hazard = HazardType.Hump, Y = GameModel.PlayerY - 1 });
mechanics.Update(1f / 60f);
Check(mechanics.Lives == 2, "hitting a road hump without braking costs a life");
var patchModel = new GameModel(new GameConfig { BoothSeed = 99 });
patchModel.Start();
for (int i = 0; i < 190; i++) patchModel.Update(1f / 60f);
patchModel.Objects.Add(new RoadObject { Lane = 1, Hazard = HazardType.Patch, Y = GameModel.PlayerY - 1 });
patchModel.Update(1f / 60f);
patchModel.Update(1f / 60f);
Check(patchModel.Lives == 3 && patchModel.PatchSlowSeconds > 0 && patchModel.EffectiveSpeed < patchModel.Speed,
    "crossing road patch slows car without costing a life");
patchModel.SetBrake(true);
patchModel.Objects.Add(new RoadObject { Lane = 1, Hazard = HazardType.Patch, Y = GameModel.PlayerY - 1 });
patchModel.Update(1f / 60f);
Check(patchModel.Handled == 1 && patchModel.Missed == 1,
    "braking handles road patch cleanly");
var warningModel = new GameModel(new GameConfig { BoothSeed = 1 });
warningModel.Start();
for (int i = 0; i < 190; i++) warningModel.Update(1f / 60f);
warningModel.Objects.Add(new RoadObject { Lane = 0, Hazard = HazardType.Pothole, Y = 270, Warned = true });
warningModel.Objects.Add(new RoadObject { Lane = 2, Hazard = HazardType.MinorPothole, Y = 400, Warned = true });
warningModel.Update(1f / 60f);
Check(warningModel.WarningType == HazardType.Pothole, "warning panel prioritizes major pothole");
var trafficModel = new GameModel(new GameConfig { BoothSeed = 7 });
trafficModel.Start();
for (int i = 0; i < 190; i++) trafficModel.Update(1f / 60f);
trafficModel.Objects.Add(new RoadObject { Lane = 0, Traffic = TrafficType.Car, ColorIndex = 2,
    Y = GameModel.PlayerY + 71 });
trafficModel.Update(1f / 60f);
Check(trafficModel.TrafficPassed == 1 && trafficModel.Lives == 3, "passing a car in another lane is safe");
trafficModel.Objects.Add(new RoadObject { Lane = 1, Traffic = TrafficType.Truck, ColorIndex = 4,
    Y = GameModel.PlayerY - 1 });
trafficModel.Update(1f / 60f);
Check(trafficModel.TrafficCollisions == 1 && trafficModel.Lives == 2, "truck collision costs one life");
var driverModel = new GameModel(new GameConfig { BoothSeed = 15 });
driverModel.Start();
for (int i = 0; i < 190; i++) driverModel.Update(1f / 60f);
var avoidingCar = new RoadObject { Lane = 1, Traffic = TrafficType.Car, Y = 300 };
driverModel.Objects.Add(avoidingCar);
driverModel.Objects.Add(new RoadObject { Lane = 1, Hazard = HazardType.Pothole, Y = 340 });
driverModel.Update(1f / 60f);
Check(avoidingCar.Lane != 1 && avoidingCar.VisualLane is > 0 and < 2,
    "traffic steers smoothly away from a major pothole");
var minorCar = new RoadObject { Lane = 2, Traffic = TrafficType.Car, Y = 190 };
driverModel.Objects.Add(minorCar);
driverModel.Objects.Add(new RoadObject { Lane = 2, Hazard = HazardType.MinorPothole, Y = 210 });
driverModel.Update(1f / 60f);
Check(minorCar.Lane == 2, "traffic does not swerve for a minor pothole");
var radarModel = new GameModel(new GameConfig { BoothSeed = 17, StartingLives = 99 });
radarModel.Start();
for (int i = 0; i < 190; i++) radarModel.Update(1f / 60f);
var sideHazard = new RoadObject { Lane = 0, Hazard = HazardType.Pothole, Y = 300 };
var forwardHazard = new RoadObject { Lane = 1, Hazard = HazardType.MinorPothole, Y = 300 };
radarModel.Objects.Add(sideHazard);
radarModel.Objects.Add(forwardHazard);
radarModel.Update(1f / 60f);
Check(forwardHazard.Warned && !sideHazard.Warned,
    "normal radar reveals only road distress in the player's lane");
radarModel.ScanHeld = true;
radarModel.Update(1f / 60f);
Check(sideHazard.Warned && radarModel.RadarWide,
    "scan boost reveals road distress in side lanes");
radarModel.ScanHeld = false;
radarModel.Update(1f / 60f);
Check(!radarModel.IsScanning && radarModel.ScanCooldownRemaining > 2.9f &&
      radarModel.ScanCooldownProgress < .05f,
    "releasing scan starts the visible cooldown");
float energyDuringCooldown = radarModel.ScanMeter;
radarModel.ScanHeld = true;
for (int i = 0; i < 60; i++) radarModel.Update(1f / 60f);
Check(!radarModel.IsScanning && radarModel.ScanMeter > energyDuringCooldown &&
      radarModel.ScanCooldownProgress > .3f,
    "repeated scan input cannot bypass cooldown while energy recovers");
radarModel.ScanHeld = false;
for (int i = 0; i < 130; i++) radarModel.Update(1f / 60f);
radarModel.ScanHeld = true;
radarModel.Update(1f / 60f);
Check(radarModel.IsScanning && radarModel.ScanCooldownRemaining == 0,
    "scanner works again after cooldown");
var drainedScan = new GameModel(new GameConfig { BoothSeed = 18, StartingLives = 999 });
drainedScan.Start();
for (int i = 0; i < 190; i++) drainedScan.Update(1f / 60f);
drainedScan.ScanHeld = true;
for (int i = 0; i < 260; i++) drainedScan.Update(1f / 60f);
Check(!drainedScan.IsScanning && drainedScan.ScanCooldownRemaining > 4.5f &&
      drainedScan.ScanMeter < .05f,
    "empty scanner energy triggers a longer cooldown");
var bikeModel = new GameModel(new GameConfig { BoothSeed = 17, StartingLives = 99 });
bikeModel.Start();
for (int i = 0; i < 190; i++) bikeModel.Update(1f / 60f);
var motorcycle = new RoadObject { Lane = 1, Traffic = TrafficType.Motorcycle, Y = 220,
    SwerveCooldown = 0, ColorIndex = 0 };
bikeModel.Objects.Add(motorcycle);
bikeModel.Update(1f / 60f);
Check(motorcycle.Lane == 2 && motorcycle.SwerveCount == 1,
    "motorcycle suddenly chooses an adjacent lane");
for (int i = 0; i < 12; i++) bikeModel.Update(1f / 60f);
Check(motorcycle.VisualLane > 1.8f,
    "motorcycle visibly swerves into its new lane");
var boardDir = Path.Combine(Path.GetTempPath(), "RoadSensePatrolTests", Guid.NewGuid().ToString("N"));
var board = new LeaderboardStore(boardDir);
board.Add("bob", 3000);
board.Add("ace", 4000);
board.Add("Sebastian Penaranda", 5000);
board.Add("ABCDEFGHIJKLMNOPQRSTUVW", 2000);
Check(new LeaderboardStore(boardDir).Entries.Select(e => e.Name).SequenceEqual(
    ["Sebastian Penaranda", "ace", "bob", "ABCDEFGHIJKLMNOPQRST"]),
    "leaderboard preserves names, older initials, and the 20-character limit");
File.WriteAllText(Path.Combine(boardDir, "leaderboard.json"), "corrupt json");
Check(new LeaderboardStore(boardDir).Entries.Count == 0, "corrupt leaderboard is handled");
Directory.Delete(boardDir, true);
