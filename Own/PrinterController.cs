/* Printer Controller
 *
 * Controls a large block printer to automatically finish printing a blueprint.
 *
 * Supports pistons moving a grid of welders.
 *
 * Groups:
 * - Printer Welders: All welders in the printer
 * - Printer Pistons X: Welder movement
 * - Printer Pistons Y: Welder movement
 * - Printer Pistons Z: Pulling out the print
 *
 * Projector: "Printer Projector"
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

namespace PrinterController
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        // Config

        private const string WELDERS = "Printer Welders";
        private const string PISTONS_X = "Printer Pistons X";
        private const string PISTONS_Y = "Printer Pistons Y";
        private const string PISTONS_Z = "Printer Pistons Z";
        private const string PROJECTOR_NAME = "Printer Projector";

        private const UpdateFrequency UPDATE_FREQUENCY = UpdateFrequency.Update10;

        private const float PISTON_POSITION_TOLERANCE = 0.01f;
        private const int MINIMUM_ROUNDS_PER_LAYER = 2;
        private const float BLOCK_SIZE = 2.5f;
        private const float MAX_PISTON_VELOCITY = 5f;
        private const float MAX_PISTON_POSITION = 10f;
        private const float PISTON_Z_ADVANCE_VELOCITY = 1f;
        private const float PISTON_Z_RESET_VELOCITY = 5f;

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
            Surface.WriteText(log.ToString());
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

        private List<IMyShipWelder> welders = new List<IMyShipWelder>();
        private List<IMyPistonBase> pistonsX = new List<IMyPistonBase>();
        private List<IMyPistonBase> pistonsY = new List<IMyPistonBase>();
        private List<IMyPistonBase> pistonsZ = new List<IMyPistonBase>();
        private IMyProjector projector = null;

        // State

        private bool printing;
        private int xyPhase;
        private int remainingBlocks;
        private int roundsInLayer;

        // Parameter parsing (commands)

        private enum Command
        {
            Default,
            Start,
            Stop,
            Reset,
            Unknown,
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
            Surface.Font = "InfoMessageBoxText";
            Surface.FontSize = 1.3f;

            Reset();

            Runtime.UpdateFrequency = highestLogLogSeverity == LogSeverity.Error ? UpdateFrequency.None : UPDATE_FREQUENCY;
        }

        private void Reset()
        {
            printing = false;
            xyPhase = 0;
            remainingBlocks = 0;
            roundsInLayer = 0;

            GridTerminalSystem.GetBlockGroupWithName(WELDERS).GetBlocksOfType(welders);

            GridTerminalSystem.GetBlockGroupWithName(PISTONS_X).GetBlocksOfType(pistonsX);
            GridTerminalSystem.GetBlockGroupWithName(PISTONS_Y).GetBlocksOfType(pistonsY);
            GridTerminalSystem.GetBlockGroupWithName(PISTONS_Z).GetBlocksOfType(pistonsZ);

            projector = GridTerminalSystem.GetBlockWithName(PROJECTOR_NAME) as IMyProjector;
            if (projector == null)
            {
                Error("Missing projector: {0}", PROJECTOR_NAME);
                return;
            }

            Stop();
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
                    Log(highestLogLogSeverity.ToString());
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

                case Command.Start:
                    Start();
                    break;

                case Command.Stop:
                    Stop();
                    break;

                case Command.Reset:
                    Stop();
                    Reset();
                    RetractZ();
                    break;

                default:
                    Error("Unknown command");
                    break;
            }
        }

        private void Start()
        {
            projector.ApplyAction("OnOff_On");

            if (!projector.IsProjecting)
            {
                Error("Load a blueprint into projector:");
                Log(PROJECTOR_NAME);
                return;
            }

            ApplyToAll(welders, "OnOff_On");
            ApplyToAll(pistonsX, "Extend");
            ApplyToAll(pistonsY, "Retract");

            StartZ();
            printing = true;
        }

        private void StartZ()
        {
            var velocity = Math.Min(MAX_PISTON_VELOCITY, PISTON_Z_ADVANCE_VELOCITY / pistonsZ.Count);
            foreach (var piston in pistonsZ)
            {
                piston.MaxLimit = piston.CurrentPosition;
                piston.Velocity = velocity;
            }
        }

        private void Stop()
        {
            ApplyToAll(pistonsX, "Retract");
            ApplyToAll(pistonsY, "Retract");
            ApplyToAll(welders, "OnOff_Off");
            printing = false;
        }

        private void PeriodicProcessing()
        {
            if (!printing) return;

            ClearLog();
            try
            {
                if (!projector.IsProjecting)
                {
                    Stop();
                    return;
                }

                MoveWeldersAround();
            }
            finally
            {
                Log(highestLogLogSeverity.ToString());
            }
        }

        private void MoveWeldersAround()
        {
            var pistonX = pistonsX.First();
            var pistonY = pistonsY.First();

            switch (xyPhase)
            {
                case 0:
                    if (IsAtExtreme(pistonX))
                    {
                        ApplyToAll(pistonsY, "Extend");
                        xyPhase = 1;
                    }

                    break;

                case 1:
                    if (IsAtExtreme(pistonY))
                    {
                        ApplyToAll(pistonsX, "Retract");
                        xyPhase = 2;
                    }

                    break;

                case 2:
                    if (IsAtExtreme(pistonX))
                    {
                        ApplyToAll(pistonsY, "Retract");
                        xyPhase = 3;
                    }

                    break;

                case 3:
                    if (IsAtExtreme(pistonY))
                    {
                        ApplyToAll(pistonsX, "Extend");
                        xyPhase = 0;
                        MoveBuildAheadWhenReady();
                    }

                    break;
            }
        }

        private void MoveBuildAheadWhenReady()
        {
            roundsInLayer++;

            var pistonZ = pistonsZ.First();
            var noProgress = remainingBlocks == projector.RemainingBlocks;
            var movingAhead = pistonZ.MaxLimit > pistonZ.CurrentPosition + PISTON_POSITION_TOLERANCE;
            var minimumRoundsPassed = roundsInLayer >= MINIMUM_ROUNDS_PER_LAYER;

            if (minimumRoundsPassed && noProgress && !movingAhead)
            {
                if (pistonZ.CurrentPosition >= MAX_PISTON_POSITION - PISTON_POSITION_TOLERANCE)
                {
                    Stop();
                    Warning("Unfinished printing, Z pistons reached maximum.");
                    return;
                }

                AdvanceZ(BLOCK_SIZE);

                roundsInLayer = 0;
            }

            remainingBlocks = projector.RemainingBlocks;
        }

        private void AdvanceZ(float delta)
        {
            var oneBlock = delta / pistonsZ.Count;
            foreach (var piston in pistonsZ)
            {
                piston.MaxLimit = Math.Min(10f, piston.MaxLimit + oneBlock);
            }
        }

        private void RetractZ()
        {
            var velocity = -PISTON_Z_RESET_VELOCITY / pistonsZ.Count;
            foreach (var piston in pistonsZ)
            {
                piston.Velocity = velocity;
                piston.MaxLimit = MAX_PISTON_POSITION;
            }
        }

        private bool IsAtExtreme(IMyPistonBase piston)
        {
            var extreme = piston.Velocity > 0 ? piston.MaxLimit : piston.MinLimit;
            return Math.Abs(piston.CurrentPosition - extreme) < PISTON_POSITION_TOLERANCE;
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