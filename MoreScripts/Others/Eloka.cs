/* ELOKA v23.08.2019 */

const int MAX_PAGE = 5;
const int PANEL_UPDATE_EXECUTION_COUNT = 3;

static readonly Color HOSTRILE_COLOR = new Color(255, 0, 0);
static readonly Color OWN_COLOR = new Color(0, 60, 255);
static readonly Color NEUTRAL_COLOR = new Color(255, 255, 255);
static readonly Color FACTION_COLOR = new Color(65, 255, 65);
static readonly Color NOTHING_COLOR = new Color(60, 60, 0);
static readonly Color ERROR_COLOR = new Color(60, 0, 60);

private readonly EchoIO echoIO;

private readonly LcdUpdateManager lcdUpdateManager;

private readonly Configuration configuration;
private readonly GravityDrive gravityDrive;
private readonly RemoteControl remoteControl;
private readonly RaycastControl raycastControl;
private readonly TargetControl targetControl;
private readonly LocationControl locationControl;
private readonly PlanetControl planetControl;
private readonly RotationControl rotationControl;
private readonly StartAndLandingControl startAndLandingControl;

private IMyShipController randomShipController = null;

private List<IMyThrust> thrustersBackwards = null;
private List<IMyThrust> thrustersUp = null;
private List<IMyThrust> thrustersDown = null;

private int executionCount = 0;

private bool autoStabilizingEnabled = false;

private bool stabilizeActive = false;
private bool landingActive = false;
private bool startingActive = false;

private int tickExecutionCount = 0;
private double avg = 0;

Program() {
    echoIO = new EchoIO(this);

    configuration = new MyConfiguration(Me);
    configuration.save();

    string shipNameString = configuration.get("SHIP_NAME").getStringValue();
    string separatorString = configuration.get("SEPARATOR").getStringValue();
    string cameraString = configuration.get("CAMERA_NAME").getStringValue();

    IMyCameraBlock camera = GridTerminalSystem.GetBlockWithName(shipNameString + separatorString + cameraString) as IMyCameraBlock;

    if (camera != null)
    {
        camera.EnableRaycast = true;
        echoIO.Echo("Camera initialized!");
    }

    lcdUpdateManager = new LcdUpdateManager(this, echoIO, GridTerminalSystem, configuration);

    locationControl = new LocationControl(Me, camera, echoIO);
    planetControl = new PlanetControl(echoIO, locationControl, configuration);

    targetControl = new TargetControl(Me, GridTerminalSystem, configuration, locationControl, planetControl, lcdUpdateManager, echoIO);

/* There is a stupid hack that requires us to use the target location as the "back" coordinate in one certain use case. Will have to deal with it some day. */
    locationControl.setTargetControl(targetControl);

    raycastControl = new RaycastControl(Me, camera, targetControl, echoIO, configuration);
    gravityDrive = new GravityDrive(echoIO, lcdUpdateManager, GridTerminalSystem, configuration);
    remoteControl = new RemoteControl(GridTerminalSystem, targetControl, planetControl, locationControl, configuration, echoIO);

    initShipController();
    initThrusters();

    autoStabilizingEnabled = configuration.get("AutostabilizingEnabled").getBooleanValue();
    if (autoStabilizingEnabled)
        Echo("Auto stabilizing re-enabled!");

    rotationControl = new RotationControl(GridTerminalSystem, Me.CubeGrid,
        configuration.get("AUSRICHTUNG_CTRL_COEFF").getDoubleValue());

    startAndLandingControl = new StartAndLandingControl(GridTerminalSystem, Me.CubeGrid,
        configuration.get("STARTING_HOVER_MODE_ACTIVATION_MS").getFloatValue(),
        configuration.get("STARTING_ADJUST_THRUST_LIMIT_MS").getFloatValue());

    if (configuration.get("AUTO_STOP_SHIP_WITHOUT_PILOT").getBooleanValue())
        checkEmergencyShutdown(); //Stopping in case Servercrash happened

    if (configuration.get("SleepMode").getBooleanValue())
    {
        Runtime.UpdateFrequency = UpdateFrequency.None;
        Echo("SLEEP MODE!");
    }
    else if (configuration.get("FastMode").getBooleanValue())
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update1;
        Echo("FAST MODE!");
    }
    else
    {
/* Execute every 100 ticks */
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
    }
}

public class MyConfiguration : Configuration
{
    public MyConfiguration(IMyProgrammableBlock me)
        : base(me)
    {
    }

    protected override void createBaseConfiguration(Dictionary<string, AbstractConfigurationEntry> dictionary)
    {
        dictionary.Add("SHIP_NAME", new StringConfigurationEntry(""));
        dictionary.Add("SEPARATOR", new StringConfigurationEntry(""));
        dictionary.Add("MAX_SPEED_MS", new DoubleConfigurationEntry(100));
        dictionary.Add("CAMERA_NAME", new StringConfigurationEntry("Camera"));
        dictionary.Add("LIGHT_NAME", new StringConfigurationEntry("Report Light"));
        dictionary.Add("PANELS", new StringConfigurationEntry("Panels Eloka"));
        dictionary.Add("REMOTE_NAME", new StringConfigurationEntry("Remote Control"));
        dictionary.Add("MAX_DISTANCE_PLANETARY_TARGETING_WAYPOINTS", new IntegerConfigurationEntry(5000));
        dictionary.Add("MIN_SUPERCAST", new FloatConfigurationEntry(-0.5f));
        dictionary.Add("MAX_SUPERCAST", new FloatConfigurationEntry(0.5f));
        dictionary.Add("SUPERCAST_STEPS", new FloatConfigurationEntry(0.25f));
        dictionary.Add("SUPERCAST_DEFAULT_DISTANCE", new DoubleConfigurationEntry(5000));
        dictionary.Add("RAYCAST_DEFAULT_DISTANCE", new DoubleConfigurationEntry(10000));
        dictionary.Add("GRAVITY_DRIVE_GROUP_NAME", new StringConfigurationEntry("Gravi Drive"));
        dictionary.Add("GRAVITY_DRIVE_IDENTIFIER", new StringConfigurationEntry("Drive"));
        dictionary.Add("AUSRICHTUNG_CTRL_COEFF", new DoubleConfigurationEntry(0.8));
        dictionary.Add("STARTING_HOVER_MODE_ACTIVATION_MS", new FloatConfigurationEntry(99.0f));
        dictionary.Add("STARTING_ADJUST_THRUST_LIMIT_MS", new FloatConfigurationEntry(90.0f));
        dictionary.Add("AUTO_STOP_SHIP_WITHOUT_PILOT", new BooleanConfigurationEntry(true));

        dictionary.Add("Target", new StringConfigurationEntry(""));
        dictionary.Add("Planet", new StringConfigurationEntry(""));
        dictionary.Add("AutostabilizingEnabled", new BooleanConfigurationEntry(false));
        dictionary.Add("PlanetManuallySet", new BooleanConfigurationEntry(false));
        dictionary.Add("SleepMode", new BooleanConfigurationEntry(false));
        dictionary.Add("FastMode", new BooleanConfigurationEntry(false));
        dictionary.Add("Page", new IntegerConfigurationEntry(1));
    }
}

