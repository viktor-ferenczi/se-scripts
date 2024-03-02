using System;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersScripts.FabricatorArm
{
    public class FabricatorArm
    {
        private const float AngleEpsilon = 1e-5f;

        private readonly IMyMotorStator azimuthBase;
        private readonly IMyMotorStator elevationBase;
        private readonly IMyConveyorSorter fabricator;
        private readonly DebugAPI debug;
        private readonly StringBuilder sb = new StringBuilder();
        private readonly Random rng = new Random();

        private double targetAzimuthAngle;
        private double targetElevationAngle;

        public Vector3I TargetLocation { get; private set; }
        public Vector3D TargetPosition { get; private set; }
        public Subgrid Subgrid { get; set; }
        public bool IsOnTarget { get; set; }
        public bool IsValid { get; }

        public FabricatorArm(IMyMotorStator armBase, DebugAPI debug)
        {
            this.debug = debug;

            azimuthBase = armBase;
            elevationBase = Util.FindBlock<IMyMotorStator>(azimuthBase.Top?.CubeGrid);
            fabricator = Util.FindBlock<IMyConveyorSorter>(elevationBase?.Top?.CubeGrid);

            if (azimuthBase == null ||
                elevationBase == null ||
                fabricator == null ||
                Util.IsHinge(azimuthBase) ||
                !Util.IsHinge(elevationBase))
            {
                return;
            }

            IsValid = true;
            fabricator.Enabled = true;
            ActivateFabricator(false);
        }

        public string Name => azimuthBase.CustomName;
        public bool IsWorking => IsValid && Subgrid != null && Subgrid.HasBuilt && !Subgrid.HasFinished;
        public bool IsMoving => IsValid && (Math.Abs(azimuthBase.TargetVelocityRad) > AngleEpsilon || Math.Abs(elevationBase.TargetVelocityRad) > AngleEpsilon);

        public void TargetSubgrid(Subgrid target)
        {
            Subgrid = target;
            IsOnTarget = false;
            TargetPosition = target != null ? Util.GetRandomPoint(rng, target.PreviewGrid.WorldAABB) : Vector3D.Zero;
            // Util.Log(Util.Format(TargetPosition));
        }

        public void Reset()
        {
            TargetSubgrid(null);
            targetAzimuthAngle = 0;
            targetElevationAngle = 0;
        }

        public void Update()
        {
            if (!IsValid)
            {
                return;
            }

            if (IsWorking)
            {
                Subgrid.Update();

                if (!Subgrid.IsWeldable(TargetLocation))
                {
                    Vector3I nextLocation;
                    Vector3D nextPosition;
                    if (Subgrid.TryFindNextBlockToWeld(TargetPosition, out nextLocation, out nextPosition))
                    {
                        TargetLocation = nextLocation;
                        TargetPosition = nextPosition;
                    }
                    else
                    {
                        TargetSubgrid(null);
                    }
                }
            }

            Rotate();
            VerifyTargeting();
            ActivateFabricator(IsOnTarget);
        }

        private void ActivateFabricator(bool activate)
        {
            var activated = IsFabricatorActivated();
            if (activated != activate)
            {
                fabricator.GetActionWithName("ToolCore_Shoot_Action")?.Apply(fabricator);
            }
        }

        private bool IsFabricatorActivated()
        {
            sb.Clear();
            fabricator.GetActionWithName("ToolCore_Shoot_Action")?.WriteValue(fabricator, sb);
            return sb.ToString() == "Deactivate";
        }

        private void Rotate()
        {
            if (IsWorking)
            {
                CalculateTargetAngles();
            }

            Rotate(azimuthBase, targetAzimuthAngle);
            Rotate(elevationBase, targetElevationAngle);
        }

        private void VerifyTargeting()
        {
            if (!IsWorking)
            {
                IsOnTarget = false;
                return;
            }

            // It is on target is the distance of a laser beam from the target point is less
            // than the diameter of the target grid's block
            var projectedTarget = Vector3D.Transform(TargetPosition, MatrixD.Invert(fabricator.WorldMatrix));
            projectedTarget.Z = 0;
            var positionError = projectedTarget.Length();
            IsOnTarget = positionError < Subgrid.PreviewGrid.GridSize * Math.Sqrt(3);
        }

        private void CalculateTargetAngles()
        {
            debug?.DrawMatrix(elevationBase.WorldMatrix, onTop: true);
            debug?.DrawMatrix(azimuthBase.WorldMatrix, onTop: true);

            debug?.DrawPoint(TargetPosition, Color.OrangeRed);
            debug?.DrawLine(fabricator.WorldMatrix.Translation, TargetPosition, Color.OrangeRed);

            var azimuthCenter = azimuthBase.WorldMatrix.Translation;
            var elevationCenter = elevationBase.WorldMatrix.Translation;
            var centerDistance = Vector3D.Distance(azimuthCenter, elevationCenter) + azimuthBase.Displacement;

            debug?.DrawPoint(elevationCenter, Color.Cyan, onTop: true);
            debug?.DrawLine(elevationCenter, TargetPosition, Color.Cyan, onTop: true);

            // Azimuth is the angle of the target projected to the floor as seen from the rotor,
            // must also consider all 4 possible hinge placements (block orientations) on the rotor head
            var projected = Vector3D.Transform(TargetPosition, MatrixD.Invert(azimuthBase.WorldMatrix));
            var hingeForwardDirection = elevationBase.Orientation.TransformDirection(Base6Directions.Direction.Forward);
            switch (hingeForwardDirection)
            {
                case Base6Directions.Direction.Forward:
                    targetAzimuthAngle = Math.Atan2(projected.X, -projected.Z);
                    break;
                case Base6Directions.Direction.Backward:
                    targetAzimuthAngle = Math.Atan2(-projected.X, projected.Z);
                    break;
                case Base6Directions.Direction.Left:
                    targetAzimuthAngle = Math.Atan2(-projected.Z, -projected.X);
                    break;
                case Base6Directions.Direction.Right:
                    targetAzimuthAngle = Math.Atan2(projected.Z, projected.X);
                    break;
                default:
                    // Invalid hinge placement (on its side)
                    targetElevationAngle = 0;
                    return;
            }

            // Elevation is the angle of the "vertical" triangle as seen from the hinge
            var distance = Math.Sqrt(projected.X * projected.X + projected.Z * projected.Z);
            var height = projected.Y - centerDistance;
            targetElevationAngle = Math.Atan2(distance, height);

            // Util.Log($"{Util.Format(projected)}");
            // Util.Log($"{Util.Format(distance)} {Util.Format(height)}");
            // Util.Log($"{Util.Format(targetAzimuthAngle)} {Util.Format(targetElevationAngle)}");
        }

        private static void Rotate(IMyMotorStator stator, double target)
        {
            var currentAngle = NormalizeAngle(stator.Angle);
            target = NormalizeAngle(target);
            target = Math.Max(stator.LowerLimitRad, Math.Min(stator.UpperLimitRad, target));
            // Util.Log($"CURR {stator.Angle:0.000} TG {target:0.000}");
            var delta = NormalizeAngle(target - currentAngle);
            stator.TargetVelocityRad = (float) (Math.Abs(delta) >= AngleEpsilon ? delta * 5.0f : 0f);
        }

        private static double NormalizeAngle(double angle)
        {
            if (angle > Math.PI)
            {
                angle -= Math.PI * 2;
            }

            if (angle < -Math.PI)
            {
                angle += Math.PI * 2;
            }

            return angle;
        }
    }
}