using VRageMath;

namespace FabricatorArm
{
    public struct Target
    {
        public Vector3I Location;
        public Vector3D Position;
        public double AzimuthAngle;
        public double ElevationAngle;
        public bool IsOnTarget;

        public void ResetAngles()
        {
            AzimuthAngle = 0;
            ElevationAngle = 0;
        }
    }
}