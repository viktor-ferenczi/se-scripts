using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;

namespace SpaceEngineersScripts.FabricatorArm
{
    public class Shipyard
    {
        private readonly IMyProjector projector;
        private readonly MultigridProjectorProgrammableBlockAgent mgp;
        private readonly IMyTextPanel lcdDetails;
        private readonly IMyTextPanel lcdStatus;
        private readonly IMyTextPanel lcdTimer;
        private readonly List<FabricatorArm> arms = new List<FabricatorArm>();
        private readonly List<Subgrid> subgrids = new List<Subgrid>();
        private readonly List<Subgrid> weldableSubgrids = new List<Subgrid>();
        private readonly StringBuilder sb = new StringBuilder();
        private readonly Random rng = new Random();
        private readonly DebugAPI debug;
        private int totalTicks;
        private bool loaded;
        private bool retracting = true;
        private int subgridUpdateIndex;

        public Shipyard(IMyGridTerminalSystem gridTerminalSystem, IMyProjector projector, MultigridProjectorProgrammableBlockAgent mgp, IMyTextPanel lcdDetails, IMyTextPanel lcdStatus, IMyTextPanel lcdTimer, DebugAPI debug)
        {
            this.projector = projector;
            this.mgp = mgp;
            this.lcdDetails = lcdDetails;
            this.lcdStatus = lcdStatus;
            this.lcdTimer = lcdTimer;
            this.debug = debug;

            var armBases = new List<IMyMotorStator>();
            gridTerminalSystem.GetBlockGroupWithName(Cfg.WelderArmsGroupName)?.GetBlocksOfType(armBases);
            if (armBases.Count == 0)
            {
                Util.Log("Put the arm bases into the \"Fabricator Arms\" group!");
                return;
            }

            arms.AddRange(armBases.Select(armBase => new FabricatorArm(armBase, debug)));
        }

        public void Update()
        {
            if (!mgp.Available)
                return;

            debug?.RemoveDraw();

            if (projector.IsWorking)
            {
                var subgridCount = mgp.GetSubgridCount(projector.EntityId);
                if (subgridCount == 0)
                {
                    if (loaded)
                    {
                        // Finished welding
                        lcdDetails?.WriteText("Completed");
                        lcdStatus?.WriteText("");
                        totalTicks = 0;
                        subgrids.Clear();
                        loaded = false;

                        foreach (var arm in arms)
                        {
                            arm.Reset();
                        }

                        retracting = true;
                    }
                }
                else
                {
                    if (!loaded)
                    {
                        subgrids.Clear();

                        for (var subgridIndex = 0; subgridIndex < subgridCount; subgridIndex++)
                        {
                            var subgrid = new Subgrid(projector.EntityId, mgp, subgridIndex);
                            subgrid.Update();
                            subgrids.Add(subgrid);
                        }

                        totalTicks = 0;
                        loaded = true;
                    }
                }
            }

            if (loaded)
            {
                subgridUpdateIndex = ++subgridUpdateIndex % subgrids.Count;
                subgrids[subgridUpdateIndex].Update();

                weldableSubgrids.AddRange(subgrids.Where(subgrid => subgrid.HasBuilt && !subgrid.HasFinished));

                // Util.Log($"Weldable SGs: {weldableSubgrids.Count}");
                var weldableSubgridsCount = weldableSubgrids.Count;
                // var i = 0;
                foreach (var arm in arms)
                {
                    if (!arm.IsValid)
                    {
                        Util.Log($"Bad arm: {arm.Name}");
                        continue;
                    }

                    if (!arm.IsWorking)
                    {
                        if (weldableSubgridsCount > 0)
                        {
                            arm.TargetSubgrid(weldableSubgrids[rng.Next(weldableSubgridsCount)]);
                        }
                    }

                    arm.Update();

                    // var targetingState = arm.IsOnTarget ? "ON " : "OFF";
                    // var angleError = Util.Format(arm.AngleError);
                    // var status = arm.IsWorking ? $"SG {arm.Subgrid.Index,2} {targetingState} ERR {angleError}" : "IDLE";
                    // Util.Log($"Arm #{++i}: {status}");
                }
                weldableSubgrids.Clear();

                ShowStatus(lcdStatus);

                var info = projector.DetailedInfo;
                var index = info.IndexOf("Build progress:", StringComparison.InvariantCulture);
                lcdDetails?.WriteText(index >= 0 ? info.Substring(index) : "");

                var seconds = ++totalTicks / 6;
                lcdTimer?.WriteText($"{seconds / 60:00}:{seconds % 60:00}");
            }
            else if (retracting)
            {
                retracting = false;

                foreach (var arm in arms)
                {
                    arm.Update();

                    if (arm.IsMoving)
                    {
                        retracting = true;
                    }
                }

                // Util.Log($"Retracting: {retracting}");
            }
        }

        private void ShowStatus(IMyTextSurface lcdStatus)
        {
            if (lcdStatus == null)
                return;

            sb.Clear();
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
    }
}