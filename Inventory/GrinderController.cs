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
        private const string GRINDER_PISTON_GROUP = "Grinder Pistons";
        private const string SUSPENSION_PISTON_GROUP = "Welder Wall Pistons";

        private const float GRINDER_VELOCITY = 2.5f; // m/s
        private const float PARKING_VELOCITY = 5.0f; // m/s
        private const float GRINDING_MIN_DISTANCE = 5.0f; // m
        private const float PISTON_TOLERANCE = 0.5f; // m
        private const float SUSPENSION_VELOCITY = 0.2f; // m/s
        private const float SUSPENSION_STEP = 1.25f; // m/run

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

        private void Warning(string formatString, params object[] args)
        {
            Log("W: " + formatString, args);
        }

        private void Error(string formatString, params object[] args)
        {
            Log("E: " + formatString, args);
        }

        // Blocks

        private List<IMyShipGrinder> grinders = new List<IMyShipGrinder>();
        private List<IMyPistonBase> grinderPistons = new List<IMyPistonBase>();
        private List<IMyPistonBase> suspensionPistons = new List<IMyPistonBase>();
        private float sumMinGrinderExtension = 0f;
        private float sumMaxGrinderExtension = 0f;
        private float sumMinSuspensionExtension = 0f;
        private float suspensionTarget = 0f;
        private float suspensionVelocity = 0f;

        private void FindBlocks()
        {
            Debug("FindBlocks");

            GridTerminalSystem.GetBlockGroupWithName(GRINDER_GROUP).GetBlocksOfType<IMyShipGrinder>(grinders);
            GridTerminalSystem.GetBlockGroupWithName(GRINDER_PISTON_GROUP).GetBlocksOfType<IMyPistonBase>(grinderPistons);
            GridTerminalSystem.GetBlockGroupWithName(SUSPENSION_PISTON_GROUP).GetBlocksOfType<IMyPistonBase>(suspensionPistons);

            if (grinders.Count < 1)
            {
                Panic("Grinder group not found: " + GRINDER_GROUP);
                return;
            }

            if (grinderPistons.Count < 1)
            {
                Panic("Piston group not found: " + GRINDER_PISTON_GROUP);
                return;
            }

            if (suspensionPistons.Count < 1)
            {
                Warning("Suspension piston group not found: " + SUSPENSION_PISTON_GROUP);
            }

            if (grinderPistons.Count % 2 != 0)
            {
                Panic("Number of pistons must be even in group: " + GRINDER_PISTON_GROUP);
                return;
            }

            if (!IsAllFunctional(grinders))
            {
                Panic("Broken grinder(s) in group " + GRINDER_GROUP);
                return;
            }

            if (!IsAllFunctional(grinderPistons))
            {
                Panic("Broken pistons(s) in group " + GRINDER_PISTON_GROUP);
                return;
            }

            sumMinGrinderExtension = grinderPistons.Sum(piston => piston.MinLimit);
            sumMaxGrinderExtension = grinderPistons.Sum(piston => piston.MaxLimit);

            sumMinSuspensionExtension = suspensionPistons.Count > 0 ? suspensionPistons.Sum(piston => piston.MinLimit) : 0f;
            suspensionTarget = GetSuspensionPosition();

            Debug("FindBlocks OK");
        }

        private float GetSuspensionPosition()
        {
            return suspensionPistons.Count > 0 ? suspensionPistons.Sum(piston => piston.CurrentPosition) : 0f;
        }

        private static bool IsAllFunctional(IEnumerable<IMyFunctionalBlock> blocks)
        {
            return blocks.All(block => block.IsFunctional);
        }

        private void EnableGrinders()
        {
            ApplyToAll(grinders, "OnOff_On");
        }

        private void DisableGrinders()
        {
            ApplyToAll(grinders, "OnOff_Off");
        }

        private void EnableGrinderPistons()
        {
            ApplyToAll(grinderPistons, "OnOff_On");
        }

        private void DisableGrinderPistons()
        {
            ApplyToAll(grinderPistons, "OnOff_Off");
        }

        private void EnableSuspensionPistons()
        {
            ApplyToAll(suspensionPistons, "OnOff_On");
        }

        private void DisableSuspensionPistons()
        {
            ApplyToAll(suspensionPistons, "OnOff_Off");
        }

        private void Park()
        {
            DisableGrinders();

            foreach (var piston in grinderPistons)
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
            ControlGrinderPistons(previousPadState);
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

        private void ControlGrinderPistons(GrinderPadState previousState)
        {
            switch (State)
            {
                case GrinderPadState.Stopped:
                case GrinderPadState.Failed:
                    DisableGrinderPistons();
                    DisableSuspensionPistons();
                    break;

                case GrinderPadState.Grinding:
                    EnableGrinderPistons();
                    SetPistonVelocityToTarget();
                    EnableSuspensionPistons();
                    break;

                case GrinderPadState.Parking:
                    DisableSuspensionPistons();
                    EnableGrinderPistons();
                    SetPistonVelocity(-SinglePistonParkingVelocity);
                    break;
            }
        }

        private bool GrindersHaveReachedTarget()
        {
            var sumTarget = SumTarget;
            var sumPosition = grinderPistons.Sum(piston => piston.CurrentPosition);
            var tolerance = PISTON_TOLERANCE * PistonCountOnOneSide;
            if (direction > 0)
            {
                return sumPosition > sumTarget - tolerance;
            }

            return sumPosition < sumTarget + tolerance;
        }

        private void ReverseGrinderDirection()
        {
            Debug("Reversing");
            direction *= -1;
            SetPistonVelocityToTarget();
        }

        private void SetPistonVelocityToTarget()
        {
            SetPistonVelocity(direction * GRINDER_VELOCITY / PistonCountOnOneSide);
        }

        private float SumTarget
        {
            get
            {
                switch (State)
                {
                    case GrinderPadState.Grinding:
                        return direction > 0 ? sumMaxGrinderExtension : GRINDING_MIN_DISTANCE * 2;
                }
                return sumMinGrinderExtension;
            }
        }

        private void SetPistonVelocity(float velocity)
        {
            foreach (var piston in grinderPistons)
            {
                piston.Velocity = velocity;
            }
        }

        private void SetSuspensionPistonVelocity(float velocity)
        {
            suspensionVelocity = velocity;
            foreach (var piston in suspensionPistons)
            {
                piston.Velocity = velocity;
            }
        }

        private bool HasSuspensionReachedBottom()
        {
            return GetSuspensionPosition() < sumMinSuspensionExtension + PISTON_TOLERANCE;
        }

        private void LowerSuspension()
        {
            if (suspensionPistons.Count == 0)
            {
                return;
            }

            suspensionTarget -= SUSPENSION_STEP;
            SetSuspensionPistonVelocity(-SUSPENSION_VELOCITY / suspensionPistons.Count);
        }

        private void StopSuspensionAtTarget()
        {
            if (suspensionVelocity != 0f)
            {
                if (GetSuspensionPosition() < suspensionTarget + PISTON_TOLERANCE * suspensionPistons.Count)
                {
                    SetSuspensionPistonVelocity(0f);
                }
            }
        }

        // Calculated settings

        private int PistonCountOnOneSide => grinderPistons.Count / 2;
        private float SinglePistonGrindingVelocity => GRINDER_VELOCITY / PistonCountOnOneSide;
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

//            Debug("suspensionPistons.Count = {0}", suspensionPistons.Count);
//            Debug("suspensionTarget = {0}", suspensionTarget);
//            Debug("suspensionVelocity = {0}", suspensionVelocity);

            ShowLog();
        }

        private void PeriodicProcessing()
        {
            if (GrindersHaveReachedTarget())
            {
                ReverseGrinderDirection();

                switch (State)
                {
                    case GrinderPadState.Grinding:
                        if (HasSuspensionReachedBottom())
                        {
                            State = GrinderPadState.Parking;
                        }
                        else
                        {
                            LowerSuspension();
                        }
                        break;

                    case GrinderPadState.Parking:
                        State = GrinderPadState.Stopped;
                        break;
                }
            }

            StopSuspensionAtTarget();
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
                            SetPistonVelocityToTarget();
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
                    FindBlocks();
                    switch (State)
                    {
                        case GrinderPadState.Stopped:
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