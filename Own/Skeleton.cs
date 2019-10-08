/*
 * This is a skeleton for developing Space Engineers programmable block scripts
 *
 * Auto-completion needs Space Engineers being installed to its standard location in Steam.
 *
 * Make a copy of this script first.
 * Change the name of the namespace to your topic.
 * Edit your script in JetBrains Rider or Microsoft Visual Studio.
 * Fill in the missing code where you see "TODO" below.
 * Make sure your IDE does not detect any errors, look for red/yellow highlights.
 * Copy-paste the contents of the CodeEditor region into the programmable block in Space Engineers.
 * Check the code in Space Engineers, it should be ready to run if no compilation errors reported.
 *
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

namespace Skeleton
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        // Config

        // TODO: Add configuration variables here
        //Examples:
        //private const string GRINDER_GROUP = "Grinders";
        //private const string PISTON_GROUP = "Pistons";

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

        // TODO: Add lists of blocks here
        //Examples:
        //private List<IMyShipGrinder> grinders = new List<IMyShipGrinder>();
        //private List<IMyPistonBase> pistons = new List<IMyPistonBase>();

        // State

        // TODO: Add state variables here
        //Examples:
        //private bool grindersRunning = false;
        //private float pistonPosition = 0f;

        // Parameter parsing (commands)

        private enum Command
        {
            Default,
            Start,
            Stop,
            Reset,
            Invalid,
        }

        private Command ParseCommand(string argument)
        {
            switch (argument)
            {
                case "":
                    return Command.Default;
                case "start":
                    return Command.Start;
                case "stop":
                    return Command.Stop;
                case "reset":
                    return Command.Reset;
                default:
                    return Command.Invalid;
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

            Reset();

            Runtime.UpdateFrequency = UPDATE_FREQUENCY;
        }

        private void Reset()
        {
            // TODO: Reset state variables here

            // TODO: Find blocks here
            //Examples:
            //GridTerminalSystem.GetBlockGroupWithName(GRINDER_GROUP).GetBlocksOfType<IMyShipGrinder>(grinders);
            //GridTerminalSystem.GetBlockGroupWithName(PISTON_GROUP).GetBlocksOfType<IMyPistonBase>(pistons);
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

            try
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
                        ProcessCommand(argument);
                        break;

                    case UpdateType.Update1:
                    case UpdateType.Update10:
                    case UpdateType.Update100:
                        PeriodicProcessing();
                        break;

                }

                Log("OK");
            }
            catch (Exception e)
            {
                Error(e.ToString());
            }

            ShowLog();
        }

        private void ProcessCommand(string argument)
        {
            // TODO: Add command processing below

            var command = ParseCommand(argument);
            switch (command)
            {
                case Command.Default:
                    // TODO
                    break;

                case Command.Start:
                    // TODO
                    break;

                case Command.Stop:
                    // TODO
                    break;

                case Command.Reset:
                    Reset();
                    break;

                default:
                    Error("Invalid command");
                    break;
            }
        }

        private void PeriodicProcessing()
        {
            // TODO: Add periodic processing here, called only if UpdateFrequency is not set to UpdateType.None
        }

        // Utility functions

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