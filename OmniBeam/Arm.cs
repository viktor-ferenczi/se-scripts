using System;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace OmniBeam
{
    public class Arm
    {
        private const float AngleEpsilon = 1e-3f;
        private const float RotationSpeedMultiplier = 6f;
        private const float MinRotationSpeed = RotationSpeedMultiplier * AngleEpsilon;

        private readonly IMyMotorStator azimuthBase;
        private readonly IMyMotorStator elevationBase;
        private readonly IMyConveyorSorter omniBeam;

        private readonly Random rng = new Random();
        private readonly DebugAPI debug;

        private Subgrid subgrid;
        private Target target;

        public ArmState State { get; private set; } = ArmState.Idle;

        public int? SubgridIndex => subgrid?.Index;
        public double PositionError => target.PositionError;

        public Arm(IMyMotorStator armBase, DebugAPI debug)
        {
            this.debug = debug;

            azimuthBase = armBase;
            elevationBase = Util.FindBlock<IMyMotorStator>(azimuthBase.Top?.CubeGrid);
            omniBeam = Util.FindBlock<IMyConveyorSorter>(elevationBase?.Top?.CubeGrid);

            if (azimuthBase == null ||
                elevationBase == null ||
                omniBeam == null ||
                Util.IsHinge(azimuthBase) ||
                !Util.IsHinge(elevationBase) ||
                !azimuthBase.IsWorking ||
                !elevationBase.IsWorking ||
                !omniBeam.IsWorking)
            {
                State = ArmState.Invalid;
                return;
            }

            StopBaseRotations();
            ActivateBeam(false);
        }

        public bool HasSameTargetAs(Arm other) =>
            this != other &&
            (State == ArmState.Targeting || State == ArmState.Welding) &&
            (other.State == ArmState.Targeting || other.State == ArmState.Welding) &&
            subgrid.Index == other.subgrid.Index &&
            target.Location == other.target.Location;


        public void Target(Subgrid subgridToWeld)
        {
            if (State == ArmState.Invalid)
            {
                return;
            }

            if (State == ArmState.Welding && subgridToWeld == subgrid)
            {
                if (subgrid.TryTargetNearbyBlock(ref target, target.PredictedPosition))
                {
                    State = ArmState.Targeting;
                    ActivateBeam(false);
                    return;
                }
            }

            subgrid = subgridToWeld;

            if (subgrid.TryTargetRandomBlock(ref target, rng))
            {
                State = ArmState.Targeting;
                ActivateBeam(false);
                return;
            }

            Cancel();
        }

        public void Update()
        {
            switch (State)
            {
                case ArmState.Idle:
                case ArmState.Invalid:
                    break;

                case ArmState.Targeting:
                    subgrid.Update();

                    if (target.IsOnTarget)
                    {
                        State = ArmState.Welding;
                        ActivateBeam(true);
                    }

                    TargetAndRotateBases();
                    EnforceTimerAndRetarget();
                    break;

                case ArmState.Welding:
                    subgrid.Update();
                    TargetAndRotateBases();
                    EnforceTimerAndRetarget();
                    break;

                case ArmState.Resetting:
                    TargetAndRotateBases();
                    EnterIdleOnceNotRotating();
                    break;
            }
        }

        private void EnforceTimerAndRetarget()
        {
            if (--target.WeldingTimer == 0)
            {
                Reset();
                return;
            }

            if (!subgrid.IsWeldableBlock(target.Location))
            {
                Target(subgrid);
            }
        }

        public void Cancel()
        {
            switch (State)
            {
                case ArmState.Idle:
                case ArmState.Invalid:
                    break;

                default:
                    subgrid = null;
                    State = ArmState.Idle;
                    ActivateBeam(false);
                    StopBaseRotations();
                    break;
            }
        }

        public void Reset()
        {
            switch (State)
            {
                case ArmState.Invalid:
                case ArmState.Resetting:
                    break;

                case ArmState.Idle:
                case ArmState.Targeting:
                case ArmState.Welding:
                    subgrid = null;
                    target.ResetAngles();
                    State = ArmState.Resetting;
                    ActivateBeam(false);
                    break;
            }
        }

        private void TargetAndRotateBases()
        {
            CalculateTargetAngles();
            CalculateTargetError();

            RotateBase(azimuthBase, (float) target.AzimuthAngle, false);
            RotateBase(elevationBase, (float) target.ElevationAngle, true);
        }

        private void EnterIdleOnceNotRotating()
        {
            var isRotating = Math.Abs(azimuthBase.TargetVelocityRad) > AngleEpsilon || Math.Abs(elevationBase.TargetVelocityRad) > AngleEpsilon;
            if (!isRotating)
            {
                StopBaseRotations();
                State = ArmState.Idle;
            }
        }

        private void CalculateTargetAngles()
        {
            if (State != ArmState.Targeting && State != ArmState.Welding)
            {
                target.AzimuthAngle = 0;
                target.ElevationAngle = 0;
                return;
            }

            var previewGrid = subgrid.PreviewGrid;
            var currentPosition = previewGrid.GridIntegerToWorld(target.Location);

            var motionVector = target.HasPreviousPosition ? currentPosition - target.PreviousPosition : Vector3D.Zero;
            target.PreviousPosition = currentPosition;
            target.HasPreviousPosition = true;

            target.PredictedPosition = currentPosition + motionVector;
            target.Motion = motionVector.Length();

            if (Cfg.PositionRandomization != 0.0)
            {
                var azimuth = rng.NextDouble() * Math.PI * 2.0;
                var elevation = rng.NextDouble() * Math.PI * 2.0;
                Vector3D direction;
                Vector3D.CreateFromAzimuthAndElevation(azimuth, elevation, out direction);
                var radius = rng.NextDouble() * previewGrid.GridSize * 0.8 * Cfg.PositionRandomization;
                target.PredictedPosition += direction * radius;
            }

#if DEBUG
            debug?.DrawMatrix(elevationBase.WorldMatrix, onTop: true);
            debug?.DrawMatrix(azimuthBase.WorldMatrix, onTop: true);

            debug?.DrawPoint(target.PredictedPosition, Color.OrangeRed);
            debug?.DrawLine(omniBeam.WorldMatrix.Translation, target.PredictedPosition, Color.OrangeRed);
#endif

            var azimuthCenter = azimuthBase.WorldMatrix.Translation;
            var elevationCenter = elevationBase.WorldMatrix.Translation;
            var centerDistance = Vector3D.Distance(azimuthCenter, elevationCenter) + azimuthBase.Displacement;

#if DEBUG
            debug?.DrawPoint(elevationCenter, Color.Cyan, onTop: true);
            debug?.DrawLine(elevationCenter, target.PredictedPosition, Color.Cyan, onTop: true);
#endif

            // Azimuth is the angle of the target projected to the floor as seen from the rotor,
            // must also consider all 4 possible hinge placements (block orientations) on the rotor head
            var projected = Vector3D.Transform(target.PredictedPosition, MatrixD.Invert(azimuthBase.WorldMatrix));
            var hingeForwardDirection = elevationBase.Orientation.TransformDirection(Base6Directions.Direction.Forward);
            switch (hingeForwardDirection)
            {
                case Base6Directions.Direction.Forward:
                    target.AzimuthAngle = Math.Atan2(projected.X, -projected.Z);
                    break;
                case Base6Directions.Direction.Backward:
                    target.AzimuthAngle = Math.Atan2(-projected.X, projected.Z);
                    break;
                case Base6Directions.Direction.Left:
                    target.AzimuthAngle = Math.Atan2(-projected.Z, -projected.X);
                    break;
                case Base6Directions.Direction.Right:
                    target.AzimuthAngle = Math.Atan2(projected.Z, projected.X);
                    break;
                default:
                    // Invalid hinge placement (on its side)
                    target.ElevationAngle = 0;
                    return;
            }

            // Elevation is the angle of the "vertical" triangle as seen from the hinge
            var distance = Math.Sqrt(projected.X * projected.X + projected.Z * projected.Z);
            var height = projected.Y - centerDistance;
            target.ElevationAngle = Math.Atan2(distance, height);

            // Util.Log($"{Util.Format(projected)}");
            // Util.Log($"{Util.Format(distance)} {Util.Format(height)}");
            // Util.Log($"{Util.Format(target.AzimuthAngle)} {Util.Format(targetElevationAngle)}");
        }

        private void CalculateTargetError()
        {
            switch (State)
            {
                case ArmState.Targeting:
                    break;

                case ArmState.Welding:
                    return;

                default:
                    target.PositionError = 0;
                    target.IsOnTarget = false;
                    return;
            }

            // Distance of the target position from the laser beam (line)

            var projectedTarget = Vector3D.Transform(target.PredictedPosition, MatrixD.Invert(omniBeam.WorldMatrix));
            projectedTarget.Z = 0;
            target.PositionError = projectedTarget.Length();
            target.IsOnTarget = target.PositionError < 0.5 * subgrid.PreviewGrid.GridSize + target.Motion;
        }

        private static void RotateBase(IMyMotorStator stator, float target, bool isHinge)
        {
            var current = stator.Angle;

            if (isHinge)
            {
                target = Math.Max(-Util.RightAngle, Math.Min(Util.RightAngle, target));
            }
            else
            {
                current = Util.NormalizeAngle(current);
                target = Util.NormalizeAngle(target);
            }

            var delta = target - current;

            // In case of rotors choose the closer direction
            if (!isHinge)
            {
                if (delta > Math.PI)
                {
                    target -= Util.FullCircle;
                }
                if (delta < -Math.PI)
                {
                    target += Util.FullCircle;
                }
                delta = target - current;
            }

            if (delta >= AngleEpsilon)
            {
                stator.TargetVelocityRad = Math.Max(MinRotationSpeed, RotationSpeedMultiplier * delta);
                stator.LowerLimitRad = isHinge ? -Util.RightAngle : float.NegativeInfinity;
                stator.UpperLimitRad = target;
                return;
            }

            if (delta <= -AngleEpsilon)
            {
                stator.TargetVelocityRad = Math.Min(-MinRotationSpeed, RotationSpeedMultiplier * delta);
                stator.LowerLimitRad = target;
                stator.UpperLimitRad = isHinge ? Util.RightAngle : float.PositiveInfinity;
                return;
            }

            StopBase(stator, isHinge);
        }

        private static void StopBase(IMyMotorStator stator, bool isHinge)
        {
            stator.TargetVelocityRad = 0;
            stator.LowerLimitRad = isHinge ? -Util.RightAngle : float.NegativeInfinity;
            stator.UpperLimitRad = isHinge ? Util.RightAngle : float.PositiveInfinity;
        }

        private void StopBaseRotations()
        {
            StopBase(azimuthBase, false);
            StopBase(elevationBase, true);
        }

        private void ActivateBeam(bool activate)
        {
            var actionId = activate
                ? "ToolCore_Shoot_Action_On"
                : "ToolCore_Shoot_Action_Off";

            omniBeam.GetActionWithName(actionId)?.Apply(omniBeam);
        }
    }
}