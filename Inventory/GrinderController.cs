using System;
using System.Collections.Generic;
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

namespace GrinderPad
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        // Config

        private const string GRINDER_GROUP = "Grinders";
        private const string PISTON_GROUP = "Grinder Pistons";

        private const float GRINDING_VELICITY = 5f; // m/s
        private const float PARKING_VELOCITY = 10f; // m/s
        private const float PISTON_TOLERANCE = 0.2f; // m

        // Debugging

        private bool DEBUG = true;

        private readonly StringBuilder log = new StringBuilder();

        private void Log(string formatString, params object[] args)
        {
            log.AppendFormat(formatString + "\n", args);
        }

        private void ShowLog()
        {
            Echo(log.ToString());
            log.Clear();
        }

        private void Debug(string formatString, params object[] args)
        {
            if (DEBUG)
            {
                Log("D: " + formatString, args);
            }
        }

        private void Error(string formatString, params object[] args)
        {
            Log("E: " + formatString, args);
        }

        // Blocks

        private List<IMyShipGrinder> grinders = new List<IMyShipGrinder>();
        private List<IMyPistonBase> pistons = new List<IMyPistonBase>();
        private float sumMinExtension = 0f;
        private float sumMaxExtension = 0f;

        private void FindBlocks()
        {
            Debug("FindBlocks");

            GridTerminalSystem.GetBlockGroupWithName(GRINDER_GROUP).GetBlocksOfType<IMyShipGrinder>(grinders);
            GridTerminalSystem.GetBlockGroupWithName(PISTON_GROUP).GetBlocksOfType<IMyPistonBase>(pistons);

            if (grinders.Count < 1)
            {
                Panic("Grinder group not found: " + GRINDER_GROUP);
                return;
            }

            if (pistons.Count < 1)
            {
                Panic("Piston group not found: " + PISTON_GROUP);
                return;
            }

            if (pistons.Count % 2 != 0)
            {
                Panic("Number of pistons must be even in group: " + PISTON_GROUP);
                return;
            }

            if (!IsAllFunctional(grinders))
            {
                Panic("Broken grinder(s) in group " + GRINDER_GROUP);
                return;
            }

            if (!IsAllFunctional(pistons))
            {
                Panic("Broken pistons(s) in group " + PISTON_GROUP);
                return;
            }

            sumMinExtension = pistons.Sum(piston => piston.MinLimit);
            sumMaxExtension = pistons.Sum(piston => piston.MaxLimit);

            Debug("FindBlocks OK");
        }

        private static bool IsAllFunctional(IEnumerable<IMyFunctionalBlock> blocks)
        {
            return blocks.All(block => block.IsFunctional);
        }

        private void EnableGrinders()
        {
            ApplyToAll(grinders, "OnOff_On");
        }

        private void EnablePistons()
        {
            ApplyToAll(pistons, "OnOff_On");
        }

        private void DisableGrinders()
        {
            ApplyToAll(grinders, "OnOff_Off");
        }

        private void DisablePistons()
        {
            ApplyToAll(pistons, "OnOff_Off");
        }

        private void Park()
        {
            DisableGrinders();

            foreach (var piston in pistons)
            {
                piston.Velocity = -PARKING_VELOCITY;
            }
        }

        private static void ApplyToAll(IEnumerable<IMyFunctionalBlock> blocks, string action)
        {
            foreach (var block in blocks)
            {
                block.ApplyAction(action);
            }
        }

        // Arguments (commands)

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
                case "reverse":
                    return Command.Reverse;
                case "reset":
                    return Command.Reset;
                default:
                    return Command.Invalid;
            }
        }

        // State machine

        private enum GrinderPadState
        {
            Stopped,
            Failed,
            Grinding,
            Parking,
        }

        private class InvalidStateTransitionException: Exception {}

        private GrinderPadState State
        {
            get
            {
                return state;
            }
            set
            {
                if (value == state)
                {
                    return;
                }

                var previousState = state;
                state = value;

                Debug("State transition: {0} => {1}", previousState, state);

                try
                {
                    StateChanged(previousState, state);
                }
                catch(InvalidStateTransitionException)
                {
                    Panic("Invalid state transition: {0} => {1}", previousState, state);
                }
            }
        }

        private GrinderPadState state = GrinderPadState.Stopped;
        private int direction = 1;

        private void StateChanged(GrinderPadState previousPadState, GrinderPadState newPadState)
        {
            FindBlocksAgain(previousPadState);
            ControlGrinders();
            ControlPistons(previousPadState);
        }

        private void Panic(string formatString, params object[] args)
        {
            Error(formatString, args);
            State = GrinderPadState.Failed;
        }

        // Block control

        private void FindBlocksAgain(GrinderPadState previousPadState)
        {
            switch (previousPadState)
            {
                case GrinderPadState.Stopped:
                case GrinderPadState.Failed:
                    FindBlocks();
                    break;
            }
        }

        private void ControlGrinders()
        {
            switch (State)
            {
                case GrinderPadState.Stopped:
                case GrinderPadState.Failed:
                    DisableGrinders();
                    break;

                case GrinderPadState.Grinding:
                case GrinderPadState.Parking:
                    EnableGrinders();
                    break;
            }
        }

        private void ControlPistons(GrinderPadState previousState)
        {
            switch (State)
            {
                case GrinderPadState.Stopped:
                case GrinderPadState.Failed:
                    DisablePistons();
                    break;

                case GrinderPadState.Grinding:
                    EnablePistons();
                    SetPistonVelocityToTarget();
                    break;

                case GrinderPadState.Parking:
                    EnablePistons();
                    SetPistonVelocity(-SinglePistonParkingVelocity);
                    break;
            }
        }

        private bool HasReachedTarget()
        {
            var sumTarget = SumTarget;
            var sumPosition = pistons.Sum(piston => piston.CurrentPosition);
            var tolerance = PISTON_TOLERANCE * PistonCountOnOneSide;
            return Math.Abs(sumPosition - sumTarget) < tolerance;
        }

        private void ReverseDirection()
        {
            Debug("Reversing");
            direction *= -1;
            SetPistonVelocityToTarget();
        }

        private void SetPistonVelocityToTarget()
        {
            SetPistonVelocity(direction * GRINDING_VELICITY / PistonCountOnOneSide);
        }

        private float SumTarget
        {
            get
            {
                switch (State)
                {
                    case GrinderPadState.Grinding:
                        return direction > 0 ? sumMaxExtension : sumMinExtension;
                }
                return sumMinExtension;
            }
        }

        private void SetPistonVelocity(float velocity)
        {
            foreach (var piston in pistons)
            {
                piston.Velocity = velocity;
            }
        }

        // Calculated settings

        private int PistonCountOnOneSide => pistons.Count / 2;
        private float SinglePistonGrindingVelocity => GRINDING_VELICITY / PistonCountOnOneSide;
        private float SinglePistonParkingVelocity => PARKING_VELOCITY / PistonCountOnOneSide;

        public Program()
        {
            //Runtime.UpdateFrequency = UpdateFrequency.Update100;
            FindBlocks();
            LoadState(Storage);
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        private void LoadState(string storage)
        {
            var rx = new System.Text.RegularExpressions.Regex(@"^GrinderPad:state=(?<state>\w+);direction=(?<direction>[+\-]?\d+);$");
            var m = rx.Match(storage);
            if (!m.Success) return;

            if (Enum.TryParse(m.Groups["state"].Value, out state))
            {
                int.TryParse(m.Groups["direction"].Value, out direction);
            }
        }

        public void Save()
        {
            Storage = DumpState();
        }

        private string DumpState()
        {
            return string.Format("GrinderPad:state={0};direction={1};", State, direction);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            try
            {
                Debug("Main");

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
                        if (argument != "")
                        {
                            ProcessCommand(argument);
                        }
                        break;
                    case UpdateType.Update1:
                    case UpdateType.Update10:
                    case UpdateType.Update100:
                        break;
                }

                PeriodicProcessing();

                Debug(DumpState());

                if (State != GrinderPadState.Failed)
                {
                    Log("OK");
                }
                else
                {
                    Log("FAILED");
                }
            }
            catch (Exception e)
            {
                Debug(DumpState());
                Error(e.ToString());
            }

            ShowLog();
        }

        private void PeriodicProcessing()
        {
            if (HasReachedTarget())
            {
                ReverseDirection();

                switch (State)
                {
                    case GrinderPadState.Parking:
                        State = GrinderPadState.Stopped;
                        break;
                }
            }
        }

        private void ProcessCommand(string argument)
        {
            var command = ParseCommand(argument);
            switch (command)
            {
                case Command.Start:
                    switch (State)
                    {
                        case GrinderPadState.Stopped:
                        case GrinderPadState.Failed:
                        case GrinderPadState.Grinding:
                        case GrinderPadState.Parking:
                            State = GrinderPadState.Grinding;
                            break;
                        default:
                            Error("Grinder pad is already running");
                            break;
                    }

                    break;

                case Command.Stop:
                    switch (State)
                    {
                        case GrinderPadState.Stopped:
                        case GrinderPadState.Grinding:
                        case GrinderPadState.Parking:
                            State = GrinderPadState.Stopped;
                            break;

                        default:
                            Error("Grinder pad is not running");
                            break;
                    }

                    break;

                case Command.Reverse:
                    switch (State)
                    {
                        case GrinderPadState.Stopped:
                            direction *= -1;
                            break;

                        case GrinderPadState.Grinding:
                        case GrinderPadState.Parking:
                            direction *= -1;
                            SetPistonVelocityToTarget();
                            break;

                        default:
                            Error("Grinder pad is not running");
                            break;
                    }
                    break;

                case Command.Reset:
                    switch (State)
                    {
                        case GrinderPadState.Failed:
                        case GrinderPadState.Grinding:
                            State = GrinderPadState.Parking;
                            break;
                    }
                    break;

                default:
                    Error("Invalid command: " + argument);
                    break;
            }
        }

        #endregion
    }
}