public void Main(string arg, UpdateType updateType)
{
    if (avg == 0.0)
        avg = Runtime.LastRunTimeMs;

    avg = avg * 0.99 + Runtime.LastRunTimeMs * 0.01;

    if (Runtime.UpdateFrequency == UpdateFrequency.None)
        Echo("SLEEP MODE!");
    if (Runtime.UpdateFrequency == UpdateFrequency.Update1 && updateType == UpdateType.Terminal)
        Echo("FAST MODE!");

    if (updateType == UpdateType.Update1)
    {
        performAutoUpdate1();
        return;
    }

/* Every 100 Ticks check for emergency */
    if (updateType == UpdateType.Update100)
    {
        performAutoUpdate();
        return;
    }

    if (updateType == UpdateType.Update10)
    {
        if (stabilizeActive && rotationControl.run())
            stabilizeActive = false;

        if (landingActive && startAndLandingControl.runLanding())
            landingActive = false;

        if (startingActive && startAndLandingControl.runStarting())
            startingActive = false;

        if (!startingActive && !landingActive && !stabilizeActive)
        {
            if (configuration.get("SleepMode").getBooleanValue())
                Runtime.UpdateFrequency = UpdateFrequency.None;
            else
                Runtime.UpdateFrequency = UpdateFrequency.Update100;

            tickExecutionCount = 0;
        }
        else if (tickExecutionCount++ >= 10)
        {
            performAutoUpdate();
            tickExecutionCount = 0;
        }

        return;
    }

    if (arg != null)
        arg = arg.ToLower().Trim();

    if (arg == "save")
    {
        configuration.save();
    }
    else if (arg == "avg")
    {
        Echo(avg + "");
    }
    else if (arg == "sleep on")
    {
/* To prevent stopping an active stabilizing process. */
        if (!stabilizeActive && !startingActive && !landingActive)
            Runtime.UpdateFrequency = UpdateFrequency.None;

        configuration.get("SleepMode").setBooleanValue(true);

        Echo("Sleep Mode enabled!");

        configuration.save();
    }
    else if (arg == "sleep off")
    {
        if (Runtime.UpdateFrequency == UpdateFrequency.None)
        {
            if (configuration.get("FastMode").getBooleanValue())
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            else
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        configuration.get("SleepMode").setBooleanValue(false);

        Echo("Sleep Mode disabled!");

        configuration.save();
    }
    else if (arg == "sleep")
    {
        var configurationSleep = configuration.get("SleepMode");

        bool currentSetting = configurationSleep.getBooleanValue();
        configurationSleep.setBooleanValue(!currentSetting);

        if (!currentSetting)
        {
            if (!stabilizeActive && !startingActive && !landingActive)
                Runtime.UpdateFrequency = UpdateFrequency.None;

            Echo("Sleep Mode enabled!");
        }
        else
        {
            if (Runtime.UpdateFrequency == UpdateFrequency.None)
            {
                if (configuration.get("FastMode").getBooleanValue())
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                else
                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            Echo("Sleep Mode disabled!");
        }

        configuration.save();
    }
    else if (arg == "fast on")
    {
/* When sleeping dont wake */
        if (!configuration.get("SleepMode").getBooleanValue())
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

        configuration.get("FastMode").setBooleanValue(true);

        Echo("Fast Mode enabled!");

        configuration.save();
    }
    else if (arg == "fast off")
    {
        if (Runtime.UpdateFrequency == UpdateFrequency.Update1)
        {
/* stabilizing put to 10 */
            if (stabilizeActive || startingActive || !landingActive)
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
            else
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        configuration.get("FastMode").setBooleanValue(false);

        Echo("Fast Mode disabled!");

        configuration.save();
    }
    else if (arg == "fast")
    {
        var configurationFast = configuration.get("FastMode");

        bool currentSetting = configurationFast.getBooleanValue();
        configurationFast.setBooleanValue(!currentSetting);

        if (!currentSetting)
        {
            if (!configuration.get("SleepMode").getBooleanValue())
                Runtime.UpdateFrequency = UpdateFrequency.Update1;

            Echo("Fast Mode enabled!");
        }
        else
        {
            if (Runtime.UpdateFrequency == UpdateFrequency.Update1)
            {
                if (stabilizeActive || startingActive || !landingActive)
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                else
                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            Echo("Fast Mode disabled!");
        }

        configuration.save();
    }
    else if (arg == "me")
    {
        locationControl.resetOverrideLocation();
    }
    else if (arg.StartsWith("me:"))
    {
        arg = arg.Replace("me:", "").Trim();

        locationControl.setOverrideLocation(arg);
    }
    else if (arg == "target")
    {
        targetControl.resetTarget();
    }
    else if (arg == "planet")
    {
        planetControl.removePlanet();
    }
    else if (arg.StartsWith("planet:"))
    {
        arg = arg.Replace("planet:", "").Trim();

        planetControl.setPlanetWithGps(arg);
    }
    else if (arg == "height")
    {
        planetControl.resetHeightOverride();
    }
    else if (arg.StartsWith("height:"))
    {
        arg = arg.Replace("height:", "").Trim();

        planetControl.setHeightOverrideString(arg);
    }
    else if (arg.StartsWith("gps:"))
    {
        targetControl.setTargetString(arg);
    }
    else if (arg == "raycast")
    {
        raycastControl.raycast(true);
    }
    else if (arg.StartsWith("raycast:"))
    {
        arg = arg.Replace("raycast:", "").Trim();

        double distance = 10000;
        double.TryParse(arg, out distance);

        raycastControl.raycast(0f, 0f, true, distance);
    }
    else if (arg == "raycasthit")
    {
        raycastControl.raycast(true);
    }
    else if (arg.StartsWith("raycasthit:"))
    {
        arg = arg.Replace("raycasthit:", "").Trim();

        double distance = 10000;
        double.TryParse(arg, out distance);

        raycastControl.raycast(0f, 0f, false, distance);
    }
    else if (arg == "supercast")
    {
        raycastControl.supercast();
    }
    else if (arg.StartsWith("supercast:"))
    {
        arg = arg.Replace("supercast:", "").Trim();

        double distance = 10000;
        double.TryParse(arg, out distance);

        raycastControl.supercast(distance);
    }
    else if (arg.StartsWith("move:"))
    {
        arg = arg.Replace("move:", "").Trim();

        targetControl.moveTargetString(arg);
    }
    else if (arg.StartsWith("range:"))
    {
        arg = arg.Replace("range:", "").Trim();

        targetControl.getTargetInRangeString(arg);
    }
    else if (arg == "takea")
    {
        targetControl.takeA();
    }
    else if (arg == "takeb")
    {
        targetControl.takeB();
    }
    else if (arg == "start space")
    {
        remoteControl.startSpace();
    }
    else if (arg == "start planet")
    {
        remoteControl.startPlanet();
    }
    else if (arg == "stop")
    {
        gravityDrive.stop();
        remoteControl.stop();
        rotationControl.stop();
        startAndLandingControl.stop();

        landingActive = false;
        startingActive = false;

        if (configuration.get("SleepMode").getBooleanValue())
            Runtime.UpdateFrequency = UpdateFrequency.None;
        else if (!configuration.get("FastMode").getBooleanValue())
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

        Echo("Started to stop ship!");
    }
    else if (arg == "info")
    {
        printInfo(echoIO, 0);
    }
    else if (arg == "next")
    {
        lcdUpdateManager.nextPage();
    }
    else if (arg == "prev")
    {
        lcdUpdateManager.prevPage();
    }
    else if (arg == "stabilize")
    {
        rotationControl.setup();
        stabilizeActive = true;

        var fastMode = configuration.get("FastMode").getBooleanValue();

        if (!fastMode)
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
    }
    else if (arg == "start stabilizing")
    {
        autoStabilizingEnabled = true;

        configuration.get("AutostabilizingEnabled").setBooleanValue(autoStabilizingEnabled);

        Echo("Auto stabilizing enabled!");

        configuration.save();
    }
    else if (arg == "stop stabilizing")
    {
        autoStabilizingEnabled = false;
        rotationControl.stop();

        configuration.get("AutostabilizingEnabled").setBooleanValue(autoStabilizingEnabled);

        Echo("Auto stabilizing disabled!");

        configuration.save();
    }
    else if (arg == "switch stabilizing")
    {
        autoStabilizingEnabled = !autoStabilizingEnabled;

        if (!autoStabilizingEnabled)
            rotationControl.stop();

        configuration.get("AutostabilizingEnabled").setBooleanValue(autoStabilizingEnabled);

        if (autoStabilizingEnabled)
            Echo("Auto stabilizing enabled!");
        else
            Echo("Auto stabilizing disabled!");

        configuration.save();
    }
    else if (arg == "brake")
    {
        if (thrustersBackwards == null)
            initThrusters();

        if (thrustersBackwards == null)
        {
            Echo("No backwards thrusters found!");
        }
        else
        {
            if (thrustersBackwards.Count == 0)
            {
                Echo("No backwards thrusters found!");
            }
            else
            {
                bool shouldEnable = !thrustersBackwards[0].Enabled;

                foreach (IMyTerminalBlock thruster in thrustersBackwards)
                {
                    if (shouldEnable)
                        thruster.ApplyAction("OnOff_On");
                    else
                        thruster.ApplyAction("OnOff_Off");
                }

                if (shouldEnable)
                    Echo("Backwards thrusters enabled!");
                else
                    Echo("Backwards thrusters disabled!");
            }
        }
    }
    else if (arg == "brake on")
    {
        if (thrustersBackwards == null)
            initThrusters();

        if (thrustersBackwards == null)
        {
            Echo("No backwards thrusters found!");
        }
        else
        {
            foreach (IMyTerminalBlock thruster in thrustersBackwards)
                thruster.ApplyAction("OnOff_On");

            Echo("Backwards thrusters enabled!");
        }
    }
    else if (arg == "brake off")
    {
        if (thrustersBackwards == null)
            initThrusters();

        if (thrustersBackwards == null)
        {
            Echo("No backwards thrusters found!");
        }
        else
        {
            foreach (IMyTerminalBlock thruster in thrustersBackwards)
                thruster.ApplyAction("OnOff_Off");

            Echo("Backwards thrusters disabled!");
        }
    }
    else if (arg == "emergency")
    {
        checkEmergencyShutdown();
    }
    else if (arg == "to orbit")
    {
        if (landingActive || startingActive)
        {
            Echo("Ship is already busy!");
        }
        else
        {
            rotationControl.setup();
            stabilizeActive = true;

            startAndLandingControl.setup();
            startingActive = true;

            var fastMode = configuration.get("FastMode").getBooleanValue();

            if (!fastMode)
                Runtime.UpdateFrequency = UpdateFrequency.Update10;

            Echo("Ship starts moving to orbit!");
        }
    }
    else if (arg.StartsWith("to orbit:"))
    {
        if (landingActive || startingActive)
        {
            Echo("Ship is already busy!");
        }
        else
        {
            arg = arg.Replace("to orbit:", "").Trim();

            double height = 0;
            double.TryParse(arg, out height);

            rotationControl.setup();
            stabilizeActive = true;

            startAndLandingControl.setup(height, true);
            startingActive = true;

            var fastMode = configuration.get("FastMode").getBooleanValue();

            if (!fastMode)
                Runtime.UpdateFrequency = UpdateFrequency.Update10;

            Echo("Ship starts moving to " + height.ToString("#,#00") + "m above ground!");
        }
    }
    else if (arg == "to ground")
    {
        if (landingActive || startingActive)
        {
            Echo("Ship is already busy!");
        }
        else
        {
            rotationControl.setup();
            stabilizeActive = true;

            startAndLandingControl.setup();
            landingActive = true;

            var fastMode = configuration.get("FastMode").getBooleanValue();

            if (!fastMode)
                Runtime.UpdateFrequency = UpdateFrequency.Update10;

            Echo("Ship starts moving to " + 1000.ToString("0,000") + "m above ground!");
        }
    }
    else if (arg.StartsWith("to ground:"))
    {
        if (landingActive || startingActive)
        {
            Echo("Ship is already busy!");
        }
        else
        {
            arg = arg.Replace("to ground:", "").Trim();

            double height = 1000;
            double.TryParse(arg, out height);

            rotationControl.setup();
            stabilizeActive = true;

            startAndLandingControl.setup(height, false);
            landingActive = true;

            var fastMode = configuration.get("FastMode").getBooleanValue();

            if (!fastMode)
                Runtime.UpdateFrequency = UpdateFrequency.Update10;

            Echo("Ship starts moving to " + height.ToString("#,#00") + "m above ground!");
        }
    }
    else if (arg == "pos beacon")
    {
        updateCoordinatesOnBeacon();
    }
    else if (arg.StartsWith("gravity drive:"))
    {
        arg = arg.Replace("gravity drive:", "").Trim();

        double accelerationInG = 0;
        double.TryParse(arg, out accelerationInG);

        gravityDrive.changeAcceleration(accelerationInG);
    }
    else if (arg == "print")
    {
        lcdUpdateManager.setNeedsUpdate();
    }
    else if (arg == "help")
    {
        printHelp();
    }
    else
    {
        Echo("No Arguments given!");

        printHelp();
    }

    lcdUpdateManager.updateIfNeeded();
}

private void performAutoUpdate1()
{
    tickExecutionCount++;

    if ((stabilizeActive || landingActive || startingActive)
        && tickExecutionCount % 10 == 0)
    {
        if (stabilizeActive && rotationControl.run())
            stabilizeActive = false;

        if (landingActive && startAndLandingControl.runLanding())
            landingActive = false;

        if (startingActive && startAndLandingControl.runStarting())
            startingActive = false;

        if (!startingActive && !landingActive && !stabilizeActive)
        {
            if (configuration.get("SleepMode").getBooleanValue())
            {
                Runtime.UpdateFrequency = UpdateFrequency.None;

                tickExecutionCount = 0;
            }
        }
    }

    if (tickExecutionCount == 100)
    {
        performAutoUpdate();
        tickExecutionCount = 0;
    }
}

private void performAutoUpdate()
{
    executionCount++;

/* Every Update we check for emergency Shutdown */
    if (configuration.get("AUTO_STOP_SHIP_WITHOUT_PILOT").getBooleanValue())
        checkEmergencyShutdown();

/* Every 5th execution we print new stuff to our Panels and update the planet */
    if (executionCount % PANEL_UPDATE_EXECUTION_COUNT == 0)
    {
        if (!planetControl.isPlanetManuallySet())
            updatePlanetLocation();

        if (autoStabilizingEnabled || startingActive || landingActive)
        {
            rotationControl.setup();
            stabilizeActive = true;

            var fastMode = configuration.get("FastMode").getBooleanValue();

            if (!fastMode)
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        lcdUpdateManager.setNeedsUpdate();
        lcdUpdateManager.updateIfNeeded();
    }
}

private void updatePlanetLocation()
{
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks);

    if (randomShipController == null)
        initShipController();

    if (randomShipController == null)
    {
        Echo("No ship controller for planet detection found!");
        return;
    }

    Vector3D planetLocation;
    if (!randomShipController.TryGetPlanetPosition(out planetLocation))
    {
/* If we already know we are not at near a planet then we dont need to notify us */
        if (planetControl.getPlanet() != null)
            planetControl.removePlanet();
    }
    else
    {
        if (planetControl.getPlanet() != planetLocation)
            planetControl.setPlanet(planetLocation);
    }
}

private void checkEmergencyShutdown()
{
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks);

    bool isUnderControl = false;

    IMyShipController randomController = null;

    foreach (IMyTerminalBlock block in blocks)
    {
        if (block.CubeGrid != Me.CubeGrid)
            continue;

/* When any controller is under control even if its a connected ship. everything is fine. */
        IMyShipController controller = block as IMyShipController;

        if (controller.CanControlShip && controller.ControlThrusters)
        {
            if (randomController == null)
                randomController = controller;

            if (controller.IsUnderControl)
            {
                isUnderControl = true;
                break;
            }
        }
    }

    if (!isUnderControl && randomController != null)
    {
        if (randomController.GetShipSpeed() > 1)
        {
            rotationControl.setup();
            stabilizeActive = true;

            if (!configuration.get("FastMode").getBooleanValue())
                Runtime.UpdateFrequency = UpdateFrequency.Update10;

/* If ship is moving then enable dampaners if not already enabled */
            if (!randomController.DampenersOverride)
            {
                randomController.DampenersOverride = true;
                Echo("Inertia Dampeners enabled!");
            }

            List<IMyTerminalBlock> emergencyBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(emergencyBlocks);

            foreach (IMyThrust thruster in emergencyBlocks)
            {
                if (thruster.CubeGrid != Me.CubeGrid)
                    continue;

                if (!landingActive && !startingActive)
                    thruster.ThrustOverride = 0;

                thruster.Enabled = true;
            }

            Echo("Thrusters enabled!");

            emergencyBlocks.Clear();

            GridTerminalSystem.GetBlocksOfType<IMyGyro>(emergencyBlocks);
            foreach (IMyTerminalBlock gyro in emergencyBlocks)
                if (gyro.CubeGrid == Me.CubeGrid)
                    gyro.ApplyAction("OnOff_On");

            Echo("Gyroscopes enabled!");

            emergencyBlocks.Clear();

            GridTerminalSystem.GetBlocksOfType<IMyReactor>(emergencyBlocks);
            foreach (IMyTerminalBlock reactor in emergencyBlocks)
                if (reactor.CubeGrid == Me.CubeGrid)
                    reactor.ApplyAction("OnOff_On");

            Echo("Reactors enabled!");

            emergencyBlocks.Clear();

            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(emergencyBlocks);
            foreach (IMyTerminalBlock battery in emergencyBlocks)
            {
                IMyBatteryBlock batteryBlock = battery as IMyBatteryBlock;
                batteryBlock.Enabled = true;
                batteryBlock.ChargeMode = ChargeMode.Auto;
            }

            Echo("Batteries enabled!");

            if (gravityDrive != null)
                gravityDrive.stop();
        }
    }
}

public static double DegreeToRadians(double angle)
{
    return (Math.PI / 180) * angle;
}

public static double RadianToDegree(double angle)
{
    double returnValue = angle * (180.0 / Math.PI);

    if (returnValue < 0)
        returnValue += 360;

    return returnValue;
}

public static string getTimeString(double time)
{
    string hoursStr, minutesStr, secondsStr;

    int hours = (int)Math.Floor(time / 60 / 60);
    time -= (hours * 60 * 60);
    int minutes = (int)Math.Floor(time / 60);
    time -= (minutes * 60);
    int seconds = (int)time;

    if (hours > 9)
        hoursStr = hours.ToString();
    else
        hoursStr = "0" + hours.ToString();

    if (minutes > 9)
        minutesStr = minutes.ToString();
    else
        minutesStr = "0" + minutes.ToString();

    if (seconds > 9)
        secondsStr = seconds.ToString();
    else
        secondsStr = "0" + seconds.ToString();

    return hoursStr + ":" + minutesStr + ":" + secondsStr;
}

public void updateCoordinatesOnBeacon()
{
    string shipNameString = configuration.get("SHIP_NAME").getStringValue() + ":";

    List<IMyTerminalBlock> beacons = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBeacon>(beacons);

    if (beacons.Count == 0)
        return;

    IMyTerminalBlock firstBlock = null;

    foreach (IMyTerminalBlock beacon in beacons)
        if (beacon.CustomName.StartsWith(shipNameString))
            firstBlock = beacon;

    if (firstBlock == null)
        return;

    Vector3D location = locationControl.getFrontPosition();

    double x = Math.Floor(location.X);
    double y = Math.Floor(location.Y);
    double z = Math.Floor(location.Z);

/* Rename the Beacon */
    firstBlock.CustomName = shipNameString
                            + " [X: " + x.ToString("#,###") + "; Y: "
                            + y.ToString("#,###") + "; Z: "
                            + z.ToString("#,###") + "]";
}

private double calcMaxUpThrust()
{
    if (thrustersUp == null)
        initThrusters();

    if (thrustersUp == null)
        return 0;

    double maxThrust = 0;
    foreach (IMyThrust thrust in thrustersUp)
        maxThrust += thrust.MaxEffectiveThrust;

    return maxThrust;
}

private double calcMaxBackThrust()
{
    if (thrustersBackwards == null)
        initThrusters();

    if (thrustersBackwards == null)
        return 0;

    double maxThrust = 0;
    foreach (IMyThrust thrust in thrustersBackwards)
        maxThrust += thrust.MaxEffectiveThrust;

    return maxThrust;
}

public void printInfo(IO io, int page)
{
    double maxSpeed = configuration.get("MAX_SPEED_MS").getDoubleValue();

    if (randomShipController == null)
        initShipController();

    double currentSpeed = 0;
    string dampenersString = "Unknown";

    if (randomShipController != null)
    {
        currentSpeed = randomShipController.GetShipSpeed();

        if (randomShipController.DampenersOverride)
            dampenersString = "Active";
        else
            dampenersString = "Inactive";
    }

    if (page == 1 || page == 0)
    {
        io.EchoTitle("My Location:");

        locationControl.printInfo(io);

        io.Echo("");

        string autoStabilizingString = "Inactive";
        if (autoStabilizingEnabled)
            autoStabilizingString = "Active";

        io.Echo("   Speed: " + currentSpeed.ToString("#,##0.0#") + " m/s");
        io.Echo("   Dampeners: " + dampenersString);
        io.Echo("   Auto Stabilizing: " + autoStabilizingString);
        io.Echo("");

        remoteControl.printInfo(io);
        gravityDrive.printInfo(io);

        io.Echo("");
    }

    if (page == 2 || page == 0)
    {
        io.EchoTitle("Target Location:");

        targetControl.printInfo(io, currentSpeed);

        io.Echo("");
    }

    if (page == 3 || page == 0)
    {
        io.EchoTitle("Raycast Location:");

        raycastControl.printInfo(io);

        io.Echo("");
    }

    if (page == 4 || page == 0)
    {
        io.EchoTitle("Planet Location:");

        planetControl.printInfo(io);

        io.Echo("");
    }

    if (page == 5 || page == 0)
    {
        io.EchoTitle("Ship Status:");

        if (randomShipController == null)
            initShipController();

        if (randomShipController == null)
        {
            io.Echo("   Status unknown");
        }
        else
        {
            MyShipMass shipMass = randomShipController.CalculateShipMass();

            io.Echo("   Base Mass: " + shipMass.BaseMass.ToString("#,##0.00") + " kg");
            io.Echo("   Real Mass: " + shipMass.PhysicalMass.ToString("#,##0.00") + " kg");
            io.Echo("   Total Mass: " + shipMass.TotalMass.ToString("#,##0.00") + " kg");

            io.Echo("");

            Vector3D gravityVector = randomShipController.GetNaturalGravity();
            double gravity = gravityVector.Normalize();

            Vector3D shipVelocityVec = randomShipController.GetShipVelocities().LinearVelocity;

            if (gravity != 0)
            {
                double downSpeed = VectorProjection(shipVelocityVec, gravityVector).Length() * Math.Sign(shipVelocityVec.Dot(gravityVector));

                double newton = shipMass.PhysicalMass * gravity;
                double remainingNewtons = (calcMaxUpThrust() - newton);

                io.Echo("   Natural Gravity: " + gravity.ToString("#,##0.00") + " m/s^2");
                io.Echo("   Hovering Thrust: " + newton.ToString("#,##0.00") + " N");
                io.Echo("   Remaining Thrust: " + remainingNewtons.ToString("#,##0.00") + " N");

                double deceleration = remainingNewtons / shipMass.PhysicalMass;
                double bremsZeit = downSpeed / deceleration;
                double bremsWeg = 0.5 * deceleration * (bremsZeit * bremsZeit);

                io.Echo("");

                io.Echo("   Down Speed: " + downSpeed.ToString("#,##0.00") + " m/s");
                io.Echo("   Landing Time: " + bremsZeit.ToString("#,##0.00") + " s");
                io.Echo("   Landing Distance: " + bremsWeg.ToString("#,##0.00") + " m");
            }

            Vector3D forwardDirection = randomShipController.WorldMatrix.Forward;
            double forwardSpeed = VectorProjection(shipVelocityVec, forwardDirection).Length() * Math.Sign(shipVelocityVec.Dot(forwardDirection));

            double remainingNewtonsBack = (calcMaxBackThrust());
            double decelerationBack = remainingNewtonsBack / shipMass.PhysicalMass;
            double bremsZeitBack = forwardSpeed / decelerationBack;
            double bremsWegBack = 0.5 * decelerationBack * (bremsZeitBack * bremsZeitBack);

            io.Echo("   Forward Speed: " + forwardSpeed.ToString("#,##0.00") + " m/s");
            io.Echo("   Breaking Time: " + bremsZeitBack.ToString("#,##0.00") + " s");
            io.Echo("   Breaking Distance: " + bremsWegBack.ToString("#,##0.00") + " m");

            io.Echo("");
        }
    }
}

public static Vector3D VectorProjection(Vector3D a, Vector3D b)
{
    return a.Dot(b) / b.LengthSquared() * b;
}

private void printHelp()
{
    Echo("Supportet Arguments:");
    Echo("---------------------------------------");
    Echo("Save");
    Echo("Saves all unsaved settings to the Custom Data. Also reverts changes made to it that are not yet used by the script.");
    Echo("Example: Save");
    Echo("");
    Echo("Sleep On");
    Echo("Puts the script into hibernation. You can still manually execute things, but it will not execute automatically. So no emergency or stability checks and no updates of lcd panels etc.");
    Echo("Example: Sleep On");
    Echo("");
    Echo("Sleep Off");
    Echo("Restores the scripts standard update cycle.");
    Echo("Example: Sleep Off");
    Echo("");
    Echo("Sleep");
    Echo("Switches between sleep and not wake mode.");
    Echo("Example: Sleep");
    Echo("");
    Echo("Fast On");
    Echo("Executes the Script each tick, but behaves like it runs every 10 or 100 ticks. This is to reduce the average execution time to trick some server mods.");
    Echo("Example: Fast On");
    Echo("");
    Echo("Fast Off");
    Echo("Restores the scripts standard update cycle.");
    Echo("Example: Fast Off");
    Echo("");
    Echo("Fast");
    Echo("Switches between fast and default mode.");
    Echo("Example: Fast");
    Echo("");
    Echo("Me");
    Echo("Resets the current location to use the own coordinates.");
    Echo("Example: Me");
    Echo("");
    Echo("Me: <GPS>");
    Echo("Used for advanced vector-calculation. Changes our coordinates to the one provided.");
    Echo("Example: Me:GPS:Center:0:0:0:");
    Echo("");
    Echo("Planet");
    Echo("Resets the planets Location to none.");
    Echo("Example: Planet");
    Echo("");
    Echo("Planet: <GPS>");
    Echo("Sets the planet center location. Used for spherical distance calculation.");
    Echo("Example: Planet:GPS:Center:0:0:0:");
    Echo("");
    Echo("Height");
    Echo("Resets the distance to the planets center back to default.");
    Echo("Example: Height");
    Echo("");
    Echo("Height: <distance>");
    Echo("Overrides the distance to planet center. Useful for calculation a course for the remote control around the planet.");
    Echo("Example: Height:65000");
    Echo("");
    Echo("<GPS>");
    Echo("Used for advanced vector-calculation. Used for creating a vector from our location to that target.");
    Echo("Example: GPS:Center:0:0:0:");
    Echo("");
    Echo("Target");
    Echo("Removes the current Target and clears custom Data and renames the Programmable Block.");
    Echo("Example: Target");
    Echo("");
    Echo("Raycast");
    Echo("Performances a raycast with the camera with default range of 10,000m.");
    Echo("Example: Raycast");
    Echo("");
    Echo("Raycast: <distance>");
    Echo("Performances a raycast with the camera with provded range in meters.");
    Echo("Example: Raycast: 20000");
    Echo("");
    Echo("RaycastHit");
    Echo("Same as Raycast, but instead of the center of the object it returns the location that was hit by the ray.");
    Echo("Example: RaycastHit");
    Echo("");
    Echo("RaycastHit: <distance>");
    Echo("Same as Raycast: <distance>, but instead of the center of the object it returns the location that was hit by the ray.");
    Echo("Example: RaycastHit");
    Echo("");
    Echo("Supercast");
    Echo("Performances several raycasts on different angles with the camera with default range of 5,000m. Planets are ignored!");
    Echo("Example: Supercast");
    Echo("");
    Echo("Supercast: <distance>");
    Echo("Performances several raycasts on different angles with the camera with provded range in meters. Planets are ignored!");
    Echo("Example: Supercast: 20000");
    Echo("");
    Echo("Move: <distance>");
    Echo("Moves the caluclated target coordinates along the vector we look at.");
    Echo("Example: Move: 20000");
    Echo("");
    Echo("Range: <distance>");
    Echo("Calculates the target coordinates of the point we look at with the provided distance.");
    Echo("Example: Range: 20000");
    Echo("");
    Echo("TakeA");
    Echo("Remembers the current vector we look at.");
    Echo("Example: TakeA");
    Echo("");
    Echo("TakeB");
    Echo("Must only be used after \"TakeA\". Takes the vector we look and calculates the target where the vectors cross each other.");
    Echo("Example: TakeB");
    Echo("");
    Echo("Stabilize");
    Echo("Only works in natural gravity. Aligns the ship with the horizon so that your ship can hit the brakes.");
    Echo("Example: Stabilize");
    Echo("");
    Echo("Start Stabilizing");
    Echo("Starts auto stabilizing the ship every 300 ticks (60 ticks = 1 second). Only has an affect when in natural gravity.");
    Echo("Example: Start Stabilizing");
    Echo("");
    Echo("Stop Stabilizing");
    Echo("Stops the ongoing auto stabilizing of your ship.");
    Echo("Example: Stop Stabilizing");
    Echo("");
    Echo("Switch Stabilizing");
    Echo("Enabled or disabled auto stabilizing of your ship depending on the state it previously was at.");
    Echo("Example: Switch Stabilizing");
    Echo("");
    Echo("Brake");
    Echo("Switches brake mode and enables or disables all thrusters which are in the backwards group.");
    Echo("Example: Brake");
    Echo("");
    Echo("Brake On");
    Echo("Enabled all thrusters in backwards group.");
    Echo("Example: Brake On");
    Echo("");
    Echo("Brake Off");
    Echo("Disables all thrusters in backwards group.");
    Echo("Example: Brake Off");
    Echo("");
    Echo("Emergency");
    Echo("Checks if any player is in control of the ship. If not thrusters, gyros and dampeners will be enabled. Normally this executes automatically, but when in sleep mode this command offers manual updates.");
    Echo("Example: Emergency");
    Echo("");
    Echo("Start Space");
    Echo("Sets the current Target into the defined remote control and enables the auto pilot to reach the target in a straight line.");
    Echo("Example: Start Space");
    Echo("");
    Echo("Start Planet");
    Echo("Calculates the coordinates to guide the remote control around the planet to reach the target. Planet must be set first");
    Echo("Example: Start Planet");
    Echo("");
    Echo("Stop");
    Echo("Stops the autopilot and removes the waypoints from the remote control.");
    Echo("Example: stop");
    Echo("");
    Echo("To Orbit");
    Echo("Only Works in planetary gravity. Brings your ship up into space!");
    Echo("Example: To Orbit");
    Echo("");
    Echo("To Orbit: <height>");
    Echo("Only Works in planetary gravity. Brings your ship up to the specified height!");
    Echo("Example: To Orbit: 5000");
    Echo("");
    Echo("To Ground");
    Echo("Only Works in planetary gravity. Brings your ship down 1000m above the ground!");
    Echo("Example: To Ground");
    Echo("");
    Echo("To Ground: <height>");
    Echo("Only Works in planetary gravity. Brings your ship down to the specified meters above the ground!");
    Echo("Example: To Ground: 100");
    Echo("");
    Echo("Pos Beacon");
    Echo("Writes the current location to the Beacon.");
    Echo("Example: stop");
    Echo("");
    Echo("Gravity Drive: <Acceleration>");
    Echo("Starts, stops or reverses the gravity drive by the given acceleration in fractions of G (Example 1 = 9.81m/s² ; 0.5 = 4.905m/s².");
    Echo("Example: stop");
    Echo("");
    Echo("Info");
    Echo("Prints out all relevant information about location and target at once.");
    Echo("Example: Info");
    Echo("");
    Echo("Print");
    Echo("Prints out all relevant information about location and target into Lcd Panels in the defined group. Normally panels auto update, but this command is for manual updates when in sleep mode.");
    Echo("Example: Print");
    Echo("");
    Echo("Next");
    Echo("Switches to the next page on Lcd-Panels.");
    Echo("Example: Next");
    Echo("");
    Echo("Prev");
    Echo("Switches to the previous page on Lcd-Panels.");
    Echo("Example: Prev");
    Echo("");
    Echo("Help");
    Echo("This page.");
    Echo("Example: Help");
    Echo("");
    Echo("-------------------------");
    Echo("Examples:");
    Echo("Finding coordinates 10,000m above a base on Earth:");
    Echo("   Me:GPS:Center:0.5:0.5:0.5:");
    Echo("   GPS:SomeBase:1000:1000:1000");
    Echo("   Move:10000");
    Echo("   Me");
    Echo("Ending with me resets the Mode back to usage of own coordinates instead of the manually set.");
    Echo("");
    Echo("Finding coordinates of 20,000m in front of you and moving it 4,000m to the right");
    Echo("   >>> Look at your target");
    Echo("   Range: 20000");
    Echo("   >>> Rotate 90 degree to the right");
    Echo("   Move: 4000");
    Echo("");
    Echo("Paste custom coordinates and add up 1,000 m to the distance");
    Echo("   GPS:SomeBase:1000:1000:1000:");
    Echo("   >>> Look at your target");
    Echo("   Move: 1000");
    Echo("");
    Echo("Calculate real flight distance");
    Echo("   Planet:GPS:Center:0.5:0.5:0.5:");
    Echo("   GPS:SomeBase:60000:0:1000:");
    Echo("   Distance");
    Echo("");
    Echo("Calculate real flight distance to unknown target in given distance");
    Echo("   Planet:GPS:Center:0.5:0.5:0.5:");
    Echo("   Distance:20000");
    Echo("");
    Echo("Creating a jump target exactly 200.000m away");
    Echo("   >>> Look at the direction you want to go");
    Echo("   Range:200000");
    Echo("Take the coordinates and select them in jumprive. No messing with blindjumps necessary!");

    Echo("Ending with me resets the Mode back to usage of own coordinates instead of the manually set.");
}

void initShipController()
{
    List<IMyShipController> shipControllers = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType(shipControllers,
        controller => controller.CubeGrid == Me.CubeGrid
                      && controller.CanControlShip);

    if (shipControllers.Count == 0)
        return;

    randomShipController = shipControllers[0];

    if (randomShipController != null)
        Echo("Ship Controller initialized!");
}

void initThrusters()
{
    if (randomShipController == null)
        initShipController();

    if (randomShipController == null)
    {
        Echo("No Ship Controller Found!");
        return;
    }

    Vector3D backwardDirection = randomShipController.WorldMatrix.Forward;
    Vector3D downDirection = randomShipController.WorldMatrix.Down;
    Vector3D upDirection = randomShipController.WorldMatrix.Up;

    List<IMyThrust> thrusters = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType(thrusters,
        thruster => thruster.CubeGrid == Me.CubeGrid);

    List<IMyThrust> upThrusters = new List<IMyThrust>();
    List<IMyThrust> backwardThrusters = new List<IMyThrust>();
    List<IMyThrust> downThrusters = new List<IMyThrust>();

    foreach (IMyThrust thrust in thrusters)
    {
        Matrix matrix = thrust.WorldMatrix;

        if (matrix.Forward == backwardDirection)
            backwardThrusters.Add(thrust);
        else if (matrix.Forward == downDirection)
            upThrusters.Add(thrust);
        else if (matrix.Forward == upDirection)
            downThrusters.Add(thrust);
    }

    if (upThrusters.Count > 0)
    {
        thrustersUp = upThrusters;

        Echo(upThrusters.Count + " upwards thrusters initialized!");
    }

    if (backwardThrusters.Count > 0)
    {
        thrustersBackwards = backwardThrusters;

        Echo(backwardThrusters.Count + " backwards thrusters initialized!");
    }

    if (downThrusters.Count > 0)
    {
        thrustersDown = downThrusters;

        Echo(backwardThrusters.Count + " down thrusters initialized!");
    }
}

private class StartAndLandingControl
{
    static readonly MyDefinitionId Hydrogen = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/Hydrogen");

    private readonly IMyGridTerminalSystem gridTerminalSystem;
    private readonly IMyCubeGrid myCubeGrid;

    private readonly float turnOffSpeed;
    private readonly float turnOnSpeed;

    private IMyShipController shipController;
    List<IMyThrust> upThrusters = new List<IMyThrust>();
    List<IMyThrust> upThrustersHydrogen = new List<IMyThrust>();
    List<IMyThrust> downThrusters = new List<IMyThrust>();

    private int landingState = -1;
    private int startingState = -1;

    private double height = 1000;
    private double startHeight = -1;
    private double currentNeededThrust = 0;

    public StartAndLandingControl(IMyGridTerminalSystem GridTerminalSystem, IMyCubeGrid myCubeGrid, float turnOffSpeed, float turnOnSpeed)
    {
        this.gridTerminalSystem = GridTerminalSystem;
        this.myCubeGrid = myCubeGrid;

        this.turnOffSpeed = turnOffSpeed;
        this.turnOnSpeed = turnOnSpeed;

        setup();
    }

    public void stop()
    {
        landingState = -1;
        startingState = -1;

        foreach (IMyThrust thrust in upThrusters)
            thrust.ThrustOverridePercentage = 0;
        foreach (IMyThrust thrust in downThrusters)
            thrust.ThrustOverridePercentage = 0;
    }

    public void setup(double height, bool start)
    {
        if (landingState != -1 || startingState != -1)
            return;

        setup();

        if (!start)
            this.height = height;
        else
            this.startHeight = height;
    }

    public void setup()
    {
        if (landingState != -1 || startingState != -1)
            return;

        height = 1000;
        startHeight = -1;

        shipController = ermittleRandomController();

        upThrusters.Clear();
        downThrusters.Clear();

        if (shipController == null)
            return;

        Vector3D downDirection = shipController.WorldMatrix.Down;
        Vector3D upDirection = shipController.WorldMatrix.Up;

        List<IMyThrust> thrusters = new List<IMyThrust>();
        gridTerminalSystem.GetBlocksOfType(thrusters,
            thruster => thruster.CubeGrid == myCubeGrid);

        foreach (IMyThrust thrust in thrusters)
        {
            Matrix matrix = thrust.WorldMatrix;

            MyResourceSinkComponent sink;
            thrust.Components.TryGet(out sink);

            bool usesHydrogen = false;
            if (sink != null)
                usesHydrogen = sink.AcceptedResources.Contains(Hydrogen);

            if (thrust.BlockDefinition.SubtypeId.Contains("Hydrogen"))
                usesHydrogen = true;

            if (usesHydrogen)
            {
                if (matrix.Forward == downDirection)
                    upThrustersHydrogen.Add(thrust);
                if (matrix.Forward == upDirection)
                    downThrusters.Add(thrust);
            }
            else
            {
                if (matrix.Forward == downDirection)
                    upThrusters.Add(thrust);
                if (matrix.Forward == upDirection)
                    downThrusters.Add(thrust);
            }
        }

        stop();
    }

    public bool runLanding()
    {
        if (startingState != -1)
            return true;

        if (landingState == -1)
        {
            landingState = 0;
            return false;
        }

        if (landingState == 0)
        {
            foreach (IMyThrust thrust in upThrusters)
            {
                thrust.ThrustOverride = 1;
                thrust.Enabled = true;
            }

            foreach (IMyThrust thrust in upThrustersHydrogen)
                thrust.ThrustOverride = 1;

            landingState++;
        }

        double maxThrust = 0;
        double maxThrustHydrogen = 0;
        foreach (IMyThrust thrust in upThrusters)
            maxThrust += thrust.MaxEffectiveThrust;
        foreach (IMyThrust thrust in upThrustersHydrogen)
            maxThrustHydrogen += thrust.MaxEffectiveThrust;

        Vector3D gravityVector = shipController.GetNaturalGravity();
        Vector3D shipVelocityVec = shipController.GetShipVelocities().LinearVelocity;

        double speed = VectorProjection(shipVelocityVec, gravityVector).Length() * Math.Sign(shipVelocityVec.Dot(gravityVector));
        MyShipMass shipMass = shipController.CalculateShipMass();

        double mass = shipMass.PhysicalMass;
        double gravity = gravityVector.Normalize();
        double newton = mass * gravity;

        double remainingNewtons = Math.Max(0, (maxThrust + maxThrustHydrogen - newton));
        double deceleration = remainingNewtons / mass;

        double bremsZeit = speed / deceleration;
        double bremsWeg = 0.5 * deceleration * (bremsZeit * bremsZeit);

        if (gravity == 0)
        {
            foreach (IMyThrust thrust in upThrusters)
                thrust.ThrustOverridePercentage = 0;
            foreach (IMyThrust thrust in upThrustersHydrogen)
                thrust.ThrustOverridePercentage = 0;

            landingState = -1;
        }

        if (landingState == 1)
        {
            double heightAboveSurface;
            if (shipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out heightAboveSurface))
            {
                if (speed > 95 && heightAboveSurface <= 10000)
                    landingState++;

                if (bremsWeg >= (heightAboveSurface - height))
                {
                    currentNeededThrust = maxThrust + maxThrustHydrogen;
                    landingState += 2;
                }
            }
        }

        if (landingState == 2)
        {
            double heightAboveSurface;
            if (shipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out heightAboveSurface))
            {
                double thrustNeeded = newton + remainingNewtons * (bremsWeg / Math.Max(1, heightAboveSurface - height));

                bool needsHydrogen = maxThrust - thrustNeeded < 0;
                double usedThrust = maxThrust;

                if (needsHydrogen)
                {
                    thrustNeeded -= maxThrust;
                    usedThrust = maxThrustHydrogen;
                }

                double thrustPercent = thrustNeeded / usedThrust;

                if (needsHydrogen)
                {
                    foreach (IMyThrust thrust in upThrusters)
                        thrust.ThrustOverridePercentage = 1;

                    foreach (IMyThrust thrust in upThrustersHydrogen)
                    {
                        thrust.ThrustOverridePercentage = (float)thrustPercent;
                        thrust.Enabled = true;
                    }
                }
                else
                {
                    foreach (IMyThrust thrust in upThrusters)
                        thrust.ThrustOverridePercentage = (float)thrustPercent;

                    foreach (IMyThrust thrust in upThrustersHydrogen)
                    {
                        thrust.ThrustOverridePercentage = 0;
                        thrust.Enabled = false;
                    }
                }

                if (speed < 1)
                    landingState += 2;
            }
        }

        if (landingState == 3)
        {
            double thrustPercent = currentNeededThrust / (maxThrust + maxThrustHydrogen);

            foreach (IMyThrust thrust in upThrusters)
            {
                thrust.ThrustOverridePercentage = (float)thrustPercent;
                thrust.Enabled = true;
            }

            foreach (IMyThrust thrust in upThrustersHydrogen)
            {
                thrust.ThrustOverridePercentage = (float)thrustPercent;
                thrust.Enabled = true;
            }

            if (speed < 1)
                landingState++;
        }

        if (landingState == 4)
        {
            foreach (IMyThrust thrust in upThrusters)
                thrust.ThrustOverridePercentage = 0;
            foreach (IMyThrust thrust in upThrustersHydrogen)
                thrust.ThrustOverridePercentage = 0;

            landingState = -1;
        }

        return landingState == -1;
    }

    public bool runStarting()
    {
        if (landingState != -1)
            return true;

        if (startingState == -1)
        {
            startingState = 0;
            return false;
        }

        if (startingState == 0)
        {
            foreach (IMyThrust thrust in upThrusters)
            {
                thrust.ThrustOverridePercentage = 1;
                thrust.Enabled = true;
            }

            foreach (IMyThrust thrust in upThrustersHydrogen)
            {
                thrust.ThrustOverridePercentage = 1;
                thrust.Enabled = true;
            }

            startingState++;
        }

        double maxThrustUp = 0;
        double maxThrustUpHydrogen = 0;
        foreach (IMyThrust thrust in upThrusters)
            maxThrustUp += thrust.MaxEffectiveThrust;
        foreach (IMyThrust thrust in upThrustersHydrogen)
            maxThrustUpHydrogen += thrust.MaxEffectiveThrust;

        double maxThrustDown = 0;
        foreach (IMyThrust thrust in downThrusters)
            maxThrustDown += thrust.MaxEffectiveThrust;

        Vector3D gravityVector = shipController.GetNaturalGravity();
        Vector3D shipVelocityVec = shipController.GetShipVelocities().LinearVelocity;

        double speed = VectorProjection(shipVelocityVec, gravityVector).Length() * Math.Sign(shipVelocityVec.Dot(gravityVector));
        MyShipMass shipMass = shipController.CalculateShipMass();

        double mass = shipMass.PhysicalMass;
        double gravity = gravityVector.Normalize();
        double newton = mass * gravity;
        double maxNewton = newton + maxThrustDown / 2;
        double deceleration = maxNewton / mass;

        double bremsZeit = (speed * -1) / deceleration;
        double bremsWeg = 0.5 * deceleration * (bremsZeit * bremsZeit);

        if (startingState == 1)
        {
            double heightAboveSurface;
            if (shipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out heightAboveSurface))
            {
                if (speed < -(turnOffSpeed - 1))
                {
                    startingState++;
                }
                else if (startHeight > 0 && bremsWeg >= (startHeight - heightAboveSurface))
                {
                    foreach (IMyThrust thrust in upThrusters)
                        thrust.ThrustOverridePercentage = 0;
                    foreach (IMyThrust thrust in upThrustersHydrogen)
                        thrust.ThrustOverridePercentage = 0;

                    currentNeededThrust = maxNewton;
                    startingState += 2;
                }
            }
        }

        if (shipController.GetNaturalGravity().Normalize() == 0)
        {
            foreach (IMyThrust thrust in upThrusters)
                thrust.ThrustOverridePercentage = 0;
            foreach (IMyThrust thrust in upThrustersHydrogen)
                thrust.ThrustOverridePercentage = 0;
            foreach (IMyThrust thrust in downThrusters)
                thrust.ThrustOverridePercentage = 0;

            startingState = -1;
        }

        if (startingState == 2)
        {
            double heightAboveSurface;
            if (shipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out heightAboveSurface))
            {
                double thrustNeeded = newton;

                bool needsHydrogen = maxThrustUp - thrustNeeded < 0;
                double usedThrust = maxThrustUp;

                if (needsHydrogen)
                {
                    thrustNeeded -= maxThrustUp;
                    usedThrust = maxThrustUpHydrogen;
                }

                double thrustPercent = thrustNeeded / usedThrust;

                float extraThrust = 0.01F;
//if (startHeight < 0)
//    extraThrust = 0;

                if (needsHydrogen)
                {
                    foreach (IMyThrust thrust in upThrusters)
                    {
                        thrust.ThrustOverridePercentage = 1;
                        thrust.Enabled = true;
                    }

                    foreach (IMyThrust thrust in upThrustersHydrogen)
                    {
                        thrust.ThrustOverridePercentage = (float)thrustPercent + extraThrust;
                        thrust.Enabled = true;
                    }
                }
                else
                {
                    foreach (IMyThrust thrust in upThrusters)
                    {
                        thrust.ThrustOverridePercentage = (float)thrustPercent + extraThrust;
                        thrust.Enabled = true;
                    }

                    foreach (IMyThrust thrust in upThrustersHydrogen)
                    {
                        thrust.ThrustOverridePercentage = 0;
                        thrust.Enabled = false;
                    }
                }

                if (startHeight > 0 && bremsWeg >= (startHeight - heightAboveSurface))
                {
                    foreach (IMyThrust thrust in upThrusters)
                        thrust.ThrustOverridePercentage = 0;
                    foreach (IMyThrust thrust in upThrustersHydrogen)
                        thrust.ThrustOverridePercentage = 0;

                    currentNeededThrust = maxNewton;
                    startingState++;
                }
                else if (speed > -turnOnSpeed)
                {
                    startingState = 0;
                }
            }
        }

        if (startingState == 3)
        {
            double thrustPercent = (currentNeededThrust - newton) / maxThrustDown;

            foreach (IMyThrust thrust in downThrusters)
            {
                thrust.ThrustOverridePercentage = (float)thrustPercent + 0.01f;
                thrust.Enabled = true;
            }

            if (speed > -1)
                startingState++;
        }

        if (startingState == 4)
        {
            foreach (IMyThrust thrust in upThrusters)
                thrust.ThrustOverridePercentage = 0;
            foreach (IMyThrust thrust in upThrustersHydrogen)
                thrust.ThrustOverridePercentage = 0;
            foreach (IMyThrust thrust in downThrusters)
                thrust.ThrustOverridePercentage = 0;

            startingState = -1;
        }

        return startingState == -1;
    }

    private IMyShipController ermittleRandomController()
    {
        List<IMyShipController> blocks = new List<IMyShipController>();
        gridTerminalSystem.GetBlocksOfType(blocks);

        IMyShipController randomController = null;

        foreach (IMyShipController controller in blocks)
        {
            if (myCubeGrid != controller.CubeGrid)
                continue;

            if (controller.CanControlShip)
            {
                randomController = controller;
                break;
            }
        }

        return randomController;
    }
}

