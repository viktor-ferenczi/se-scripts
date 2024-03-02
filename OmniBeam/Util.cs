using System;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace OmniBeam
{
    public static class Util
    {
        private static readonly StringBuilder LogBuilder = new StringBuilder();

        public static void ClearLog()
        {
            LogBuilder.Clear();
        }

        public static void Log(string message)
        {
            LogBuilder.Append($"{message}\r\n");
        }

        public static void ShowLog(IMyTextPanel lcd)
        {
            lcd?.WriteText(LogBuilder.ToString());
        }

        public static string Format(float v)
        {
            return $"{v:0.000}";
        }

        public static string Format(double v)
        {
            return $"{v:0.000}";
        }

        public static string Format(Vector3I v)
        {
            return $"[{v.X}, {v.Y}, {v.Z}]";
        }

        public static string Format(Vector3D v)
        {
            return $"[{v.X:0.000}, {v.Y:0.000}, {v.Z:0.000}]";
        }

        public static string Format(MatrixD m)
        {
            return $"\r\n  T: {Format(m.Translation)}\r\n  F: {Format(m.Forward)}\r\n  U: {Format(m.Up)}\r\n  S: {Format(m.Scale)}";
        }

        public static T FindBlock<T>(IMyCubeGrid grid) where T : class, IMyCubeBlock
        {
            var min = grid.Min;
            var max = grid.Max;
            for (var x = min.X; x <= max.X; x++)
            {
                for (var y = min.Y; y <= max.Y; y++)
                {
                    for (var z = min.Z; z <= max.Z; z++)
                    {
                        var slimBlock = grid.GetCubeBlock(new Vector3I(x, y, z));
                        var fatBlock = slimBlock?.FatBlock as T;
                        if (fatBlock != null)
                        {
                            return fatBlock;
                        }
                    }
                }
            }
            return null;
        }

        public static bool IsHinge(IMyMotorStator stator) => stator.BlockDefinition.SubtypeName.EndsWith("Hinge");

        public const float RightAngle = (float) (0.5 * Math.PI);
        public const float FullCircle = (float) (2.0 * Math.PI);

        public static float NormalizeAngle(float angle)
        {

            if (angle > Math.PI)
            {
                return angle - FullCircle;
            }

            if (angle < -Math.PI)
            {
                return angle + FullCircle;
            }

            return angle;
        }
    }
}