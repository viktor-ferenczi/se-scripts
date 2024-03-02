using System;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersScripts.FabricatorArm
{
    public class FabricatorArm
    {
        private const float AngleEpsilon = 1e-5f;
        private const double AngleErrorLimit = Math.PI * 10 / 180;

        private readonly IMyMotorStator azimuthBase;
        private readonly IMyMotorStator elevationBase;
        private readonly IMyConveyorSorter fabricator;
        private readonly DebugAPI debug;
        private readonly StringBuilder sb = new StringBuilder();

        private Subgrid subgrid;
        private Vector3I targetLocation;
        private Vector3D targetPosition;

        private double targetAzimuthAngle;
        private double targetElevationAngle;
        private double angleError = -1;
        private bool isOnTarget;

        public FabricatorArm(IMyMotorStator armBase, DebugAPI debug)
        {
            this.debug = debug;

            azimuthBase = armBase;
            elevationBase = Util.FindBlock<IMyMotorStator>(azimuthBase.Top?.CubeGrid);
            fabricator = Util.FindBlock<IMyConveyorSorter>(elevationBase?.Top?.CubeGrid);

            fabricator.Enabled = true;
            ActivateFabricator(false);
        }

        public string Name => azimuthBase.CustomName;
        public Subgrid Subgrid => subgrid;
        public bool IsValid => azimuthBase != null && elevationBase != null && fabricator != null && !Util.IsHinge(azimuthBase) && Util.IsHinge(elevationBase);
        public bool IsWorking => subgrid != null && subgrid.HasBuilt && !subgrid.HasFinished;
        public bool IsOnTarget => isOnTarget;
        public double AngleError => angleError;
        public bool IsMoving => IsValid && (Math.Abs(azimuthBase.TargetVelocityRad) > AngleEpsilon || Math.Abs(elevationBase.TargetVelocityRad) > AngleEpsilon);

        public void TargetSubgrid(Subgrid target)
        {
            subgrid = target;
            targetPosition = fabricator.WorldMatrix.Translation;
        }

        public void Reset()
        {
            TargetSubgrid(null);
            angleError = -1;
            isOnTarget = false;
        }

        public void Update()
        {
            if (!IsValid)
            {
                return;
            }

            if (IsWorking)
            {
                subgrid.Update();

                if (!subgrid.IsWeldable(targetLocation))
                {
                    if (!subgrid.TryFindNearestBlockToWeld(fabricator.WorldMatrix.Translation, ref targetLocation, ref targetPosition))
                    {
                        Reset();
                    }
                }
            }

            Rotate();

            if (IsWorking)
            {
                VerifyTargeting();
            }
            else
            {
                isOnTarget = false;
            }

            ActivateFabricator(isOnTarget);
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
            else
            {
                targetAzimuthAngle = 0;
                targetElevationAngle = 0;
            }

            Rotate(azimuthBase, targetAzimuthAngle);
            Rotate(elevationBase, targetElevationAngle);
        }

        private void VerifyTargeting()
        {
            if (!IsWorking)
            {
                angleError = -1;
                isOnTarget = false;
                return;
            }

            var azimuthError = NormalizeAngle(targetAzimuthAngle - azimuthBase.Angle);
            var elevationError = NormalizeAngle(targetElevationAngle - elevationBase.Angle);
            angleError = Math.Sqrt(azimuthError * azimuthError + elevationError * elevationError);
            isOnTarget = angleError < AngleErrorLimit;
        }


        private void CalculateTargetAngles()
        {
            debug?.DrawMatrix(elevationBase.WorldMatrix, onTop: true);
            debug?.DrawMatrix(azimuthBase.WorldMatrix, onTop: true);

            debug?.DrawPoint(targetPosition, Color.OrangeRed);
            debug?.DrawLine(fabricator.WorldMatrix.Translation, targetPosition, Color.OrangeRed);

            var azimuthCenter = azimuthBase.WorldMatrix.Translation;
            var elevationCenter = elevationBase.WorldMatrix.Translation;
            var centerDistance = Vector3D.Distance(azimuthCenter, elevationCenter) + azimuthBase.Displacement;

            debug?.DrawPoint(elevationCenter, Color.Cyan, onTop: true);
            debug?.DrawLine(elevationCenter, targetPosition, Color.Cyan, onTop: true);

            // Azimuth is the angle of the target projected to the floor as seen from the rotor,
            // must also consider all 4 possible hinge placements (block orientations) on the rotor head
            var projected = Vector3D.Transform(targetPosition, MatrixD.Invert(azimuthBase.WorldMatrix));
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