private class RotationControl
{
    private readonly double ctrlCoeff;

    private readonly IMyGridTerminalSystem gridTerminalSystem;
    private readonly IMyCubeGrid cubeGrid;

    private IMyShipController shipController;
    private List<IMyGyro> gyros;

    private bool wasForcedToStop = false;

    public RotationControl(IMyGridTerminalSystem gridTerminalSystem, IMyCubeGrid cubeGrid, double ctrlCoeff)
    {
        this.gridTerminalSystem = gridTerminalSystem;
        this.cubeGrid = cubeGrid;
        this.ctrlCoeff = ctrlCoeff;

        setup();
    }

    public void stop()
    {
        foreach (IMyGyro gyro in gyros)
        {
            gyro.Pitch = 0f;
            gyro.Roll = 0f;
            gyro.Yaw = 0f;

            gyro.GyroOverride = false;
        }

        wasForcedToStop = true;
    }

    public void setup()
    {
        shipController = ermittleRandomController();

        gyros = new List<IMyGyro>();
        gridTerminalSystem.GetBlocksOfType(gyros, gyro => gyro.CubeGrid == cubeGrid);

        stop();

        wasForcedToStop = false;
    }

    public bool run()
    {
        if (wasForcedToStop)
            return true;

        Vector3D planetLocation;
        if (shipController == null || !shipController.TryGetPlanetPosition(out planetLocation))
        {
            foreach (IMyGyro gyro in gyros)
            {
                if (gyro == null)
                    continue;

                gyro.Pitch = 0f;
                gyro.Roll = 0f;
                gyro.Yaw = 0f;

                gyro.GyroOverride = false;
            }

            return true;
        }

        Matrix orientation;
        shipController.Orientation.GetMatrix(out orientation);

        Vector3D down = orientation.Down;

        Vector3D grav = shipController.GetNaturalGravity();
        grav.Normalize();

        bool done = false;

        foreach (IMyGyro gyro in gyros)
        {
            if (gyro == null)
                continue;

            gyro.Orientation.GetMatrix(out orientation);

            var localDown = Vector3D.Transform(down, MatrixD.Transpose(orientation));
            var localGrav = Vector3D.Transform(grav, MatrixD.Transpose(gyro.WorldMatrix.GetOrientation()));

            var rotation = Vector3D.Cross(localDown, localGrav);
            Vector3D lol = new Vector3D(rotation);

            double angle = rotation.Length();
            angle = Math.Atan2(angle, Math.Sqrt(Math.Max(0.0, 1.0 - angle * angle)));

            if (angle < 0.01)
            {
                //Close enough

                gyro.GyroOverride = false;
                done = true;
                continue;
            }

            double ctrl_vel = 30 * (angle / Math.PI) * ctrlCoeff;
            ctrl_vel = Math.Min(30, ctrl_vel);
            ctrl_vel = Math.Max(0.01, ctrl_vel);

            rotation.Normalize();
            rotation *= ctrl_vel;

            gyro.SetValueFloat("Pitch", (float)rotation.X);
            gyro.SetValueFloat("Yaw", -(float)rotation.Y);
            gyro.SetValueFloat("Roll", -(float)rotation.Z);

            gyro.GyroOverride = true;
        }

        return done;
    }

