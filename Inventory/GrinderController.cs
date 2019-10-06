using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Inventory;
using Sandbox.Game;
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
        private const float PISTON_TOLERANCE = 0.01f; // m
        private const float PISTON_PARKING_VELOCITY = 1.0f; // m/s
        private const float PISTON_SKEW_FIXING_VELOCITY = 0.1f; // s

        // Debugging

        private bool DEBUG = true;

        private readonly StringBuilder log = new StringBuilder();

        private void Log(string formatString, params object[] args)
        {
            if (DEBUG)
            {
                log.AppendFormat(formatString, args);
            }
        }

        private void ShowLog()
        {
            if (DEBUG)
            {
                Echo(log.ToString());
                log.Clear();
            }
        }

        private void Error(string formatString, params object[] args)
        {
            DEBUG = true;
            Log("ERROR: " + formatString, args);
        }


        // Blocks

        private readonly List<IMyShipGrinder> grinders = new List<IMyShipGrinder>();
        private readonly List<IMyPistonBase> pistons = new List<IMyPistonBase>();

        private void FindBlocks()
        {
            GridTerminalSystem.GetBlockGroupWithName(GRINDER_GROUP).GetBlocksOfType<IMyShipGrinder>(grinders);
            GridTerminalSystem.GetBlockGroupWithName(PISTON_GROUP).GetBlocksOfType<IMyPistonBase>(pistons);

            if (grinders.Count == 0)
            {
                Panic("Grinder group not found: " + GRINDER_GROUP);
                return;
            }

            if (pistons.Count == 0)
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
                piston.Velocity = -PISTON_PARKING_VELOCITY;
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
                    Echo("Invalid argument: " + argument);
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
            Fixing,
        }

        private class InvalidStateTransitionException: Exception {}

        private GrinderPadState State
        {
            get => state;
            set
            {
                if (value == state)
                {
                    return;
                }

                var previousState = state;
                state = value;

                Log("State transition: {0} => {1}", previousState, state);

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

        private const int Forward = 1;
        private const int Backward = -1;
        private int direction = Forward;

        private void StateChanged(GrinderPadState previousPadState, GrinderPadState newPadState)
        {
            ValidateStateChange(previousPadState);
            FindBlocksBeforeUsingThem(previousPadState);
            ControlGrinders();
            ControlPistons();
        }

        private void ValidateStateChange(GrinderPadState previousPadState)
        {
            switch (previousPadState)
            {
                case GrinderPadState.Stopped:
                    switch (State)
                    {
                        case GrinderPadState.Failed:
                        case GrinderPadState.Grinding:
                        case GrinderPadState.Parking:
                            break;
                        default:
                            throw new InvalidStateTransitionException();
                    }

                    break;
                case GrinderPadState.Failed:
                    switch (State)
                    {
                        case GrinderPadState.Parking:
                            break;
                        default:
                            throw new InvalidStateTransitionException();
                    }

                    break;
                case GrinderPadState.Grinding:
                case GrinderPadState.Fixing:
                    switch (State)
                    {
                        case GrinderPadState.Stopped:
                        case GrinderPadState.Failed:
                        case GrinderPadState.Parking:
                        case GrinderPadState.Fixing:
                            break;
                        default:
                            throw new InvalidStateTransitionException();
                    }

                    break;
                case GrinderPadState.Parking:
                    switch (State)
                    {
                        case GrinderPadState.Stopped:
                        case GrinderPadState.Failed:
                            break;
                        default:
                            throw new InvalidStateTransitionException();
                    }

                    break;
                default:
                    throw new InvalidStateTransitionException();
            }
        }

        private void Panic(string formatString, params object[] args)
        {
            Error(formatString, args);
            State = GrinderPadState.Failed;
        }

        // Block control

        private void FindBlocksBeforeUsingThem(GrinderPadState previousPadState)
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
                case GrinderPadState.Fixing:
                    EnableGrinders();
                    break;
            }
        }

        private void ControlPistons()
        {
            switch (State)
            {
                case GrinderPadState.Stopped:
                case GrinderPadState.Failed:
                    DisablePistons();
                    break;

                case GrinderPadState.Grinding:
                    EnablePistons();
                    SetPistonVelocity(direction * SinglePistonGrindingVelocity);
                    break;

                case GrinderPadState.Parking:
                    EnablePistons();
                    SetPistonVelocity(-SinglePistonParkingVelocity);
                    break;

                case GrinderPadState.Fixing:
                    EnablePistons();
                    SetPistonVelocity(0f);
                    break;
            }
        }

        private void ReversePistonsAtTheEnd()
        {
            var piston = pistons[0];
            var position = piston.CurrentPosition;

            if (direction > 0)
            {
                if (position > piston.MaxLimit - PISTON_TOLERANCE)
                {
                    direction = Backward;
                }
            } else {
                if (position < piston.MinLimit + PISTON_TOLERANCE)
                {
                    direction = Forward;
                }
            }
        }

        private void StopWhenParked()
        {
            if(pistons.All(piston => piston.CurrentPosition < PISTON_TOLERANCE))
            {
                State = GrinderPadState.Stopped;
                direction = Forward;
            }
        }

        private void SetPistonVelocity(float velocity)
        {
            foreach (var piston in pistons)
            {
                piston.Velocity = velocity;
            }
        }

        private bool HasPistonSkew()
        {
            var target = pistons[0].CurrentPosition;
            return pistons.Any(piston => Math.Abs(piston.CurrentPosition - target) > PISTON_TOLERANCE);
        }

        private void FixPistonSkew()
        {
            const double tolerance = 0.5 * PISTON_TOLERANCE;

            var target = pistons[0].CurrentPosition;

            foreach (var piston in pistons)
            {
                var position = piston.CurrentPosition;
                if (position < target - tolerance)
                {
                    piston.Velocity = PISTON_SKEW_FIXING_VELOCITY;
                } else if (position > target + tolerance)
                {
                    piston.Velocity = -PISTON_SKEW_FIXING_VELOCITY;
                }
                else
                {
                    piston.Velocity = (target - position) / 10.0f;
                }
            }
        }

        // Calculated settings

        private int PistonCountOnOneSide => pistons.Count / 2;
        private float SinglePistonGrindingVelocity => GRINDING_VELICITY / PistonCountOnOneSide;
        private float SinglePistonParkingVelocity => PISTON_PARKING_VELOCITY / PistonCountOnOneSide;

        public Program()
        {
            //Runtime.UpdateFrequency = UpdateFrequency.Update100;
            Runtime.UpdateFrequency = UpdateFrequency.None;
            Load(Storage);
        }

        private void Load(string storage)
        {
            var rx = new Regex(@"^GrinderPad:state=(?<state>\w+);direction=(?<direction>[+\-]?\d+);$");
            var m = rx.Match(storage);
            if (!m.Success) return;

            if (Enum.TryParse(m.Groups["state"].Value, out state))
            {
                int.TryParse(m.Groups["direction"].Value, out direction);
            }
        }

        public void Save()
        {
            Storage = string.Format("GrinderPad:state={0};direction={1};", State, direction);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Log("Main");

            switch (State)
            {
                case GrinderPadState.Grinding:
                    ReversePistonsAtTheEnd();
                    break;

                case GrinderPadState.Parking:
                    StopWhenParked();
                    break;

                case GrinderPadState.Fixing:
                    FixPistonSkew();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (HasPistonSkew())
            {
                State = GrinderPadState.Fixing;
            } else {}

            Echo("OK");
        }

        #endregion
    }
}