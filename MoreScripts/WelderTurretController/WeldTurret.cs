using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRageMath;

namespace WelderTurretController
{
    partial class Program : MyGridProgram
    {
        static Random rnd = new Random();
        class WeldTurret
        {
            public WeldTurret(IMyMotorAdvancedStator rotor, IMyMotorAdvancedStator hinge)
            {
                ID = rnd.Next();
                this.rotor = rotor;
                this.hinge = hinge;
                remote = false;
            }
            public WeldTurret(int ID)
            {
                this.ID = ID;
                this.ping = gProgram.tick;
                remote = true;
            }
            public Vector3D target = Vector3D.Zero;
            public WeldTarget wtarget = null;
            public void setTarget(WeldTarget w)
            {
                wtarget = w;
                setTarget(w != null ? w.wpos : Vector3D.Zero);
            }
            public void setTarget(Vector3D pos)
            {
                if (remote)
                {
                    if (target != pos)
                    {
                        target = pos;
                        MyTuple<int, int, Vector3D> pkt =
                        new MyTuple<int, int, Vector3D>((int)MSG.SETTARGET, ID, pos);
                        gProgram.IGC.SendBroadcastMessage(Network.tag, pkt);
                    }
                }
                else
                {
                    target = pos;
                }
            }
            public void update()
            {
                if (remote) return;

                if (target != Vector3D.Zero)
                {
                    rotate(target);
                }
                else set(false);
            }
            public bool remote = true;

            public int ID = -1;
            public int ping = -1;
            public IMyMotorAdvancedStator hinge = null;
            public IMyMotorAdvancedStator rotor = null;
            public List<IMyFunctionalBlock> tools = new List<IMyFunctionalBlock>();

            double lalign = 999;
            double rrr = 0.0174533;

            void rotate(Vector3D target)
            {
                Vector3D heading = (target - hinge.Top.GetPosition()).Normalized();


                var a = AimRotorAtPosition(rotor, heading, hinge.WorldMatrix.Forward, 1, 1f / 6f, true);
                var b = AimRotorAtPosition(rotor, heading, hinge.WorldMatrix.Backward, 1, 1f / 6f, true);
                if (a < b) AimRotorAtPosition(rotor, heading, hinge.WorldMatrix.Forward);
                else AimRotorAtPosition(rotor, heading, hinge.WorldMatrix.Backward);

                AimRotorAtPosition(hinge, heading, hinge.Top.WorldMatrix.Left);

                var align = Vector3D.Angle(heading, hinge.Top.WorldMatrix.Left);
                set(align < rrr * 2);
                lalign = align;
            }
            bool lon = false;
            void set(bool on)
            {
                if (on == lon) return;
                lon = on;
                foreach (var b in tools)
                {
                    if (on) b.Enabled = on;
                    try
                    {
                        var a = b.GetActionWithName("ToolCore_Shoot_Action");
                        StringBuilder ab = new StringBuilder();
                        a.WriteValue(b, ab);
                        var cs = ab.ToString();
                        if (on && cs == "Activate") a.Apply(b);
                        else if (!on && cs == "Deactivate") a.Apply(b);
                    }
                    catch (Exception) { }
                }
            }
        }
        static int stale = 60 * 5;
        void culldead()
        {
            for (int i = 0; i < turrets.Count;)
            {
                var e = turrets[i];
                if (e.remote && tick - e.ping > stale + 60) turrets.RemoveAt(i);
                else i++;
            }
        }
        void sendpings()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                var e = turrets[i];
                if (!e.remote && tick - e.ping > stale)
                {
                    e.ping = tick;
                    MyTuple<int, int, Vector3D> pkt =
                            new MyTuple<int, int, Vector3D>((int)MSG.TURRETPING, e.ID, Vector3D.Zero);
                    gProgram.IGC.SendBroadcastMessage(Network.tag, pkt);
                }
            }
        }
        void recping(int ID)
        {
            foreach (var t in turrets)
            {
                if (t.ID == ID)
                {
                    t.ping = tick;
                    return;
                }
            }
            turrets.Add(new WeldTurret(ID));
        }
    }
}
