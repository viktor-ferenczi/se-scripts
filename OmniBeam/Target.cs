using VRageMath;

namespace OmniBeam
{
    public struct Target
    {
        public Vector3I Location;
        public Vector3D PredictedPosition;
        public Vector3D PreviousPosition;
        public bool HasPreviousPosition;
        public double Motion;
        public double AzimuthAngle;
        public double ElevationAngle;
        public double PositionError;
        public bool IsOnTarget;
        public long WeldingTimer;

        public void ResetAngles()
        {
            AzimuthAngle = 0;
            ElevationAngle = 0;
        }

        public void SetLocation(Vector3I location)
        {
            Location = location;
            HasPreviousPosition = false;
            Motion = 0;
            PositionError = double.PositiveInfinity;
            IsOnTarget = false;
            WeldingTimer = Cfg.WeldingDeadline * 6;  // Multiplier because of the Update10 schedule
        }
    }
}