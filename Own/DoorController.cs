/* Door Controller

Controls pairs of sliding doors to prevent opening both of them
at the same time. This is needed to keep up air tightness.

How to use:

Build a programmable block.
Copy-paste all code from the CodeEditor region below into the block.
Compile and run the code in the block.
Build a pair of doors and set the same name on both.
Build more door pairs as needed.
Assign all controlled doors to the "Controlled Doors" group.

Run the program every time after you make changes to the doors.
This is to find and pair the doors again, which is a one time operation.
It can be automated by a timer block running it once every 30 seconds.

This controller works with more than two doors having the same name.
In this case it will let only one of them being opened at a time.

When multiple doors are opened at the exact same time the script will
force close all the doors in that group. It means that air can be
leaked with a small probability. This is the price to pay to avoid the
button which would open the door otherwise.

*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Skeleton;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
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

namespace DoorController
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        // Config

        private const string DOOR_GROUP = "Controlled Doors";
        private const double AUTO_CLOSE_DELAY = 3.0; // s
        private const UpdateFrequency UPDATE_FREQUENCY = UpdateFrequency.Update10;

        // Debugging

        enum LogSeverity
        {
            Ok,
            Warning,
            Error,
        }

        private bool DEBUG = false;
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
            Surface.WriteText(highestLogLogSeverity.ToString());
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

        private readonly List<IMyDoor> doors = new List<IMyDoor>();
        private readonly Dictionary<string, List<IMyDoor>> doorGroups = new Dictionary<string, List<IMyDoor>>();
        private readonly Dictionary<string, DateTime> lastOpened = new Dictionary<string, DateTime>();

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
            Reset();
            Runtime.UpdateFrequency = UPDATE_FREQUENCY;
        }

        private void Reset()
        {
            if (!DEBUG)
            {
                ClearLog();
            }

            doors.Clear();
            GridTerminalSystem.GetBlockGroupWithName(DOOR_GROUP)?.GetBlocksOfType<IMyDoor>(doors);

            GroupDoors();

            Log("Doors: {0}", doors.Count);
            Log("Door groups: {0}", doorGroups.Count);
        }

        private void GroupDoors()
        {
            doorGroups.Clear();
            lastOpened.Clear();
            foreach (var door in doors)
            {
                List<IMyDoor> group = null;
                if (!doorGroups.TryGetValue(door.CustomName, out group))
                {
                    group = new List<IMyDoor>();
                    doorGroups[door.CustomName] = group;
                    lastOpened[door.CustomName] = DateTime.UtcNow;
                }
                group.Add(door);
            }
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
                    ClearLog();

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
            var now = DateTime.UtcNow;
            foreach (var group in doorGroups)
            {
                var doors = group.Value;
                var open = doors.Where(IsOpen).Count();

                switch (open)
                {
                    case 0:
                        ApplyToAll(doors, "OnOff_On");
                        lastOpened[group.Key] = now;
                        break;

                    case 1:
                        var openSince = now - lastOpened[group.Key];
                        if (openSince.TotalSeconds < AUTO_CLOSE_DELAY)
                        {
                            ApplyToAll(doors.Where(IsClosed).Where(IsSteady), "OnOff_Off");
                        }
                        else
                        {
                            ApplyToAll(doors, "Open_Off");
                        }
                        break;

                    default:
                        ApplyToAll(doors, "Open_Off");
                        break;
                }
            }
        }

        private static bool IsOpen(IMyDoor door)
        {
            switch (door.Status)
            {
                case DoorStatus.Opening:
                case DoorStatus.Open:
                    return true;
            }
            return false;
        }

        private static bool IsClosed(IMyDoor door)
        {
            switch (door.Status)
            {
                case DoorStatus.Closing:
                case DoorStatus.Closed:
                    return true;
            }
            return false;
        }

        private static bool IsSteady(IMyDoor door)
        {
            switch (door.Status)
            {
                case DoorStatus.Open:
                case DoorStatus.Closed:
                    return true;
            }

            return false;
        }

        private static void ApplyToAll(IEnumerable<IMyFunctionalBlock> blocks, string action)
        {
            foreach (var block in blocks)
            {
                block.ApplyAction(action);
            }
        }

        #endregion
    }
}