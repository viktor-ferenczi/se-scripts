using System;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace OmniBeam
{
    public class Arm
    {
        private const float AngleEpsilon = 1e-3f;

        private readonly IMyMotorStator azimuthBase;
        private readonly IMyMotorStator elevationBase;
        private readonly IMyConveyorSorter omniBeam;

        private readonly StringBuilder sb = new StringBuilder();
        private readonly Random rng = new Random();
        private readonly DebugAPI debug;

        private Subgrid subgrid;
        private Target target;

        public ArmState State { get; private set; } = ArmState.Idle;

        public int? SubgridIndex => subgrid?.Index;

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
            ActivateOmniBeam(false);
        }

        public string Name => azimuthBase.CustomName;

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

            subgrid = subgridToWeld;
            if (subgrid.TryTargetRandomBlock(ref target, rng))
            {
                State = ArmState.Targeting;
                ActivateOmniBeam(false);
            }
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
                        ActivateOmniBeam(true);
                    }

                    TargetAndRotateBases();
                    break;

                case ArmState.Welding:
                    subgrid.Update();

                    if (!subgrid.IsWeldableBlock(target.Location))
                    {
                        if (!subgrid.TryTargetNearbyBlock(ref target, target.Position))
                        {
                            Cancel();
                            break;
                        }
                    }

                    TargetAndRotateBases();
                    break;

                case ArmState.Resetting:
                    RotateBases();

                    var isMoving = Math.Abs(azimuthBase.TargetVelocityRad) > AngleEpsilon || Math.Abs(elevationBase.TargetVelocityRad) > AngleEpsilon;
                    if (!isMoving)
                    {
                        StopBaseRotations();
                        State = ArmState.Idle;
                    }

                    break;
            }
        }

        public void Cancel()
        {
            switch (State)
            {
                case ArmState.Idle:
                case ArmState.Resetting:
                case ArmState.Invalid:
                    break;

                case ArmState.Targeting:
                case ArmState.Welding:
                    subgrid = null;
                    State = ArmState.Idle;
                    ActivateOmniBeam(false);
                    StopBaseRotations();
                    break;
            }
        }

        public void Reset()
        {
            switch (State)
            {
                case ArmState.Invalid:
                    break;

                case ArmState.Idle:
                case ArmState.Targeting:
                case ArmState.Welding:
                case ArmState.Resetting:
                    subgrid = null;
                    target.ResetAngles();
                    State = ArmState.Resetting;
                    ActivateOmniBeam(false);
                    break;
            }
        }

        private void TargetAndRotateBases()
        {
            CalculateTargetAngles();
            CalculateTargetError();
            RotateBases();
        }

        private void RotateBases()
        {
            RotateBase(azimuthBase, target.AzimuthAngle);
            RotateBase(elevationBase, target.ElevationAngle);
        }

        private void CalculateTargetAngles()
        {
            target.Position = subgrid.PreviewGrid.GridIntegerToWorld(target.Location);

#if DEBUG
            debug?.DrawMatrix(elevationBase.WorldMatrix, onTop: true);
            debug?.DrawMatrix(azimuthBase.WorldMatrix, onTop: true);

            debug?.DrawPoint(target.Position, Color.OrangeRed);
            debug?.DrawLine(omniBeam.WorldMatrix.Translation, target.Position, Color.OrangeRed);
#endif

            var azimuthCenter = azimuthBase.WorldMatrix.Translation;
            var elevationCenter = elevationBase.WorldMatrix.Translation;
            var centerDistance = Vector3D.Distance(azimuthCenter, elevationCenter) + azimuthBase.Displacement;

#if DEBUG
            debug?.DrawPoint(elevationCenter, Color.Cyan, onTop: true);
            debug?.DrawLine(elevationCenter, target.Position, Color.Cyan, onTop: true);
#endif

            // Azimuth is the angle of the target projected to the floor as seen from the rotor,
            // must also consider all 4 possible hinge placements (block orientations) on the rotor head
            var projected = Vector3D.Transform(target.Position, MatrixD.Invert(azimuthBase.WorldMatrix));
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
            // Distance of the target position from the laser beam (line)
            var projectedTarget = Vector3D.Transform(target.Position, MatrixD.Invert(omniBeam.WorldMatrix));

            projectedTarget.Z = 0;
            var positionError = projectedTarget.Length();

            target.IsOnTarget = positionError < subgrid.PreviewGrid.GridSize * Math.Sqrt(3);
            // Util.Log($"{Util.Format(positionError)} {IsOnTarget}");
        }

        private static void RotateBase(IMyMotorStator stator, double target)
        {
            var currentAngle = Util.NormalizeAngle(stator.Angle);

            target = Util.NormalizeAngle(target);
            target = Math.Max(stator.LowerLimitRad, Math.Min(stator.UpperLimitRad, target));
            // Util.Log($"CURR {stator.Angle:0.000} TG {target:0.000}");

            // The rotor base serves as a PID controller by integrating the angular velocity into its current angle over time
            var delta = Util.NormalizeAngle(target - currentAngle);
            stator.TargetVelocityRad = (float) (Math.Abs(delta) >= AngleEpsilon ? delta * Cfg.StatorDeltaMultiplier : 0f);
        }

        private void StopBaseRotations()
        {
            azimuthBase.TargetVelocityRad = 0;
            elevationBase.TargetVelocityRad = 0;
        }

        private void ActivateOmniBeam(bool activate)
        {
            var activated = IsOmniBeamActivate();
            if (activated != activate)
            {
                omniBeam.GetActionWithName("ToolCore_Shoot_Action")?.Apply(omniBeam);
            }
        }

        private bool IsOmniBeamActivate()
        {
            sb.Clear();
            omniBeam.GetActionWithName("ToolCore_Shoot_Action")?.WriteValue(omniBeam, sb);
            return sb.ToString() == "Deactivate";
        }
    }
}