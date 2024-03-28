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
        public bool HasFinished => HasBuilt && RemainingBlockCount == 0;
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
        }

        public bool IsWeldableBlock(Vector3I location)
        {
            BlockState blockState;
            if (!blockStates.TryGetValue(location, out blockState))
            {
                return false;
            }

            return blockState == BlockState.Buildable || blockState == BlockState.BeingBuilt;
        }

        public bool TryTargetRandomBlock(ref Target target, Random rng)
        {
            if (blockStates.Count == 0)
            {
                return false;
            }

            var i = rng.Next(blockStates.Count);
            foreach (var pair in blockStates)
            {
                if (i-- == 0)
                {
                    target.Location = pair.Key;
                    break;
                }
            }

            return true;
        }

        public bool TryTargetNearbyBlock(ref Target target, Vector3D referencePosition)
        {
            if (blockStates.Count == 0)
            {
                return false;
            }

            var minDistanceSquared = double.PositiveInfinity;
            foreach (var pair in blockStates)
            {
                var position = PreviewGrid.GridIntegerToWorld(pair.Key);

                var distanceSquared = Vector3D.DistanceSquared(referencePosition, position);
                if (distanceSquared < minDistanceSquared)
                {
                    // The random addition helps to untangle the lasers, so they eventually go down different paths.
                    // Without this randomness they meet and converge, all of them welding the same sequence of blocks.
                    minDistanceSquared = distanceSquared;
                    target.Location = pair.Key;
                }
            }

            return !double.IsInfinity(minDistanceSquared);
        }
    }
}