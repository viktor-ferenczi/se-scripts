/*
 * Shared Inventory
 *
 * Create a programmable block.
 * Copy-paste all code from the CodeEditor region below into the block.
 * Compile and run the code in the block.
 *
 * This program will periodically scan all of your cargo blocks.
 * Updates will be less frequent if you have more cargo blocks.
 *
 * It will make a summary available on the block's CustomData for other
 * compatible blocks to read, so it does not have to be collected again.
 *
 * Hook up LCD panels by putting them into a group named "IGT Own"
 *
 * Panels must have the following in their name (case insensitive):
 * - Resource
 * - Ore
 * - Ingot
 * - Component
 * - Ammo
 *
 * Components may need two LCD panels to fit all text.
 * Panels of the same type are concatenated in ascending name order.
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

namespace IGT_Inventory
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        // Config

        private const string LCD_GROUP = "IGT Own";
        private const UpdateFrequency FREQUENCY = UpdateFrequency.Update100;

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

        private List<IMyShipGrinder> cargoBlocks = new List<IMyCargoContainer>();

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
                    // TODO
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

        #endregion
    }
}