    private IMyShipController ermittleRandomController()
    {
        List<IMyShipController> blocks = new List<IMyShipController>();
        gridTerminalSystem.GetBlocksOfType(blocks);

        IMyShipController randomController = null;

        foreach (IMyShipController controller in blocks)
        {
            if (cubeGrid != controller.CubeGrid)
                continue;

            if (controller.CanControlShip)
            {
                randomController = controller;
                break;
            }
        }

        return randomController;
    }
}

public class RaycastControl
{
    private readonly IMyCameraBlock camera;
    private readonly TargetControl targetControl;
    private readonly IO io;
    private readonly IMyProgrammableBlock Me;
    private readonly Configuration configuration;

    public RaycastControl(IMyProgrammableBlock Me, IMyCameraBlock camera, TargetControl targetControl, IO io, Configuration configuration)
    {
        this.targetControl = targetControl;
        this.io = io;
        this.Me = Me;
        this.camera = camera;
        this.configuration = configuration;
    }

    public void supercast()
    {
        supercast(configuration.get("SUPERCAST_DEFAULT_DISTANCE").getDoubleValue());
    }

    public void supercast(double maxDistance)
    {
        float minSuparcast = configuration.get("MIN_SUPERCAST").getFloatValue();
        float maxSuparcast = configuration.get("MAX_SUPERCAST").getFloatValue();
        float suparcastSteps = configuration.get("SUPERCAST_STEPS").getFloatValue();

        for (float i = minSuparcast; i < maxSuparcast; i += suparcastSteps)
        {
            for (float j = minSuparcast; j < maxSuparcast; j += suparcastSteps)
            {
                if (raycast(i, j, true, maxDistance))
                {
                    io.Echo("");
                    printInfo(io);

                    return;
                }
            }
        }

        targetControl.resetTarget();
    }

