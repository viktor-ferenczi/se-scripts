using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Configuration;
using VRage.Game;
using VRageMath;

namespace KTZHunt3
{
    public partial class Program : MyGridProgram
    {
        static int getCtrlTick = -9000;
        static IMyShipController getCtrlL = null;

        static IMyShipController getCtrl()
        {
            var p = gProgram;
            if (tick - getCtrlTick > 3 * 60)
            {
                foreach (var c in p.controllers)
                {
                    if (c.IsUnderControl)
                    {
                        getCtrlL = c;
                        break;
                    }
                }
                if (getCtrlL == null)
                {
                    foreach (var c in p.controllers)
                    {
                        if (c.IsMainCockpit)
                        {
                            getCtrlL = c;
                            break;
                        }
                    }
                }
                if (getCtrlL == null && p.controllers.Count > 0) getCtrlL = p.controllers[0];
                getCtrlTick = tick;
            }
            return getCtrlL;
        }


        static int getMassT = -1;
        static double getMassL = 0;

        static double getMass()
        {
            if (tick != getMassT)
            {
                getMassT = tick;
                getMassL = getCtrl().CalculateShipMass().PhysicalMass;
            }
            return getMassL;
        }

        static int getPositionT = -1;
        static Vector3D getPositionL = Vector3D.Zero;

        static Vector3D getPosition()
        {
            if (tick != getPositionT)
            {
                getPositionL = getCtrl().GetPosition();
                getPositionT = tick;
            }
            return getPositionL;
        }

        static int getVelocityT = -1;
        static Vector3D getVelocityL = Vector3D.Zero;

        static Vector3D getVelocity()
        {
            if (tick != getVelocityT)
            {
                getVelocityL = getCtrl().GetShipVelocities().LinearVelocity;
                getVelocityT = tick;
            }
            return getVelocityL;
        }

        static int getGravityT = -1;
        static Vector3D getGravityL = Vector3D.Zero;

        static Vector3D getGravity()
        {
            if (tick != getGravityT)
            {
                getGravityL = getCtrl().GetNaturalGravity();
                getGravityT = tick;
            }
            return getGravityL;
        }
    }
}