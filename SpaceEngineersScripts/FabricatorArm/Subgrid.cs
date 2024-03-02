using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersScripts.FabricatorArm
{
    public class Subgrid
    {
        public readonly int Index;
        public bool HasBuilt { get; private set; }
        public bool HasFinished { get; private set; }
        public int RemainingBlockCount => blockStates.Count;

        private static readonly BoundingBoxI MaxBox = new BoundingBoxI(Vector3I.MinValue, Vector3I.MaxValue);
        private readonly long projectorEntityId;
        private readonly MultigridProjectorProgrammableBlockAgent mgp;
        private readonly Dictionary<Vector3I, BlockState> blockStates = new Dictionary<Vector3I, BlockState>();
        private readonly Random rng = new Random();
        private ulong latestStateHash;

        public IMyCubeGrid PreviewGrid { get; }

        public Subgrid(long projectorEntityId, MultigridProjectorProgrammableBlockAgent mgp, int index)
        {
            Index = index;
            this.projectorEntityId = projectorEntityId;
            this.mgp = mgp;

            PreviewGrid = mgp.GetPreviewGrid(projectorEntityId, index);
        }

        public void Update()
        {
            HasBuilt = mgp.GetBuiltGrid(projectorEntityId, Index) != null;
            HasFinished = HasBuilt && mgp.IsSubgridComplete(projectorEntityId, Index);

            var stateHash = mgp.GetStateHash(projectorEntityId, Index);
            if (stateHash == latestStateHash)
            {
                return;
            }
            latestStateHash = stateHash;

            blockStates.Clear();
            mgp.GetBlockStates(blockStates, projectorEntityId, Index, MaxBox, (int) BlockState.Buildable | (int) BlockState.BeingBuilt);
        }

        public bool IsWeldable(Vector3I location)
        {
            BlockState blockState;
            if (!blockStates.TryGetValue(location, out blockState))
            {
                return false;
            }

            return blockState == BlockState.Buildable || blockState == BlockState.BeingBuilt;
        }

        public bool TryFindNextBlockToWeld(Vector3D referencePosition, out Vector3I nextLocation, out Vector3D nextPosition)
        {
            nextLocation = Vector3I.Zero;
            nextPosition = Vector3D.Zero;

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

                    nextLocation = pair.Key;
                    nextPosition = position;
                }
            }

            return true;
        }
    }
}