    public void raycast(bool getCenter)
    {
        raycast(0f, 0f, getCenter, configuration.get("RAYCAST_DEFAULT_DISTANCE").getDoubleValue());
    }

    public bool raycast(float pitch, float yaw, bool getCenter, double maxDistance)
    {
        if (camera == null)
        {
            io.Echo("Camera not found!");
            targetControl.resetTarget();
            targetControl.setLightColor(ERROR_COLOR);

            return false;
        }

        if (camera.CanScan(maxDistance))
        {
            MyDetectedEntityInfo info = camera.Raycast(maxDistance, pitch, yaw);

            MyDetectedEntityType type = info.Type;
            if (type != MyDetectedEntityType.None && type != MyDetectedEntityType.Planet && Me.CubeGrid.EntityId != info.EntityId)
            {
                MyRelationsBetweenPlayerAndBlock relationship = info.Relationship;

                Vector3D velocity = info.Velocity;

                Vector3D? targetPosition;

                if (getCenter)
                    targetPosition = info.Position;
                else
                    targetPosition = info.HitPosition;

                io.Echo("Target: " + VectorToString(targetPosition.Value, 2, ", "));
                io.Echo("Name: " + info.Name);
                io.Echo("Relationship: " + relationship);
                io.Echo("Speed: " + velocity.Length().ToString("#,##0.0") + " ms");
                io.Echo("Velocity: " + velocity);

                targetControl.setTarget(info, getCenter);

                io.Echo("");
                printInfo(io);

                return true;
            }
            else
            {
                io.Echo("Nothing found!");

                targetControl.resetTarget();

                return false;
            }
        }
        else
        {
            io.Echo("Scan not Possible!");

            targetControl.resetTarget();
            targetControl.setLightColor(ERROR_COLOR);

            return false;
        }
    }

