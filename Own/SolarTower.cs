/*
 * Solar Tower Controller
 *
 * Rotates the big solar panel on the top of base tower towards the Sun.
 *
 * Creative mode:
 * ==============
 *
 * Paste blueprint "Solar Tower"
 *
 * Survival mode:
 * ==============
 *
 * 1. Project blueprint "Solar Tower Printable"
 * 2. Print or build the tower starting from the red block next to its bottom connector.
 * 3. Add the missing rotor bases, name them the following way:
 *    - At the top of the base tower: "Solar Tower Rotor Up"
 *    - Hinge blue rotor head: "Solar Tower Rotor Left"
 *    - Hinge green rotor head: "Solar Tower Rotor Right"
 * 4. Set both velocity and power of all rotors to zero.
 * 5. Lock all three rotors and turn them off.
 * 6. Disconnect the subgrids by removing the red blocks.
 * 7. Attach the rotor heads to their bases.
 * 8. Assign all three rotors to a new group: "Solar Tower Rotors"
 * 9. Set rotor limits:
 *    - Solar Tower Rotor Up: Keep unlimited
 *    - Solar Tower Rotor Left: -135 .. 85
 *    - Solar Tower Rotor Right: -85 .. 135
 * 10. Unlock all three rotors.
 * 11. Remove all remaining red blocks, they are only for assembly.
 * 12. Run the programmable block.
 *
 * Gyroscope axes
 * ==============
 *
 * Yaw: Around the base's axis, uses the up rotor
 * Pitch: Around the hinge, uses the left and right rotors
 * Roll: Keep at zero, no rotor for that
 *
 * Programmatic control can be disabled by turning off the gyro overrides.
 *
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Skeleton;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Profiler;
using VRageMath;
using ContentType = VRage.Game.GUI.TextPanel.ContentType;
using IMyBatteryBlock = Sandbox.ModAPI.Ingame.IMyBatteryBlock;
using IMyBlockGroup = Sandbox.ModAPI.Ingame.IMyBlockGroup;
using IMyCargoContainer = Sandbox.ModAPI.Ingame.IMyCargoContainer;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyGasTank = Sandbox.Game.Entities.Interfaces.IMyGasTank;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyTextPanel = Sandbox.ModAPI.Ingame.IMyTextPanel;
using IMyMotorAdvancedRotor = Sandbox.ModAPI.Ingame.IMyMotorAdvancedRotor;

namespace SolarTower
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        // Config

        private const string BATTERIES = "Solar Tower Batteries";
        private const string PANELS = "Solar Tower Panels";
        private const string GYROSCOPES = "Solar Tower Gyroscopes";

        private const UpdateFrequency UPDATE_FREQUENCY = UpdateFrequency.Update100;

        // Debugging

        enum LogSeverity
        {
            Ok,
            Warning,
            Error,
        }

        private bool DEBUG = true;
        private LogSeverity highestLogLogSeverity = LogSeverity.Ok;
        private readonly StringBuilder log = new StringBuilder();

        private void Log(string formatString, params object[] args)
        {
            log.AppendFormat(formatString + "\n", args);
        }

        private void Debug(string formatString, params object[] args)
        {
            if (DEBUG)
            {
                Log("D: " + formatString, args);
            }
        }

        private void Warning(string formatString, params object[] args)
        {
            Log("W: " + formatString, args);
            IncreaseSeverity(LogSeverity.Warning);
        }

        private void Error(string formatString, params object[] args)
        {
            Log("E: " + formatString, args);
            IncreaseSeverity(LogSeverity.Error);
        }

        private void ClearLog()
        {
            highestLogLogSeverity = LogSeverity.Ok;
            log.Clear();
        }

        private void ShowLog()
        {
            Echo(log.ToString());
            Surface.WriteText(display + highestLogLogSeverity.ToString());
        }

        private void IncreaseSeverity(LogSeverity severity)
        {
            if (highestLogLogSeverity < severity)
            {
                highestLogLogSeverity = severity;
            }
        }

        private IMyTextSurface Surface
        {
            get
            {
                return Me.GetSurface(0);
            }
        }

        // Blocks

        private readonly List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        private readonly List<IMyTerminalBlock> panels = new List<IMyTerminalBlock>();
        private readonly List<IMyGyro> gyroscopes = new List<IMyGyro>();

        // State

        private double totalCharge;  // MWh

        private double previousTotalPower;  // W
        private double totalPower;  // W

        private double previousYaw;  // degrees
        private double yaw;  // degrees

        private double previousPitch;  // degrees
        private double pitch;  // degrees

        private string display;

        // Parameter parsing (commands)

        private enum Command
        {
            Default,
            Unknown,
        }

        private Command ParseCommand(string argument)
        {
            switch (argument)
            {
                case "":
                    return Command.Default;
                default:
                    return Command.Unknown;
            }
        }

        public Program()
        {
            Initialize();
            Load();
        }

        private void Initialize()
        {
            Surface.ContentType = ContentType.TEXT_AND_IMAGE;
            Surface.FontSize = 2f;

            Reset();

            Runtime.UpdateFrequency = highestLogLogSeverity == LogSeverity.Error ? UpdateFrequency.None : UPDATE_FREQUENCY;
        }

        private void Reset()
        {
            GridTerminalSystem.GetBlockGroupWithName(BATTERIES).GetBlocksOfType<IMyBatteryBlock>(batteries);
            GridTerminalSystem.GetBlockGroupWithName(PANELS).GetBlocksOfType<IMyTerminalBlock>(panels);
            GridTerminalSystem.GetBlockGroupWithName(GYROSCOPES).GetBlocksOfType<IMyGyro>(gyroscopes);

            if (batteries.Count == 0)
            {
                Error("No batteries in group {0}", BATTERIES);
                return;
            }
            if (panels.Count == 0)
            {
                Error("No solar panels in group {0}", PANELS);
                return;
            }
            if (gyroscopes.Count == 0)
            {
                Error("No gyroscopes in group {0}", GYROSCOPES);
                return;
            }

            UpdateInputs();
            UpdateDisplay();
        }

        private void UpdateInputs()
        {
            SummarizeBatteryCharge();
            SummarizeSolarPower();
            UpdateGyroAngles();
        }

        private void SummarizeBatteryCharge()
        {
            totalCharge = 0;
            foreach (var battery in batteries)
            {
                totalCharge += battery.CurrentStoredPower;
            }
        }

        private void SummarizeSolarPower()
        {
            previousTotalPower = totalPower;
            totalPower = 0;
            foreach (var panel in panels)
            {
                if (!panel.IsFunctional)
                {
                    Warning("Broken solar panel: {0}", panel.CustomName);
                    continue;
                }

                if (!panel.IsWorking)
                {
                    Warning("Disabled solar panel: {0}", panel.CustomName);
                    continue;
                }

                totalPower += ParseSolarPanelPower(panel.DetailedInfo);
            }
        }

        /*
         * Type: Solar Panel
         * Max Output: 120.67 kW
         * Current Output: 0 W
         */
        private readonly System.Text.RegularExpressions.Regex maxOutputRegex = new System.Text.RegularExpressions.Regex(@"Max Output: ([\d\.]+) (W|kW|MW)");

        private double ParseSolarPanelPower(string panelDetailedInfo)
        {
            var match = maxOutputRegex.Match(panelDetailedInfo);
            if (!match.Success)
            {
                return 0;
            }

            var value = double.Parse(match.Groups[1].Value);

            switch (match.Groups[2].Value)
            {
                case "W":
                    return value;
                case "kW":
                    return value * 1e3;
                case "MW":
                    return value * 1e6;
            }

            return 0;
        }

        private void UpdateGyroAngles()
        {
            previousYaw = yaw;
            previousPitch = pitch;

            yaw = gyroscopes[0].Yaw;
            pitch = gyroscopes[0].Pitch;
        }

        private void Load()
        {
            // Load state from Storage here
        }

        public void Save()
        {
            // Save state to Storage here
        }

        public void Main(string argument, UpdateType updateSource)
        {
            ClearLog();

            Debug("Main {0} {1}", updateSource, argument);

            switch (updateSource)
            {
                case UpdateType.None:
                case UpdateType.Terminal:
                case UpdateType.Trigger:
                case UpdateType.Antenna:
                case UpdateType.Mod:
                case UpdateType.Script:
                case UpdateType.Once:
                case UpdateType.IGC:
                    try {
                        ProcessCommand(argument);
                    }
                    catch (Exception e)
                    {
                        Error(e.ToString());
                    }
                    break;

                case UpdateType.Update1:
                case UpdateType.Update10:
                case UpdateType.Update100:
                    try {
                        PeriodicProcessing();
                    }
                    catch (Exception e)
                    {
                        Error(e.ToString());
                    }

                    if (highestLogLogSeverity >= LogSeverity.Error)
                    {
                        StopPeriodicProcessing();
                    }

                    break;
            }

            Log(highestLogLogSeverity.ToString());

            ShowLog();
        }

        private void StopPeriodicProcessing()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        private void ProcessCommand(string argument)
        {
            var command = ParseCommand(argument);
            switch (command)
            {
                case Command.Default:
                    Reset();
                    break;

                default:
                    Error("Unknown command");
                    break;
            }
        }

        private void PeriodicProcessing()
        {
            UpdateInputs();
            Control();
            UpdateDisplay();
        }

        private void Control()
        {
            var powerDelta = totalPower - previousTotalPower;
            var yawTorque = AxisControl(powerDelta, yaw, previousYaw);
            var pitchTorque = AxisControl(powerDelta, pitch, previousPitch);
            ApplyTorque(yawTorque, pitchTorque);
        }

        private void ApplyTorque(double yawTorque, double pitchTorque)
        {
            foreach (var gyroscope in gyroscopes)
            {
                if (!gyroscope.GyroOverride)
                {
                    continue;
                }

                if (gyroscope.CustomName.Contains("Yaw"))
                {
                    if (yawTorque > 0)
                    {
                        gyroscope.ApplyAction("IncreaseYaw");
                    }
                    if (yawTorque < 0)
                    {
                        gyroscope.ApplyAction("DecreaseYaw");
                    }
                }

                if (gyroscope.CustomName.Contains("Pitch"))
                {
                    if (pitchTorque > 0)
                    {
                        gyroscope.ApplyAction("IncreasePitch");
                    }
                    if (pitchTorque < 0)
                    {
                        gyroscope.ApplyAction("DecreasePitch");
                    }
                }
            }
        }

        private double AxisControl(double powerDelta, double currentAngle, double previousAngle)
        {
            var angleDelta = RotationDelta(currentAngle, previousAngle);
            var gradient = Gradient(powerDelta, angleDelta);
            return gradient > 0 ? 1 : (gradient < 0 ? -1 : 0);
        }

        private double Gradient(double powerDelta, double angleDelta)
        {
            Log("angleDelta {0}", angleDelta);
            if (Math.Abs(angleDelta) < 0.01)
            {
                return 1;
            }

            var gradient = powerDelta / angleDelta;
            Log("gradient {0}", gradient);
            if (Math.Abs(gradient) < 0.01)
            {
                return 0;
            }

            return gradient;
        }

        private static double RotationDelta(double current, double previous)
        {
            var delta = current - previous;

            if (delta < -180)
            {
                return delta + 360;
            }

            if (delta > 180)
            {
                return delta - 360;
            }

            return delta;
        }

        private void UpdateDisplay()
        {
            display = string.Format(
                "{0} panels \n{1:n1} MW\n{2} batteries\n{3:n1} MWh\n",
                panels.Count, totalPower * 1e-6, batteries.Count, totalCharge);
        }

        #endregion
    }
}