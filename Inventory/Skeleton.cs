/*
 * This is a skeleton for developing Space Engineers programmable block scripts
 *
 * Autocompletion needs Space Engineers being installed to its standard location in Steam.
 *
 * Make a copy of this script first.
 * Change the name of the namespace to your topic.
 * Edit your script in JetBrains Rider or Microsoft Visual Studio.
 * Fill in the missing code where you see "TODO" below.
 * Make sure your IDE does not highlight any errors. You cannot run the script, though.
 * Copy the contents of the CodeEditor region into the programmable block in Space Engineers.
 * Check the code in Space Engineers, it it reports no errors then it should be ready to run.
 *
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Inventory;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Profiler;
using VRageMath;
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

        // Debugging

        private bool DEBUG = true;
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
        }

        private void Error(string formatString, params object[] args)
        {
            Log("E: " + formatString, args);
        }

        private void ShowLog()
        {
            Echo(log.ToString());
            ClearLog();
        }

        private void ClearLog()
        {
            log.Clear();
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

        // Utility functions

        private static void ApplyToAll(IEnumerable<IMyFunctionalBlock> blocks, string action)
        {
            foreach (var block in blocks)
            {
                block.ApplyAction(action);
            }
        }

        // Parameter parsing (commands)

        private enum Command
        {
            Start,
            Stop,
            Reverse,
            Reset,
            Invalid,
        }

        private Command ParseCommand(string argument)
        {
            switch (argument)
            {
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

        private void ProcessCommand(string argument)
        {
            // TODO: Add command processing below

            var command = ParseCommand(argument);
            switch (command)
            {
                case Command.Start:
                    // TODO
                    break;

                case Command.Stop:
                    // TODO
                    break;

                case Command.Reset:
                    // TODO
                    break;

                default:
                    Error("Invalid command: " + argument);
                    break;
            }
        }

        public Program()
        {
            Initialize();
            Load();
        }

        private void Initialize()
        {
            FindBlocks();

            // TODO: Set the update frequency here, unless you plan to trigger this script by other means
            //Example:
            //Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        private void FindBlocks()
        {
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
            try
            {
                Debug("Main: {0}", argument);

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
                }

                PeriodicProcessing();

                Log("OK");
            }
            catch (Exception e)
            {
                Error(e.ToString());
            }
            ShowLog();
        }

        private void PeriodicProcessing()
        {
            // TODO: Add periodic processing here
        }

        #endregion
    }
}