    private string VectorToString(Vector3D vector, int decimals, string delimiter)
    {
        return Math.Round(vector.GetDim(0), decimals) + delimiter + Math.Round(vector.GetDim(1), decimals) + delimiter + Math.Round(vector.GetDim(2), decimals);
    }

    public void printInfo(IO io)
    {
        if (camera == null)
        {
            io.Echo("   no Camera");
            return;
        }

        double availableRange = camera.AvailableScanRange;
        double maxDistance = camera.RaycastDistanceLimit;

        float minSuparcast = configuration.get("MIN_SUPERCAST").getFloatValue();
        float maxSuparcast = configuration.get("MAX_SUPERCAST").getFloatValue();
        float suparcastSteps = configuration.get("SUPERCAST_STEPS").getFloatValue();

        double raycastDefaultDistance = configuration.get("RAYCAST_DEFAULT_DISTANCE").getDoubleValue();
        double supercastDefaultDistance = configuration.get("SUPERCAST_DEFAULT_DISTANCE").getDoubleValue();

        int superCastCount = 0;
        for (float i = minSuparcast; i < maxSuparcast; i += suparcastSteps)
        for (float j = minSuparcast; j < maxSuparcast; j += suparcastSteps)
            superCastCount++;

        string maxDistanceString = "unlimited";
        if (maxDistance > 0)
            maxDistanceString = maxDistance.ToString("#,##0.0#") + " m";

        io.Echo("   Available range: " + availableRange.ToString("#,##0.0#") + " m");
        io.Echo("   Max range: " + maxDistanceString);
        io.Echo("   Max cone: " + camera.RaycastConeLimit.ToString("#,##0.0#") + " °");
        io.Echo("");
        io.Echo("   Raycast default: " + raycastDefaultDistance.ToString("#,##0.0#") + " m");
        io.Echo("");
        io.Echo("   Supercast default: " + supercastDefaultDistance.ToString("#,##0.0#") + " m");
        io.Echo("   Supercast cone minimum: " + minSuparcast.ToString("#,##0.0#") + " °");
        io.Echo("   Supercast cone maximum: " + maxSuparcast.ToString("#,##0.0#") + " °");
        io.Echo("   Supercast cone step size: " + suparcastSteps.ToString("#,##0.0#") + " °");
        io.Echo("   Raycasts per supercast: " + superCastCount.ToString("#,##0"));
        io.Echo("");
        io.Echo("   Remaining raycasts: "
                + Math.Floor(availableRange / raycastDefaultDistance).ToString("#,##0"));
        io.Echo("   Remaining supercasts: "
                + Math.Floor(availableRange / (supercastDefaultDistance * superCastCount)).ToString("#,##0"));
    }
}

public class TargetControl
{
    private readonly List<IMyInteriorLight> lights = null;
    private readonly IMyProgrammableBlock Me = null;
    private readonly IO io;
    private readonly Configuration configuration;
    private readonly LcdUpdateManager lcdUpdateManager;
    private readonly LocationControl locationControl;
    private readonly PlanetControl planetControl;

    private Vector3D? targetLocation = null;

    private Vector3D? A1 = null;
    private Vector3D? A2 = null;

    public TargetControl(IMyProgrammableBlock Me, IMyGridTerminalSystem GridTerminalSystem, Configuration configuration,
        LocationControl locationControl, PlanetControl planetControl, LcdUpdateManager lcdUpdateManager, IO io)
    {
        this.io = io;
        this.Me = Me;
        this.configuration = configuration;
        this.lcdUpdateManager = lcdUpdateManager;
        this.locationControl = locationControl;
        this.planetControl = planetControl;

        string shipNameString = configuration.get("SHIP_NAME").getStringValue();
        string separatorString = configuration.get("SEPARATOR").getStringValue();
        string lightString = configuration.get("LIGHT_NAME").getStringValue();

        string groupName = shipNameString + separatorString + lightString;

        IMyBlockGroup reportLightGroup = GridTerminalSystem.GetBlockGroupWithName(groupName);

        if (reportLightGroup != null)
        {
            lights = new List<IMyInteriorLight>();
            reportLightGroup.GetBlocksOfType<IMyInteriorLight>(lights);

            setLightColor(NOTHING_COLOR);
            io.Echo(lights.Count + " Light(s) initialized!");
        }
        else
        {
            IMyInteriorLight light = GridTerminalSystem.GetBlockWithName(shipNameString + separatorString + lightString) as IMyInteriorLight;

            if (light != null)
            {
                lights = new List<IMyInteriorLight>();
                lights.Add(light);

                setLightColor(NOTHING_COLOR);
                io.Echo("1 Light initialized!");
            }
        }

        string targetString = configuration.get("Target").getStringValue();
        this.targetLocation = GpsParser.fromGpsString(targetString);

        if (targetLocation != null)
            io.Echo("Target initialzed at " + this.targetLocation + "!");
    }

    public bool hasTarget()
    {
        return targetLocation != null;
    }

    public Vector3D getTargetLocation()
    {
        return targetLocation.Value;
    }

    public void resetTarget()
    {
        targetLocation = null;

        renameMe(targetLocation);
        setLightColor(NOTHING_COLOR);

        io.Echo("Target removed!");
    }

    public void setTargetString(string gpsString)
    {
        targetLocation = GpsParser.fromGpsString(gpsString);

        renameMe(targetLocation);
        setLightColor(NOTHING_COLOR);

        io.Echo("Target set to: " + this.targetLocation);
    }

    public void setTarget(MyDetectedEntityInfo entityInfo, bool getCenter)
    {
        Vector3D? targetPosition;

        if (getCenter)
            targetPosition = entityInfo.Position;
        else
            targetPosition = entityInfo.HitPosition;

        switch (entityInfo.Relationship)
        {
            case MyRelationsBetweenPlayerAndBlock.NoOwnership:
            case MyRelationsBetweenPlayerAndBlock.Neutral:
                setLightColor(NEUTRAL_COLOR);
                break;
            case MyRelationsBetweenPlayerAndBlock.Owner:
                setLightColor(OWN_COLOR);
                break;
            case MyRelationsBetweenPlayerAndBlock.FactionShare:
                setLightColor(FACTION_COLOR);
                break;
            case MyRelationsBetweenPlayerAndBlock.Enemies:
                setLightColor(HOSTRILE_COLOR);
                break;
            default:
                setLightColor(ERROR_COLOR);
                break;
        }

        renameMe(targetPosition);
        targetLocation = targetPosition;

        io.Echo("Target set to: " + targetLocation);
    }

    internal void setLightColor(Color color)
    {
        if (lights == null)
            return;

        foreach (IMyTerminalBlock block in lights)
            if (block is IMyInteriorLight)
                ((IMyInteriorLight)block).Color = color;
    }

    private void renameMe(Vector3D? targetLocation)
    {
        this.targetLocation = targetLocation;

        if (targetLocation == null)
        {
            Me.CustomName = "GPS:Target:Na";
            configuration.get("Target").parseString("");
        }
        else
        {
            string gpsString = GpsParser.toGpsString(targetLocation.Value);

            configuration.get("Target").parseString(gpsString);

            Me.CustomName = gpsString;
        }

        configuration.save();

        lcdUpdateManager.setNeedsUpdate();
    }

    public void moveTargetString(string distanceString)
    {
        if (targetLocation == null)
        {
            io.Echo("You need a target before moving it around!");
        }
        else
        {
            double distance = 0;
            double.TryParse(distanceString, out distance);

            if (distance == 0)
                return;

            moveTarget(distance);

            io.Echo("Target set to: " + targetLocation);
        }
    }

    public void getTargetInRangeString(string distanceString)
    {
        double distance = 0;
        double.TryParse(distanceString, out distance);

        if (distance == 0)
            return;

        ermittleTarget(distance, locationControl.getBackPosition());

        io.Echo("Target set to: " + targetLocation);
    }

    private void moveTarget(double distance)
    {
        if (targetLocation == null)
            return;

        targetLocation = berechneDistanceVector(distance) + targetLocation.Value;

        renameMe(targetLocation);
    }

    private void ermittleTarget(double distance, Vector3D p2)
    {
        targetLocation = berechneDistanceVector(distance) + p2;

        renameMe(targetLocation);
    }

    private Vector3D berechneDistanceVector(double distance)
    {
        Vector3D p1 = locationControl.getFrontPosition();
        Vector3D p2 = locationControl.getBackPosition();

        Vector3D? overrideLocation = locationControl.getOverrideLocation();

        if (overrideLocation != null && targetLocation != null)
        {
            p1 = targetLocation.Value;
            p2 = overrideLocation.Value;
        }

        Vector3D v1 = p1 - p2;

        double distanceP12 = Vector3D.Distance(p1, p2);

        return v1 * (distance / distanceP12);
    }

    public void takeA()
    {
        A1 = locationControl.getFrontPosition();
        A2 = locationControl.getBackPosition();

        io.Echo("Vector A taken!");
    }

    public void takeB()
    {
        io.Echo("Vector B taken!");

        ermittleTargetOfLocations();
    }

    private void ermittleTargetOfLocations()
    {
        Vector3D p1 = locationControl.getFrontPosition();
        Vector3D p2 = locationControl.getBackPosition();

        Vector3D? overrideLocation = locationControl.getOverrideLocation();

        if (overrideLocation != null && targetLocation != null)
        {
            p1 = targetLocation.Value;
            p2 = overrideLocation.Value;
        }

        if (A1 == null || A2 == null)
        {
            io.Echo("Need to Set Vector A first!");
            return;
        }

        Vector3D A1A2 = (Vector3D)A1 - (Vector3D)A2;
        Vector3D B1B2 = p1 - p2;

        Vector3D Va = p1 - (Vector3D)A1;
        Vector3D Vb = (Vector3D)A1 - p1;
        double betragVa = CalcBetrag(Va);
        double betragVb = CalcBetrag(Vb); //dann muss ich betta nicht drehen

        double alphaCos = CalcWert(A1A2 * Va) / (betragVa * CalcBetrag(A1A2));
        double alpha = RadianToDegree(Math.Acos(alphaCos)); //Bogenmaß in Grad umrechnen

        double bettaCos = CalcWert(B1B2 * Vb) / (betragVb * CalcBetrag(B1B2));
        double betta = RadianToDegree(Math.Acos(bettaCos)); //Bogenmaß in Grad umrechnen

        double gamma = 180 - alpha - betta;
        double gammaRadians = DegreeToRadians(gamma);

        io.Echo("Alpha: " + alpha.ToString("#,##0.0#"));
        io.Echo("Betta: " + betta.ToString("#,##0.0#"));
        io.Echo("Gamma: " + gamma.ToString("#,##0.0#"));

        Vector3D A1NotNull = (Vector3D)A1;

        double distance = 0;
        Vector3D.Distance(ref p1, ref A1NotNull, out distance);

        io.Echo("Seite C: " + distance.ToString("#,##0.0#"));

//nach sinussatz ist c / sin(gamma) = a / sin(alpha) umgestellt also a = c / sin(gamma) * sin(alpha)
        distance = (distance / Math.Sin(gammaRadians)) * Math.Sin(DegreeToRadians(alpha));

//distance nach http://www.calculator.net/triangle-calculator.html ermittelt distanz stimmt
        io.Echo("Entfernung zum Ziel: " + distance.ToString("#,##0.0#"));

        ermittleTarget(distance, p2);

        io.Echo("Target set to: " + this.targetLocation);
    }

    private double CalcWert(Vector3D vector)
    {
        return vector.X + vector.Y + vector.Z;
    }

    private double CalcBetrag(Vector3D vector)
    {
        return Math.Sqrt(Math.Pow(vector.X, 2) + Math.Pow(vector.Y, 2) + Math.Pow(vector.Z, 2));
    }

    public void printInfo(IO io, double speed)
    {
        Vector3D myLocation = locationControl.getFrontPosition();

        if (targetLocation != null)
        {
            Vector3D targetNotNull = targetLocation.Value;

            io.Echo("   X: " + targetNotNull.X.ToString("#,##0.0#"));
            io.Echo("   Y: " + targetNotNull.Y.ToString("#,##0.0#"));
            io.Echo("   Z: " + targetNotNull.Z.ToString("#,##0.0#"));
            io.Echo("");

            double distanceToTarget = Vector3D.Distance(myLocation, targetNotNull);

            io.Echo("   Distance to target:");
            io.Echo("      " + distanceToTarget.ToString("#,##0.0#") + " m");
            io.Echo("      Arrival in:");

            if (speed > 0.01)
                io.Echo("         " + getTimeString(distanceToTarget / speed) + " when at " + speed.ToString("#,##0.0#") + " m/s");
            else
                io.Echo("         Never");

            io.Echo("");

            Vector3D? planetLocation = planetControl.getPlanet();

            if (planetLocation != null)
            {
                Vector3D planetNotNull = planetLocation.Value;
                double distanceToCenter = Vector3D.Distance(myLocation, planetNotNull);
                double diameter = distanceToCenter * 2;

                if (distanceToTarget <= diameter)
                {
                    double distanceToTargetPlanet = diameter * Math.Asin(distanceToTarget / diameter);

                    io.Echo("   Distance to target over planet:");
                    io.Echo("      " + distanceToTargetPlanet.ToString("#,##0.0#") + " m");
                    io.Echo("      Arrival in:");

                    if (speed > 0.01)
                        io.Echo("         " + getTimeString(distanceToTargetPlanet / speed) + " when at " + speed.ToString("#,##0.0#") + " m/s");
                    else
                        io.Echo("         Never");
                }
            }
        }
        else
        {
            io.Echo("   no Target");
        }
    }
}

