/* Airlock Controller

Controls pairs of sliding doors to prevent opening both of them
at the same time. This is needed to keep up air tightness.

How to use:

Build a programmable block.
Copy-paste all code from the CodeEditor region below into the block.
Compile and run the code in the block.
Build airlocks, set the same name on all doors inside the same airlock.
Assign all doors of the controlled airlocks to the "Controlled Doors" group.

Run the program every time after you make changes to the doors.
This is to find the doors of the airlocks again, which is a one time operation.

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
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;
using Skeleton;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.GameSystems.Conveyors;
using Sandbox.Game.SessionComponents;
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

namespace AirlockController
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        // Config

        // Name of the block group containing all doors of the controlled airlocks
        private const string DOOR_GROUP = "Airlock Doors";

        // Close doors automatically after a delay
        private const double AUTO_CLOSE_DELAY = 1.3; // s

        // Open the other door(s) of the airlock automatically after closing the
        // one opened by a player. This is to help to get through without having
        // to open the other door manually.
        private const bool AUTO_OPEN = true;

        // Door monitoring frequency
        private const UpdateFrequency UPDATE_FREQUENCY = UpdateFrequency.Update10;

        // Debugging

        enum LogSeverity
        {
            Ok,
            Warning,
            Error,
        }

        private static bool DEBUG = false;
        private static LogSeverity highestLogLogSeverity = LogSeverity.Ok;
        private static readonly StringBuilder log = new StringBuilder();

        private static void Log(string formatString, params object[] args)
        {
            log.AppendFormat(formatString + "\n", args);
        }

        private static void Debug(string formatString, params object[] args)
        {
            if (DEBUG)
            {
                Log("D: " + formatString, args);
            }
        }

        private static void Warning(string formatString, params object[] args)
        {
            Log("W: " + formatString, args);
            IncreaseSeverity(LogSeverity.Warning);
        }

        private static void Error(string formatString, params object[] args)
        {
            Log("E: " + formatString, args);
            IncreaseSeverity(LogSeverity.Error);
        }

        private static void ClearLog()
        {
            highestLogLogSeverity = LogSeverity.Ok;
            log.Clear();
        }

        private void ShowLog()
        {
            Echo(log.ToString());
            Surface.WriteText(highestLogLogSeverity.ToString());
        }

        private static void IncreaseSeverity(LogSeverity severity)
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

        private readonly List<IMyDoor> controlledDoors = new List<IMyDoor>();
        private readonly Dictionary<string, Airlock> airlocks = new Dictionary<string, Airlock>();

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

            FindDoors();
            FindAirlocks();

            Close(controlledDoors);
            Enable(controlledDoors);

            Log("Doors: {0}", controlledDoors.Count);
            Log("Airlocks: {0}", airlocks.Count);
        }

        private void FindDoors()
        {
            controlledDoors.Clear();
            GridTerminalSystem.GetBlockGroupWithName(DOOR_GROUP)?.GetBlocksOfType<IMyDoor>(controlledDoors);
        }

        private void FindAirlocks()
        {
            var now = DateTime.UtcNow;
            airlocks.Clear();
            foreach (var door in controlledDoors)
            {
                Airlock airlock;
                if (!airlocks.TryGetValue(door.CustomName, out airlock))
                {
                    airlock = new Airlock(now);
                    airlocks[door.CustomName] = airlock;
                }
                airlock.Add(door);
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
            foreach (var airlock in airlocks.Values)
            {
                if (airlock.Monitor(now))
                {
                    Warning("Airlock breach: {0}", airlock.Name);
                }
            }
        }

        private static bool IsOpen(IMyDoor door)
        {
            return door.Status != DoorStatus.Closed;
        }

        private static void Open(IMyDoor door)
        {
            door.ApplyAction("Open_On");
        }

        private static void Close(IMyDoor door)
        {
            door.ApplyAction("Open_Off");
        }

        private static void Close(IEnumerable<IMyDoor> doors)
        {
            Apply(doors, "Open_Off");
        }

        private static void Enable(IMyFunctionalBlock block)
        {
            block.ApplyAction("OnOff_On");
        }

        private static void Enable(IEnumerable<IMyFunctionalBlock> blocks)
        {
            Apply(blocks, "OnOff_On");
        }

        private static void Disable(IMyFunctionalBlock block)
        {
            block.ApplyAction("OnOff_Off");
        }

        private static void Disable(IEnumerable<IMyFunctionalBlock> blocks)
        {
            Apply(blocks, "OnOff_Off");
        }

        private static void Apply(IEnumerable<IMyFunctionalBlock> blocks, string action)
        {
            foreach (var block in blocks)
            {
                block.ApplyAction(action);
            }
        }

        // Airlock logic

        private enum State
        {
            Sealed,
            Open,
            AutoOpen,
        }

        private class Airlock
        {
            private readonly List<IMyDoor> doors;
            private State state;
            private DateTime since;
            private IMyDoor openDoor;

            private IEnumerable<IMyDoor> closedDoors
            {
                get
                {
                    return doors.Where(door => door != openDoor);
                }
            }

            public Airlock(DateTime now)
            {
                doors = new List<IMyDoor>();
                state = State.Sealed;
                since = now;
                openDoor = null;
            }

            public string Name
            {
                get
                {
                    return doors.First().CustomName;
                }
            }

            public string StateName
            {
                get
                {
                    return state.ToString();
                }
            }

            void SetState(DateTime now, State newState)
            {
                since = now;
                state = newState;
                Debug("{0} {0} {1}", now, StateName, Name);
            }

            public void Add(IMyDoor door)
            {
                doors.Add(door);
            }

            private void Seal()
            {
                Close(doors);
            }

            public bool Monitor(DateTime now)
            {
                var duration = (now - since).TotalSeconds;
                var openDoors = doors.Where(IsOpen).ToList();
                var openCount = openDoors.Count;
                var doorCount = doors.Count;

                if (openCount > 1)
                {
                    SetState(now, State.Open);
                    Seal();
                    return true;
                }

                switch (state)
                {
                    case State.Sealed:
                        if (openCount > 0)
                        {
                            openDoor = openDoors.First();
                            SetState(now, State.Open);
                            Disable(closedDoors);
                        }
                        break;

                    case State.Open:
                        if (openCount > 0)
                        {
                            if (duration >= AUTO_CLOSE_DELAY)
                            {
                                Close(openDoor);
                            }
                        } else {
                            if (AUTO_OPEN && doorCount == 2)
                            {
                                Disable(openDoor);
                                openDoor = closedDoors.First();
                                Enable(openDoor);
                                Open(openDoor);
                                SetState(now, State.AutoOpen);
                            }
                            else
                            {
                                openDoor = null;
                                SetState(now, State.Sealed);
                                Enable(doors);
                            }
                        }
                        break;

                    case State.AutoOpen:
                        if (openCount > 0)
                        {
                            if (duration >= AUTO_CLOSE_DELAY)
                            {
                                Close(openDoor);
                            }
                        } else {
                            openDoor = null;
                            SetState(now, State.Sealed);
                            Enable(doors);
                        }
                        break;
                }

                return false;
            }
        }

        #endregion
    }
}