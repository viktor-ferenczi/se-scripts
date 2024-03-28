using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace WelderTurretController
{
    partial class Program : MyGridProgram
    {
        public static string v2ss(Vector3D v)
        {
            return "<" + v.X.ToString("0.0000") + "," + v.Y.ToString("0.0000") + "," + v.Z.ToString("0.0000") + ">";
        }

		public static double GetAllowedRotationAngle(double desiredDelta, IMyMotorStator rotor)
		{
			double desiredAngle = rotor.Angle + desiredDelta;
			var max = MathHelper.TwoPi;
			if ((desiredAngle < rotor.LowerLimitRad && desiredAngle + max < rotor.UpperLimitRad)
				|| (desiredAngle > rotor.UpperLimitRad && desiredAngle - max > rotor.LowerLimitRad))
			{
				return -Math.Sign(desiredDelta) * (max - Math.Abs(desiredDelta));
			}
			return desiredDelta;
		}
		public static double AimRotorAtPosition(IMyMotorStator rotor, Vector3D desiredDirection, Vector3D currentDirection, float rotationScale = 1f, float timeStep = 1f / 6f, bool t = false)
		{
			Vector3D desiredDirectionFlat = VectorMath.Rejection(desiredDirection, rotor.WorldMatrix.Up);
			Vector3D currentDirectionFlat = VectorMath.Rejection(currentDirection, rotor.WorldMatrix.Up);
			double angle = VectorMath.AngleBetween(desiredDirectionFlat, currentDirectionFlat);
			if (t) return angle;
			//var r = angle;
			Vector3D axis = Vector3D.Cross(desiredDirection, currentDirection);
			angle *= Math.Sign(Vector3D.Dot(axis, rotor.WorldMatrix.Up));
			angle = GetAllowedRotationAngle(angle, rotor);
			rotor.TargetVelocityRad = rotationScale * (float)angle / timeStep;
			return 0;
		}

		public static class VectorMath
		{
			/// <summary>
			/// Normalizes a vector only if it is non-zero and non-unit
			/// </summary>
			public static Vector3D SafeNormalize(Vector3D a)
			{
				if (Vector3D.IsZero(a))
					return Vector3D.Zero;

				if (Vector3D.IsUnit(ref a))
					return a;

				return Vector3D.Normalize(a);
			}

			/// <summary>
			/// Reflects vector a over vector b with an optional rejection factor
			/// </summary>
			public static Vector3D Reflection(Vector3D a, Vector3D b, double rejectionFactor = 1)
			{
				Vector3D proj = Projection(a, b);
				Vector3D rej = a - proj;
				return proj - rej * rejectionFactor;
			}

			/// <summary>
			/// Rejects vector a on vector b
			/// </summary>
			public static Vector3D Rejection(Vector3D a, Vector3D b)
			{
				if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
					return Vector3D.Zero;

				return a - a.Dot(b) / b.LengthSquared() * b;
			}

			/// <summary>
			/// Projects vector a onto vector b
			/// </summary>
			public static Vector3D Projection(Vector3D a, Vector3D b)
			{
				if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
					return Vector3D.Zero;

				if (Vector3D.IsUnit(ref b))
					return a.Dot(b) * b;

				return a.Dot(b) / b.LengthSquared() * b;
			}

			/// <summary>
			/// Scalar projection of a onto b
			/// </summary>
			public static double ScalarProjection(Vector3D a, Vector3D b)
			{
				if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
					return 0;

				if (Vector3D.IsUnit(ref b))
					return a.Dot(b);

				return a.Dot(b) / b.Length();
			}

			/// <summary>
			/// Computes angle between 2 vectors in radians.
			/// </summary>
			public static double AngleBetween(Vector3D a, Vector3D b)
			{
				if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
					return 0;
				else
					return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
			}

			/// <summary>
			/// Computes cosine of the angle between 2 vectors.
			/// </summary>
			public static double CosBetween(Vector3D a, Vector3D b)
			{
				if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
					return 0;
				else
					return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
			}

			/// <summary>
			/// Returns if the normalized dot product between two vectors is greater than the tolerance.
			/// This is helpful for determining if two vectors are "more parallel" than the tolerance.
			/// </summary>
			public static bool IsDotProductWithinTolerance(Vector3D a, Vector3D b, double tolerance)
			{
				double dot = Vector3D.Dot(a, b);
				double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance);
				return Math.Abs(dot) * dot > num;
			}
		}

	}
}