public class LocationControl
{
    private readonly IO io;

    private readonly IMyProgrammableBlock Me;
    private readonly IMyCameraBlock camera;

    private TargetControl targetControl;

    private Vector3D? overrideLocation = null;

    public LocationControl(IMyProgrammableBlock Me, IMyCameraBlock camera, IO io)
    {
        this.Me = Me;
        this.camera = camera;
        this.io = io;
    }

    public void setTargetControl(TargetControl targetControl)
    {
        this.targetControl = targetControl;
    }

    public Vector3D getFrontPosition()
    {
        if (overrideLocation != null)
            return overrideLocation.Value;

        if (camera != null)
            return camera.GetPosition() + camera.WorldMatrix.Forward;

        return Me.GetPosition() + Me.WorldMatrix.Forward;
    }

    public Vector3D getBackPosition()
    {
        if (overrideLocation != null && targetControl.hasTarget())
            return targetControl.getTargetLocation();

        if (camera != null)
            return camera.GetPosition();

        return Me.GetPosition();
    }

    public Vector3D getLeftPosition()
    {
        if (camera != null)
            return camera.GetPosition() + camera.WorldMatrix.Left;

        return Me.GetPosition() + Me.WorldMatrix.Left;
    }

    public void resetOverrideLocation()
    {
        overrideLocation = null;

        io.Echo("Me is now at: " + getFrontPosition());
    }

    public Vector3D? getOverrideLocation()
    {
        return overrideLocation;
    }

    public void setOverrideLocation(string gpsString)
    {
        overrideLocation = GpsParser.fromGpsStringNullSafe(io, gpsString);

        io.Echo("Me is now at: " + overrideLocation);
    }

    public double calcPitch()
    {
        Vector3D front = getFrontPosition();
        Vector3D center = getBackPosition();

        Vector3D delta = center - front;

        double pitch = Math.Atan2(delta.Y, Math.Sqrt(Math.Pow(delta.X, 2) + Math.Pow(delta.Z, 2)));

        return Math.Round(RadianToDegree(pitch), 3);
    }

    public double calcYaw()
    {
        Vector3D front = getFrontPosition();
        Vector3D center = getBackPosition();

        Vector3D delta = center - front;

        double yaw = Math.Atan2(delta.Z, delta.X);

        return Math.Round(RadianToDegree(yaw), 3);
    }

    public double calcRoll()
    {
        Vector3D left = getLeftPosition();
        Vector3D center = getBackPosition();

        Vector3D delta = center - left;

        double roll = Math.Atan2(delta.Y, Math.Sqrt(Math.Pow(delta.X, 2) + Math.Pow(delta.Z, 2)));

        return Math.Round(RadianToDegree(roll), 3);
    }

    public void printInfo(IO io)
    {
        Vector3D myLocation = getFrontPosition();

        io.Echo("   X: " + myLocation.X.ToString("#,##0.0#"));
        io.Echo("   Y: " + myLocation.Y.ToString("#,##0.0#"));
        io.Echo("   Z: " + myLocation.Z.ToString("#,##0.0#"));
        io.Echo("");
        io.Echo("   Pitch: " + calcPitch().ToString("#,##0.0#") + " °");
        io.Echo("   Yaw: " + calcYaw().ToString("#,##0.0#") + " °");
        io.Echo("   Roll: " + calcRoll().ToString("#,##0.0#") + " °");
    }
}

public class PlanetControl
{
    private readonly IO io;
    private readonly Configuration configuration;
    private readonly LocationControl locationControl;

    private Vector3D? planet = null;
    private bool planetManuallySet = false;

    private double heightOverride = -1;

    public PlanetControl(IO io, LocationControl locationControl, Configuration configuration)
    {
        this.io = io;
        this.configuration = configuration;
        this.locationControl = locationControl;

        string planetString = configuration.get("Planet").getStringValue();
        this.planet = GpsParser.fromGpsString(planetString);

        if (planet != null)
            io.Echo("Planet center initialized at " + this.planet + "!");

        planetManuallySet = configuration.get("PlanetManuallySet").getBooleanValue();
        if (planetManuallySet)
            io.Echo("Planet was manually set!");
    }

    public Vector3D? getPlanet()
    {
        return planet;
    }

    public bool isPlanetManuallySet()
    {
        return planetManuallySet;
    }

    public void removePlanet()
    {
        configuration.get("Planet").parseString("");
        configuration.get("PlanetManuallySet").setBooleanValue(false);

        planet = null;
        planetManuallySet = false;

        io.Echo("Planet center removed!");

        configuration.save();
    }

    public void setPlanetWithGps(string gpsString)
    {
        configuration.get("Planet").parseString(gpsString);
        configuration.get("PlanetManuallySet").setBooleanValue(true);

        planet = GpsParser.fromGpsStringNullSafe(io, gpsString);
        planetManuallySet = true;

        io.Echo("Planet center was manually set and is now at: " + this.planet + ". Don't forget to unset the planet when you are done.");

        configuration.save();
    }

    public void setPlanet(Vector3D planetLocation)
    {
        planet = planetLocation;
        configuration.get("Planet").parseString(GpsParser.toGpsString(planetLocation));

        io.Echo("Planet center is now at: " + this.planet);

        configuration.save();
    }

    public void resetHeightOverride()
    {
        setHeightOverride(-1);

        io.Echo("Height override removed!");

        if (planet != null)
            io.Echo("Height above planets center is now " + getHeightAbovePlanet().ToString("#,##0.0#") + " m");
    }

    public void setHeightOverride(double newHeight)
    {
        heightOverride = newHeight;

        io.Echo("Height above center is now " + this.heightOverride.ToString("#,##0.0#") + " m");

        if (planet == null)
            io.Echo("But planet is not yet set!");
    }

    public void setHeightOverrideString(string newHeightString)
    {
        double newHeight;
        double.TryParse(newHeightString, out newHeight);

        setHeightOverride(newHeight);
    }

    public double getHeightAbovePlanet()
    {
        if (heightOverride > 0)
            return heightOverride;

        if (planet == null)
        {
            io.Echo("You have to set a planet first!");
            return -1;
        }

        return Vector3D.Distance((Vector3D)planet, locationControl.getFrontPosition());
    }

    public void printInfo(IO io)
    {
        if (planet != null)
        {
            Vector3D planetNotNull = (Vector3D)planet;

            io.Echo("   X: " + planetNotNull.X.ToString("#,##0.0#"));
            io.Echo("   Y: " + planetNotNull.Y.ToString("#,##0.0#"));
            io.Echo("   Z: " + planetNotNull.Z.ToString("#,##0.0#"));
            io.Echo("");

            io.Echo("   Planet location manually set: " + planetManuallySet);
            io.Echo("");

            io.Echo("   Distance to center:");
            io.Echo("      " + getHeightAbovePlanet().ToString("#,##0.0#") + " m");
        }
        else
        {
            io.Echo("   no Planet");
        }
    }
}

public class GravityDrive
{
    private const double G = 9.81;

    private readonly List<IMyArtificialMassBlock> gravityMassBlocks = null;
    private readonly List<IMyGravityGenerator> gravityGeneratorBlocks = null;
    private readonly string driveIdentifier;

    private readonly LcdUpdateManager lcdUpdateManager;
    private readonly IO io;

    private double currentGravityDriveAcceleration = 0.0;

    public GravityDrive(IO io, LcdUpdateManager lcdUpdateManager, IMyGridTerminalSystem GridTerminalSystem, Configuration configuration)
    {
        string shipNameString = configuration.get("SHIP_NAME").getStringValue();
        string separatorString = configuration.get("SEPARATOR").getStringValue();
        string gravityDriveString = configuration.get("GRAVITY_DRIVE_GROUP_NAME").getStringValue();

        this.lcdUpdateManager = lcdUpdateManager;
        this.io = io;
        this.driveIdentifier = configuration.get("GRAVITY_DRIVE_IDENTIFIER").getStringValue();

        string groupName = shipNameString + separatorString + gravityDriveString;

        IMyBlockGroup gravityDriveGroup = GridTerminalSystem.GetBlockGroupWithName(groupName);

        if (gravityDriveGroup != null)
        {
            gravityMassBlocks = new List<IMyArtificialMassBlock>();
            gravityDriveGroup.GetBlocksOfType<IMyArtificialMassBlock>(gravityMassBlocks);

            gravityGeneratorBlocks = new List<IMyGravityGenerator>();
            gravityDriveGroup.GetBlocksOfType<IMyGravityGenerator>(gravityGeneratorBlocks);

            io.Echo("Gravity-Drive initialized!");
        }
    }

    public void stop()
    {
        changeAccelerationG(0.0);
    }

    public void changeAcceleration(double acceleration)
    {
        changeAccelerationG(G * acceleration);
    }

    public void changeAccelerationG(double acceleration)
    {
        if (gravityGeneratorBlocks == null || gravityGeneratorBlocks.Count == 0)
        {
            io.Echo("Gravity-Drive not initialized!");
            return;
        }

        currentGravityDriveAcceleration = acceleration;

        foreach (IMyArtificialMassBlock block in gravityMassBlocks)
            block.Enabled = acceleration != 0.0;

        foreach (IMyGravityGenerator block in gravityGeneratorBlocks)
        {
            if (block.CustomName.Contains(driveIdentifier) || block.CustomData.Contains(driveIdentifier))
            {
                block.GravityAcceleration = (float)acceleration;
                block.Enabled = acceleration != 0.0;
            }
        }

        if (acceleration == 0.0)
            io.Echo("Gravity-Drive stopped!");
        else
            io.Echo("Gravity-Drive acceleration set to " + acceleration + " m/s²");

        lcdUpdateManager.setNeedsUpdate();
    }

    public void printInfo(IO io)
    {
        io.Echo("   Gravity Drive: " + getGravityDriveState());
    }

    public string getGravityDriveState()
    {
        if (gravityGeneratorBlocks == null || gravityGeneratorBlocks.Count == 0)
            return "Offline";

        if (currentGravityDriveAcceleration == 0.0)
            return "Inactive";

        return "Active (" + currentGravityDriveAcceleration.ToString("#,##0.0#") + " m/s²)";
    }
}

public class RemoteControl
{
    private readonly IMyRemoteControl remoteControl;
    private readonly TargetControl targetControl;
    private readonly PlanetControl planetControl;
    private readonly LocationControl locationControl;
    private readonly IO io;
    private readonly Configuration configuration;

    public RemoteControl(IMyGridTerminalSystem GridTerminalSystem, TargetControl targetControl, PlanetControl planetControl,
        LocationControl locationControl, Configuration configuration, IO io)
    {
        this.targetControl = targetControl;
        this.planetControl = planetControl;
        this.locationControl = locationControl;
        this.io = io;
        this.configuration = configuration;

        string shipNameString = configuration.get("SHIP_NAME").getStringValue();
        string separatorString = configuration.get("SEPARATOR").getStringValue();
        string remoteString = configuration.get("REMOTE_NAME").getStringValue();

        remoteControl = GridTerminalSystem.GetBlockWithName(shipNameString + separatorString + remoteString) as IMyRemoteControl;

        if (remoteControl != null)
        {
            remoteControl.ClearWaypoints();
            io.Echo("RemoteControl initialized!");
        }
    }

    public bool isAutoPilotEnabled()
    {
        if (remoteControl == null)
            return false;

        return remoteControl.IsAutoPilotEnabled;
    }

    public void startSpace()
    {
        if (remoteControl == null)
        {
            io.Echo("No remote-control found!");
        }
        else
        {
            if (!targetControl.hasTarget())
            {
                io.Echo("You have to set a target first!");
            }
            else
            {
                remoteControl.ClearWaypoints();
                remoteControl.AddWaypoint(targetControl.getTargetLocation(), "Target");
                remoteControl.SetAutoPilotEnabled(true);

                io.Echo("Remote control started for straight flight!");
            }
        }
    }

