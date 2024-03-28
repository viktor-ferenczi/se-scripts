using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;

namespace OmniBeam
{
    public class Shipyard
    {
        private readonly IMyGridTerminalSystem gridTerminalSystem;
        private readonly IMyProjector projector;
        private readonly MultigridProjectorProgrammableBlockAgent mgp;
        private readonly IMyTextPanel lcdDetails;
        private readonly IMyTextPanel lcdStatus;
        private readonly IMyTextPanel lcdTimer;
        private readonly List<Arm> arms = new List<Arm>();
        private readonly List<Subgrid> subgrids = new List<Subgrid>();
        private readonly List<Subgrid> weldableSubgrids = new List<Subgrid>();
        private readonly StringBuilder sb = new StringBuilder();
        private readonly Random rng = new Random();
        private readonly DebugAPI debug;
        private int ticks;

        public ShipyardState State { get; private set; }

        public Shipyard(IMyGridTerminalSystem gridTerminalSystem, IMyProjector projector, MultigridProjectorProgrammableBlockAgent mgp, IMyTextPanel lcdDetails, IMyTextPanel lcdStatus, IMyTextPanel lcdTimer, DebugAPI debug)
        {
            this.gridTerminalSystem = gridTerminalSystem;
            this.projector = projector;
            this.mgp = mgp;
            this.lcdDetails = lcdDetails;
            this.lcdStatus = lcdStatus;
            this.lcdTimer = lcdTimer;
            this.debug = debug;

            FindArms();
            ResetArms();

            State = ShipyardState.Stopping;
        }

        private void FindArms()
        {
            var statorsInGroup = new List<IMyMotorStator>();
            gridTerminalSystem.GetBlockGroupWithName(Cfg.WelderArmsGroupName)?.GetBlocksOfType(statorsInGroup);

            var armBases = statorsInGroup.Where(stator => !Util.IsHinge(stator)).ToList();
            if (armBases.Count == 0)
            {
                Util.Log("Put the arm bases into the \"OmniBeam Arms\" group!");
                return;
            }

            arms.Clear();
            arms.AddRange(armBases.Select(armBase => new Arm(armBase, debug)));
        }

        private void ResetArms()
        {
            foreach (var arm in arms)
            {
                arm.Reset();
            }
        }

        public void Start()
        {
            if (State == ShipyardState.Welding)
            {
                return;
            }

            if (arms.Count == 0)
            {
                Util.Log("No working arms");
                return;
            }

            if (!mgp.Available)
            {
                Util.Log("MGP is not available");
                return;
            }

            if (!projector.IsWorking)
            {
                Util.Log("Projector is not working");
                return;
            }

            var subgridCount = mgp.GetSubgridCount(projector.EntityId);
            if (subgridCount == 0)
            {
                Util.Log("No projected subgrids");
                return;
            }

            for (var subgridIndex = 0; subgridIndex < subgridCount; subgridIndex++)
            {
                subgrids.Add(new Subgrid(projector.EntityId, mgp, subgridIndex));
            }

            ticks = 0;
            State = ShipyardState.Welding;
        }

        public void Stop()
        {
            if (State != ShipyardState.Welding)
            {
                return;
            }

            ResetArms();
            subgrids.Clear();

            lcdDetails?.WriteText("");
            lcdStatus?.WriteText("");

            State = ShipyardState.Stopping;
        }

        public void Update()
        {
            switch (State)
            {
                case ShipyardState.Idle:
                    break;

                case ShipyardState.Welding:
                    if (!mgp.Available ||
                        !projector.IsWorking ||
                        mgp.GetSubgridCount(projector.EntityId) != subgrids.Count ||
                        subgrids.All(subgrid => subgrid.HasFinished))
                    {
                        Stop();
                        break;
                    }

                    Util.Log($"Arms: {arms.Count}");
                    Util.Log($"Subgrids: {subgrids.Count}");

                    subgrids[ticks % subgrids.Count].Update();

                    DivertArms();
                    TargetUpdateArms();

                    ShowStatus();
                    ShowDetails();
                    ShowTimer();

                    break;

                case ShipyardState.Stopping:
                    UpdateArms();
                    if (arms.All(arm => arm.State == ArmState.Idle))
                    {
                        State = ShipyardState.Idle;
                    }
                    break;
            }
        }

        private void DivertArms()
        {
            if (arms.Count < 2)
            {
                return;
            }

            foreach (var arm in arms)
            {
                var other = rng.Next(arms.Count);
                if (arm.HasSameTargetAs(arms[other]))
                {
                    arm.Cancel();
                }
            }
        }

        private void TargetUpdateArms()
        {
            weldableSubgrids.Clear();
            weldableSubgrids.AddRange(subgrids.Where(subgrid => subgrid.IsWeldable));
            Util.Log($"Weldable: {weldableSubgrids.Count}");

            foreach (var arm in arms)
            {
                if (arm.State == ArmState.Idle && weldableSubgrids.Count != 0)
                {
                    arm.Target(weldableSubgrids[rng.Next(weldableSubgrids.Count)]);
                }

                arm.Update();
            }
        }

        private void UpdateArms()
        {
            foreach (var arm in arms)
            {
                arm.Update();
            }
        }

        private void ShowStatus()
        {
            if (lcdStatus == null)
                return;

            sb.Clear();

            sb.Append("Arm Sub State\r\n");
            sb.Append("--- --- -----\r\n");
            int index = 0;
            foreach (var arm in arms)
            {
                var subgridIndex = arm.SubgridIndex == null ? "-" : arm.SubgridIndex.ToString();
                sb.Append($"{index++,3} {subgridIndex,3} {arm.State.ToString(),-9}\r\n");
            }
            sb.Append("\r\n");

            sb.Append("Sub Blocks\r\n");
            sb.Append("--- ------\r\n");
            foreach (var subgrid in subgrids)
            {
                if (!subgrid.HasBuilt)
                    continue;

                var remainingBlockCount = subgrid.RemainingBlockCount;
                if (remainingBlockCount == 0)
                    continue;

                sb.Append($"{subgrid.Index,3} {remainingBlockCount,6}\r\n");
            }

            lcdStatus.WriteText(sb.ToString());
        }

        private void ShowDetails()
        {
            if (lcdDetails == null)
            {
                return;
            }

            var info = projector.DetailedInfo;
            var index = info.IndexOf("Build progress:", StringComparison.InvariantCulture);
            lcdDetails.WriteText(index >= 0 ? info.Substring(index) : "");
        }

        private void ShowTimer()
        {
            if (lcdTimer == null)
            {
                return;
            }

            var seconds = ++ticks / 6;
            lcdTimer.WriteText($"{seconds / 60:00}:{seconds % 60:00}");
        }
    }
}