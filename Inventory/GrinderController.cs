using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Inventory;
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

namespace GrinderController
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        private const string GRINDER_GROUP = "Grinders";
        private const string PISTON_GROUP = "Grinder Pistons";

        private const float GRINDING_VELICITY = 2.5f; // m/s
        private const float PISTON_TOLERANCE = 0.01f; // m
        private const float PISTON_PARKING_VELOCITY = 1.0f; // m/s
        private const float PISTON_SKEW_FIXING_VELOCITY = 0.1f; // s

        private const string COMMAND_START = "start";
        private const string COMMAND_STOP = "stop";
        private const string COMMAND_PARK = "park";

        enum State
        {
            Stopped,
            Grinding,
            FixingSkew,
            Parking,
        }

        private State state = State.Stopped;

        private List<IMyShipGrinder> grinders = new List<IMyShipGrinder>();
        private List<IMyPistonBase> pistons = new List<IMyPistonBase>();

        private float direction = 1.0f;
        private string syaye = COMMAND_STOPPED;

        public Program()
        {
            //Runtime.UpdateFrequency = UpdateFrequency.Update100;
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            switch (argument)
            {
                case COMMAND_PARK:
                    Park();
                    break;
                case COMMAND_START:
                    break;
                case COMMAND_STOP:
                    break;
                default:

                    return;
            }

            FindBlocks();

            if (HasBrokenBlock())
            {
                return;
            }

            if (HasPistonSkew())
            {
                FixPistonSkew();
                return;
            }

            if (ArePistonsAtEndPosition())
            {
                ReversePistons();
                return;
            }

            SetPistonVelocity();

            Echo("OK");
        }

        private void SetPistonVelocity()
        {
            var velocity = direction * GRINDING_VELICITY / PistonCountOnOneSide;

            foreach (var piston in pistons)
            {
                piston.Velocity = velocity;
            }
        }

        private bool ArePistonsAtEndPosition()
        {
            var firstPiston = pistons[0];
            var position = pistons[0].CurrentPosition;
            return (position < firstPiston.MinLimit + PISTON_TOLERANCE ||
                    position > firstPiston.MaxLimit - PISTON_TOLERANCE);
        }

        private void ReversePistons()
        {
            direction *= -1f;
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

        private bool HasBrokenBlock()
        {
            if (!IsAllFunctional(grinders))
            {
                Panic("Broken grinder(s) in group " + GRINDER_GROUP);
                return true;
            }

            if (!IsAllFunctional(pistons))
            {
                Panic("Broken pistons(s) in group " + PISTON_GROUP);
                return true;
            }

            return false;
        }

        private void Panic(string message)
        {
            Echo(message);
            Stop();
        }

        private void Stop()
        {
            ApplyToAll(grinders, "OnOff_Off");
            ApplyToAll(pistons, "OnOff_Off");
        }

        private void Park()
        {
            ApplyToAll(grinders, "OnOff_Off");

            foreach (var piston in pistons)
            {
                piston.Velocity = -PISTON_PARKING_VELOCITY;
            }
        }

        private void FindBlocks()
        {
            GridTerminalSystem.GetBlockGroupWithName(GRINDER_GROUP).GetBlocksOfType<IMyShipGrinder>(grinders);
            GridTerminalSystem.GetBlockGroupWithName(PISTON_GROUP).GetBlocksOfType<IMyPistonBase>(pistons);
        }

        private int PistonCountOnOneSide => pistons.Count / 2;

        private static void ApplyToAll(IEnumerable<IMyFunctionalBlock> blocks, string action)
        {
            foreach (var block in blocks)
            {
                block.ApplyAction(action);
            }
        }

        private static bool IsAllFunctional(IEnumerable<IMyFunctionalBlock> blocks)
        {
            return blocks.All(block => block.IsFunctional);
        }

        #endregion
    }
}