    public void startPlanet()
    {
        if (remoteControl == null)
        {
            io.Echo("No remote-control found!");
        }
        else
        {
            if (!targetControl.hasTarget())
            {
                io.Echo("You have to set a target first!");
            }
            else
            {
                if (planetControl.getPlanet() == null)
                {
                    io.Echo("You have to set a planet first!");
                }
                else
                {
                    remoteControl.ClearWaypoints();

                    List<Vector3D> straightVectors = berechneVectorenInGeraderLinie();

                    if (straightVectors != null)
                    {
                        List<Vector3D> vectorsAroundPlanet = berechneVectorenUmPlaneten(
                            straightVectors, planetControl.getHeightAbovePlanet());

                        if (vectorsAroundPlanet != null)
                        {
                            int i = 0;
                            foreach (Vector3D v in vectorsAroundPlanet)
                                remoteControl.AddWaypoint(v, "Target " + (++i));

                            remoteControl.SetAutoPilotEnabled(true);

                            io.Echo("Remote control started for flight around planet!");
                        }
                        else
                        {
                            io.Echo("Remote control flight could not be started!");
                        }
                    }
                    else
                    {
                        io.Echo("Remote control flight could not be started!");
                    }
                }
            }
        }
    }

    private List<Vector3D> berechneVectorenInGeraderLinie()
    {
        if (!targetControl.hasTarget())
        {
            io.Echo("You need a target for calculating a path!");
            return null;
        }

        List<Vector3D> vectoren = new List<Vector3D>();

        Vector3D position = locationControl.getFrontPosition();

        double distanceBetweenPoints = configuration.get("MAX_DISTANCE_PLANETARY_TARGETING_WAYPOINTS").getDoubleValue();

        Vector3D target = targetControl.getTargetLocation();

        double distance = Vector3D.Distance(target, position);
        int stepCount = (int)(distance / distanceBetweenPoints);

        if (stepCount == 0)
        {
            io.Echo("Target must be at least " + distanceBetweenPoints.ToString("#,##0.0#") + " m away!");
            return null;
        }

        double restDistance = distance - (stepCount * distanceBetweenPoints);
        distanceBetweenPoints += (restDistance / stepCount);

        Vector3D v1 = target - position;

        for (int i = 1; i <= stepCount; i++)
        {
            Vector3D vector = v1 * ((distanceBetweenPoints * i) / distance);
            vectoren.Add(vector + position);
        }

        return vectoren;
    }

    private List<Vector3D> berechneVectorenUmPlaneten(List<Vector3D> straightVectors, double distance)
    {
        Vector3D? planet = planetControl.getPlanet();

        if (planet == null)
        {
            io.Echo("You need to set a planet for calculation!");
            return null;
        }

        List<Vector3D> vectoren = new List<Vector3D>();

        Vector3D center = planet.Value;

        foreach (Vector3D v in straightVectors)
        {
            double distanceVCenter = Vector3D.Distance(v, center);

            Vector3D v1 = v - center;
            Vector3D vector = v1 * (distance / distanceVCenter);
            vectoren.Add(vector + center);
        }

        return vectoren;
    }

    public void stop()
    {
        if (remoteControl == null)
        {
            io.Echo("No remote-control found!");
        }
        else
        {
            remoteControl.SetAutoPilotEnabled(false);
            remoteControl.ClearWaypoints();
        }
    }

    public void printInfo(IO io)
    {
        io.Echo("   Remove Control: " + getRemoteControlState());
    }

    public string getRemoteControlState()
    {
        if (remoteControl == null)
            return "Offline";

        if (isAutoPilotEnabled())
            return "Active";

        return "Inactive";
    }
}

public class LcdUpdateManager
{
    private readonly List<IMyTextSurface> panels = null;

    private readonly PanelIO panelIO;
    private readonly Program program;
    private readonly Configuration configuration;

    private int page = 1;

    private bool needsUpdate;

    public LcdUpdateManager(Program program, IO echoIO, IMyGridTerminalSystem GridTerminalSystem, Configuration configuration)
    {
        this.panelIO = new PanelIO();
        this.configuration = configuration;
        this.program = program;

        string shipNameString = configuration.get("SHIP_NAME").getStringValue();
        string separatorString = configuration.get("SEPARATOR").getStringValue();
        string panelsString = configuration.get("PANELS").getStringValue();

        page = configuration.get("Page").getIntValue();

        List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(
            shipNameString + separatorString + panelsString);

        if (group != null)
        {
            group.GetBlocksOfType<IMyTextPanel>(blocks);

            if (blocks.Count != 0)
            {
                blocks.Sort(new SortByNameComp());

                panels = new List<IMyTextSurface>();

                foreach (IMyTerminalBlock block in blocks)
                    if (block is IMyTextPanel)
                    {
                        IMyTextPanel panel = block as IMyTextPanel;

                        panels.Add(panel);
                    }

                echoIO.Echo(panels.Count + " Panels initialized!");
            }
        }
    }

    public void nextPage()
    {
        page++;

        if (page > MAX_PAGE)
            page = 1;

        var configurationPage = configuration.get("Page");
        configurationPage.setIntValue(page);
        configuration.save();

        setNeedsUpdate();
    }

    public void prevPage()
    {
        page--;

        if (page < 1)
            page = MAX_PAGE;

        var configurationPage = configuration.get("Page");
        configurationPage.setIntValue(page);
        configuration.save();

        setNeedsUpdate();
    }

    public void setNeedsUpdate()
    {
        needsUpdate = true;
    }

    public void updateIfNeeded()
    {
        if (panels == null)
            return;

        if (!needsUpdate)
            return;

        int i = page;

        foreach (IMyTextSurface panel in panels)
        {
            panel.ContentType = ContentType.TEXT_AND_IMAGE;
            panel.TextPadding = 0;

            panel.WriteText("", false);

            panelIO.setPanel(panel);

            program.printInfo(panelIO, i);

            panelIO.setPanel(null);

            i++;

            if (i > MAX_PAGE)
                i = 1;
        }

        needsUpdate = false;
    }
}


public interface IO
{
    void Echo(string text);

    void EchoTitle(string text);
}

public class EchoIO : IO
{
    private readonly MyGridProgram program;

    public EchoIO(MyGridProgram program)
    {
        this.program = program;
    }

    public void Echo(string text)
    {
        program.Echo(text);
    }

    public void EchoTitle(string text)
    {
        Echo("=== " + text + "===");
        Echo("");
    }
}

public class PanelIO : IO
{
    private IMyTextSurface panel;

    public void setPanel(IMyTextSurface panel)
    {
        this.panel = panel;
    }

    public void Echo(string text)
    {
        if (panel == null)
            throw new Exception("Textpanel for Output not set!");

        panel.WriteText(text + "\n", true);
    }

    public void EchoTitle(string text)
    {
        Echo(text);
        Echo("----------------------------------------------------------------------------------------------------------------------------------------");
        Echo("");
    }
}

private static class GpsParser
{
    public static Vector3D fromGpsStringNullSafe(IO io, string gpsString)
    {
        Vector3D? gps = fromGpsString(gpsString);
        if (gps == null)
        {
            io.Echo("invalid GPS coordinates!");

            return Vector3D.Zero;
        }

        return gps.Value;
    }

    public static Vector3D? fromGpsString(string gpsString)
    {
        if (!gpsString.ToLower().StartsWith("gps:"))
            return null;

        string[] stringSeparators = new string[] { ":" };

        string[] splits = gpsString.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (splits.Length != 5)
            return null;

        double x;
        double y;
        double z;

        double.TryParse(splits[2], out x);
        double.TryParse(splits[3], out y);
        double.TryParse(splits[4], out z);

        return new Vector3D(x, y, z);
    }

    public static string toGpsString(Vector3D targetNotNull)
    {
        return toGpsString(targetNotNull, "Target");
    }

    public static string toGpsString(Vector3D targetNotNull, string name)
    {
        return "GPS:" + name + ":" + targetNotNull.X.ToString("0.000")
               + ":" + targetNotNull.Y.ToString("0.000") + ":" + targetNotNull.Z.ToString("0.000") + ":";
    }
}

public abstract class Configuration
{
    private Dictionary<string, AbstractConfigurationEntry> dictionary = new Dictionary<string, AbstractConfigurationEntry>();
    private readonly IMyProgrammableBlock me;

    public Configuration(IMyProgrammableBlock me)
    {
        this.me = me;
        initConfiguration();
    }

    protected abstract void createBaseConfiguration(Dictionary<string, AbstractConfigurationEntry> dictionary);

    private void initConfiguration()
    {
        createBaseConfiguration(dictionary);
        string[] lines = me.CustomData.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            if (line.Trim() == "")
                continue;
            string[] entry = line.Split(new string[] { "=" }, StringSplitOptions.None);
            if (entry == null || entry.Length != 2)
                throw new Exception("Configuration '" + line + "' is invalid!");
            string key = entry[0];
            string value = entry[1];
            AbstractConfigurationEntry configEntry = get(key);
            if (configEntry != null)
                configEntry.parseString(value);
        }
    }

    public AbstractConfigurationEntry get(string key)
    {
        if (!dictionary.ContainsKey(key))
            return null;
        return dictionary[key];
    }

    public void save()
    {
        me.CustomData = "";
        foreach (string key in dictionary.Keys)
            me.CustomData += key + "=" + dictionary[key].toString() + "\n";
    }
}

public class StringConfigurationEntry : AbstractConfigurationEntry
{
    private string value;

    public StringConfigurationEntry(string value)
    {
        this.value = value;
    }

    public override void setStringValue(string value)
    {
        this.value = value;
    }

    public override string getStringValue()
    {
        return value;
    }

    public override void parseString(string valueAsString)
    {
        setStringValue(valueAsString);
    }

    public override string toString()
    {
        return value;
    }
}

public class DoubleConfigurationEntry : AbstractConfigurationEntry
{
    private double value;

    public DoubleConfigurationEntry(double value)
    {
        this.value = value;
    }

    public override void setDoubleValue(double value)
    {
        this.value = value;
    }

    public override double getDoubleValue()
    {
        return value;
    }

    public override void parseString(string valueAsString)
    {
        double.TryParse(valueAsString, out value);
    }

    public override string toString()
    {
        return value.ToString();
    }
}

public class FloatConfigurationEntry : AbstractConfigurationEntry
{
    private float value;

    public FloatConfigurationEntry(float value)
    {
        this.value = value;
    }

    public override void setFloatValue(float value)
    {
        this.value = value;
    }

    public override float getFloatValue()
    {
        return value;
    }

    public override void parseString(string valueAsString)
    {
        float.TryParse(valueAsString, out value);
    }

    public override string toString()
    {
        return value.ToString();
    }
}

public class IntegerConfigurationEntry : AbstractConfigurationEntry
{
    private int value;

    public IntegerConfigurationEntry(int value)
    {
        this.value = value;
    }

    public override void setIntValue(int value)
    {
        this.value = value;
    }

    public override int getIntValue()
    {
        return value;
    }

    public override void parseString(string valueAsString)
    {
        int.TryParse(valueAsString, out value);
    }

    public override string toString()
    {
        return value.ToString();
    }
}

public class BooleanConfigurationEntry : AbstractConfigurationEntry
{
    private bool value;

    public BooleanConfigurationEntry(bool value)
    {
        this.value = value;
    }

    public override void setBooleanValue(bool value)
    {
        this.value = value;
    }

    public override bool getBooleanValue()
    {
        return value;
    }

    public override void parseString(string valueAsString)
    {
        bool.TryParse(valueAsString, out value);
    }

    public override string toString()
    {
        return value.ToString();
    }
}

public abstract class AbstractConfigurationEntry
{
    public virtual void setStringValue(string value)
    {
        throw new Exception("This type does not accept string-Type");
    }

    public virtual string getStringValue()
    {
        throw new Exception("This type does not accept string-Type");
    }

    public virtual void setDoubleValue(double value)
    {
        throw new Exception("This type does not accept Double-Type");
    }

    public virtual double getDoubleValue()
    {
        throw new Exception("This type does not accept Double-Type");
    }

    public virtual void setFloatValue(float value)
    {
        throw new Exception("This type does not accept Float-Type");
    }

    public virtual float getFloatValue()
    {
        throw new Exception("This type does not accept Float-Type");
    }

    public virtual void setIntValue(int value)
    {
        throw new Exception("This type does not accept Integer-Type");
    }

    public virtual int getIntValue()
    {
        throw new Exception("This type does not accept Integer-Type");
    }

    public virtual void setBooleanValue(bool value)
    {
        throw new Exception("This type does not accept Boolean-Type");
    }

    public virtual bool getBooleanValue()
    {
        throw new Exception("This type does not accept Boolean-Type");
    }

    public abstract void parseString(string valueAsString);
    public abstract string toString();
}

public class SortByNameComp : IComparer<IMyTerminalBlock>
{
    public int Compare(IMyTerminalBlock block1, IMyTerminalBlock block2)
    {
        return block1.CustomName.CompareTo(block2.CustomName);
    }
}