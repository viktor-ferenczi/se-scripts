using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace OmniBeam
{
    public class Subgrid
    {
        public readonly int Index;
        public IMyCubeGrid PreviewGrid { get; }
        public bool HasBuilt { get; private set; }
        public bool HasFinished { get; private set; }
        public bool IsWeldable => HasBuilt && RemainingBlockCount > 0;
        public int RemainingBlockCount => blockStates.Count;

        private static readonly BoundingBoxI MaxBox = new BoundingBoxI(Vector3I.MinValue, Vector3I.MaxValue);
        private readonly long projectorEntityId;
        private readonly MultigridProjectorProgrammableBlockAgent mgp;
        private readonly Dictionary<Vector3I, BlockState> blockStates = new Dictionary<Vector3I, BlockState>();
        private ulong latestStateHash;

        public Subgrid(long projectorEntityId, MultigridProjectorProgrammableBlockAgent mgp, int index)
        {
            Index = index;
            this.projectorEntityId = projectorEntityId;
            this.mgp = mgp;

            PreviewGrid = mgp.GetPreviewGrid(projectorEntityId, index);
            if (PreviewGrid == null)
            {
                return;
            }

            Update();
        }

        public void Update()
        {
            if (PreviewGrid == null)
            {
                return;
            }

            HasBuilt = mgp.GetBuiltGrid(projectorEntityId, Index) != null;
            if (!HasBuilt)
            {
                latestStateHash = 0;
                return;
            }

            // Optimization: Update the block states only the first time and whenever there is any change
            var stateHash = mgp.GetStateHash(projectorEntityId, Index);
            if (stateHash == latestStateHash && latestStateHash != 0)
            {
                return;
            }
            latestStateHash = stateHash;

            blockStates.Clear();
            mgp.GetBlockStates(blockStates, projectorEntityId, Index, MaxBox, (int) BlockState.Buildable | (int) BlockState.BeingBuilt);

            HasFinished = blockStates.Count == 0;
        }

        public bool IsWeldableBlock(Vector3I location)
        {
            var blockState = mgp.GetBlockState(projectorEntityId, Index, location);
            if (blockState == BlockState.Buildable || blockState == BlockState.BeingBuilt)
            {
                blockStates[location] = blockState;
                return true;
            }

            blockStates.Remove(location);
            return false;
        }

        public bool TryTargetRandomBlock(ref Target target, Random rng)
        {
            if (blockStates.Count == 0)
            {
                return false;
            }

            var i = rng.Next(Math.Min(30, blockStates.Count));
            foreach (var location in blockStates.Keys)
            {
                if (i-- == 0)
                {
                    target.SetLocation(location);
                    return true;
                }
            }

            return false;
        }

        public bool TryTargetNearbyBlock(ref Target target, Vector3D referencePosition)
        {
            if (blockStates.Count == 0)
            {
                return false;
            }

            var okayDistanceSquared = 3 * PreviewGrid.GridSize * PreviewGrid.GridSize;

            var minDistanceSquared = double.PositiveInfinity;
            var minLocation = Vector3I.Zero;
            foreach (var location in blockStates.Keys)
            {
                var position = PreviewGrid.GridIntegerToWorld(location);

                var distanceSquared = Vector3D.DistanceSquared(referencePosition, position);
                if (distanceSquared < minDistanceSquared)
                {
                    minDistanceSquared = distanceSquared;
                    minLocation = location;

                    if (minDistanceSquared < okayDistanceSquared)
                    {
                        break;
                    }
                }
            }

            if (double.IsInfinity(minDistanceSquared))
            {
                return false;
            }

            target.SetLocation(minLocation);
            return true;
        }
    }
}