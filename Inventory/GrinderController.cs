using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Inventory;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
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

        private const float GRINDING_VELICITY = 2.5f; // m/s
        private const float PARKING_VELOCITY = 2.5f; // m/s
        private const float PISTON_TOLERANCE = 0.05f; // m

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

        private enum GrinderDirection
        {
            Forward,
            Backward,
        }

        private GrinderPadState state = GrinderPadState.Stopped;
        private GrinderDirection direction = GrinderDirection.Forward;

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
                    SetPistonVelocityForGrinding();
                    break;

                case GrinderPadState.Parking:
                    EnablePistons();
                    SetPistonVelocity(-SinglePistonParkingVelocity);
                    break;
            }
        }

        private void ReversePistonsAtTheEnd()
        {
            var sumPositions = pistons.Sum(piston => piston.CurrentPosition);
            var targetSumPosition = 2 * End;
            var delta = sumPositions - targetSumPosition;
            Debug("sumPos {0}", sumPositions);
            Debug("End {0}", End);
            Debug("Delta {0}", delta);
            if (Math.Abs(delta) < PISTON_TOLERANCE)
            {
                ReverseDirection();
            }
        }

        private void ReverseDirection()
        {
            switch (direction)
            {
                case GrinderDirection.Forward:
                    direction = GrinderDirection.Backward;
                    break;
                case GrinderDirection.Backward:
                    direction = GrinderDirection.Forward;
                    break;
            }
        }

        private float End
        {
            get
            {
                switch (direction)
                {
                    case GrinderDirection.Forward:
                        return pistons.Sum(piston => piston.MaxLimit) / PistonCountOnOneSide;
                        break;
                    case GrinderDirection.Backward:
                        return pistons.Sum(piston => piston.MinLimit) / PistonCountOnOneSide;
                        break;
                }

                return 0f;
            }
        }

        private void StopWhenParked()
        {
            if(pistons.All(piston => piston.CurrentPosition < PISTON_TOLERANCE))
            {
                State = GrinderPadState.Stopped;
                direction = GrinderDirection.Forward;
            }
        }

        private void SetPistonVelocity(float velocity)
        {
            foreach (var piston in pistons)
            {
                piston.Velocity = velocity;
            }
        }

        private void SetPistonVelocityForGrinding()
        {
            var target = End / PistonCountOnOneSide;
            var totalDelta = pistons.Sum(piston => Math.Abs(target - piston.CurrentPosition));
            if (totalDelta < PISTON_TOLERANCE)
            {
                SetPistonVelocity(0f);
                return;
            }

            var skew = 0f;
            var averageDelta = totalDelta / PistonCountOnOneSide;
            foreach (var piston in pistons)
            {
                var delta = target - piston.CurrentPosition;
                skew += Math.Abs(delta - averageDelta);
                piston.Velocity = delta / totalDelta * GRINDING_VELICITY;
            }

            Debug("Piston skew {0}", skew);
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
                Enum.TryParse(m.Groups["direction"].Value, out direction);
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
            switch (State)
            {
                case GrinderPadState.Grinding:
                    ReversePistonsAtTheEnd();
                    break;

                case GrinderPadState.Parking:
                    StopWhenParked();
                    break;
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
                        case GrinderPadState.Grinding:
                            ReverseDirection();
                            break;

                        case GrinderPadState.Parking:
                            State = GrinderPadState.Stopped;
                            break;

                        default:
                            Error("Grinder pad is not grinding");
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