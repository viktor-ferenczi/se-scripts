using Sandbox.Engine.Utils;
using Sandbox.Game.Screens.Helpers;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRageMath;
using VRageRender.Voxels;

namespace KTZHunt3
{
    partial class Program : MyGridProgram
    {
        List<MyDetectedEntityInfo> WCobstructions = new List<MyDetectedEntityInfo>();
        Dictionary<MyDetectedEntityInfo, float> WCthreats = new Dictionary<MyDetectedEntityInfo, float>();
        MyDetectedEntityInfo focus = new MyDetectedEntityInfo();
        long lfocus = -1;
        int focusChangeTick = -1;

        class DetectedEntity
        {
            public int updTick;

            public long EntityId;
            public string Name = "";
            public MyDetectedEntityType Type;
            public BoundingBoxD BBox;
            public MatrixD Orientation;
            public Vector3D Position;
            public Vector3D Velocity;
            public MyRelationsBetweenPlayerAndBlock Rel = MyRelationsBetweenPlayerAndBlock.Neutral;
            public float threat;

            public MyDetectedEntityInfo focus;
            public double ldistSqr;
            public double distSqr;

            public bool isPMW = false;


            public DetectedEntity upd(MyDetectedEntityInfo e)
            {
                if (e.IsEmpty()) return this;
                updTick = tick;
                EntityId = e.EntityId;
                if (e.Name.Length > 0) Name = e.Name;
                Type = e.Type;
                Orientation = e.Orientation;
                Position = e.Position;
                Velocity = e.Velocity;
                BBox = e.BoundingBox;
                Rel = e.Relationship;
                if ((e.Type == MyDetectedEntityType.CharacterHuman || e.Type == MyDetectedEntityType.CharacterOther) && Name.Length == 0)
                {
                    Name = "Suit"; // + e.EntityId;
                }
                if (e.Type == MyDetectedEntityType.Unknown)
                {
                    //unknown means obstruction list generally
                    if (e.Name.StartsWith("MyVoxelMap"))
                    {
                        Type = MyDetectedEntityType.Asteroid;
                        Name = "Asteroid";
                        Rel = MyRelationsBetweenPlayerAndBlock.Neutral;
                    }
                    else if (e.Name.Length == 0)
                    {
                        var he = BBox.Max - BBox.Min;
                        //grids this small don't actually show up in obstruction list, only suits.
                        if (he.X < 3 && he.Y < 3 && he.Z < 3)
                        {
                            Type = MyDetectedEntityType.CharacterHuman;
                            Rel = MyRelationsBetweenPlayerAndBlock.Friends;
                            Name = "Suit";
                        }
                    }
                    else Rel = MyRelationsBetweenPlayerAndBlock.Neutral;
                }
                else if (e.Type == MyDetectedEntityType.Asteroid) Name = "Asteroid";
                else if (e.Type == MyDetectedEntityType.Planet) Name = "Planet";
                if (e.Type == MyDetectedEntityType.LargeGrid)
                {
                    try
                    {
                        focus = gProgram.APIWC.GetAiFocus(EntityId).GetValueOrDefault();
                    }
                    catch (Exception) { }
                }
                return this;
            }

            const double rdivisor = 1.0d / 60.0d;

            public DetectedEntity upd(MyDetectedEntityInfo e, float t)
            {
                upd(e);
                threat = t;
                if (Type == MyDetectedEntityType.SmallGrid)
                {
                    if (e.Name.StartsWith("Small Grid")) isPMW = true;
                    else
                    {
                        var he = BBox.Max - BBox.Min;
                        if (he.X < 10 && he.Y < 10 && he.Z < 10) isPMW = true;
                    }
                }
                return this;
            }

            public Vector3D getEstPos()
            {
                if (updTick == tick) return Position;
                return Position + (Velocity * (tick - updTick) * rdivisor);
            }
        }

        Dictionary<long, DetectedEntity> detectedEntitiesD = new Dictionary<long, DetectedEntity>();
        List<DetectedEntity> detectedEntitiesL = new List<DetectedEntity>();

        void addDE(DetectedEntity e)
        {
            detectedEntitiesD[e.EntityId] = e;
            detectedEntitiesL.Add(e);
        }

        void remDE(DetectedEntity e)
        {
            detectedEntitiesD.Remove(e.EntityId);
            detectedEntitiesL.Remove(e);
        }

        int stale_threshold = 20;

        string getColFromRel(MyRelationsBetweenPlayerAndBlock rel)
        {
            if (rel == MyRelationsBetweenPlayerAndBlock.Enemies) return ""; // Config.REC.Val;
            else if (rel == MyRelationsBetweenPlayerAndBlock.Owner) return "blue"; //never happens?
            else if (rel == MyRelationsBetweenPlayerAndBlock.Friends || rel == MyRelationsBetweenPlayerAndBlock.FactionShare) return ""; // Config.RFC.Val;
            else if (rel == MyRelationsBetweenPlayerAndBlock.Neutral) return ""; // Config.RNC.Val;
            else return ""; // Config.ROC.Val;
        }

        bool matchSpeed = false;
        DetectedEntity matchTarget = null;
        DetectedEntity rotateTarget = null;
        bool autoRotate = false;

        string lastRadarStr = "";

        int lastUpdTick = 0;
        static Profiler radarP = new Profiler("radar");
        IEnumerator<bool> __processRadar = null;

        public void processRadar(double maxMS)
        {
            radarP.s();
            if (tick - lastUpdTick > 60)
            {
                __processRadar = _processRadar();
                lastUpdTick = tick;
            }
            if (__processRadar != null)
            {
                DateTime s = DateTime.Now;
                double r = 0;
                bool fin = false;
                do
                {
                    fin = !__processRadar.MoveNext();
                    r = (DateTime.Now - s).TotalMilliseconds;
                } while (!fin && r < maxMS);
                if (fin)
                {
                    __processRadar.Dispose();
                    __processRadar = null;
                }
            }
            radarP.e();
        }

        public IEnumerator<bool> _processRadar()
        {
            var my_id = Me.CubeGrid.EntityId;

            #region DATA UPDATE

            {
                var tickstart = tick;
                yield return true;
                var my_pos = Me.GetPosition();
                focus = APIWC.GetAiFocus(my_id, 0).GetValueOrDefault();
                yield return true;
                if (focus.EntityId != lfocus)
                {
                    lfocus = focus.EntityId;
                    focusChangeTick = tick;
                }
                WCobstructions.Clear();
                APIWC.GetObstructions(Me, WCobstructions);
                yield return true;

                WCthreats.Clear();
                APIWC.GetSortedThreats(WCthreats);
                yield return true;
                foreach (var o in WCobstructions)
                {
                    if (!o.IsEmpty())
                    {
                        DetectedEntity de = null;
                        detectedEntitiesD.TryGetValue(o.EntityId, out de);
                        if (de != null) de.upd(o);
                        else addDE(new DetectedEntity().upd(o));
                        yield return true;
                    }
                }
                foreach (var kvp in WCthreats)
                {
                    if (!kvp.Key.IsEmpty())
                    {
                        DetectedEntity de = null;
                        detectedEntitiesD.TryGetValue(kvp.Key.EntityId, out de);
                        if (de != null) de.upd(kvp.Key).threat = kvp.Value;
                        else
                        {
                            var n = new DetectedEntity();
                            n.upd(kvp.Key).threat = kvp.Value;
                            addDE(n);
                        }
                        yield return true;
                    }
                }
                List<DetectedEntity> del = new List<DetectedEntity>();
                foreach (var e in detectedEntitiesL)
                {
                    if (tickstart - e.updTick > stale_threshold) del.Add(e);
                    else
                    {
                        e.ldistSqr = e.distSqr;
                        e.distSqr = (my_pos - e.Position).LengthSquared();
                    }
                    yield return true;
                }
                foreach (var e in del)
                {
                    remDE(e);
                    yield return true;
                }
            }

            #endregion

            #region RENDER

            StringBuilder b = new StringBuilder();
            {
                //matcher

                var trg = APIWC.GetAiFocus(my_id).GetValueOrDefault();
                yield return true;
                if (matchSpeed)
                {
                    b.Append("<color=white>!<color=green>SPEEDMATCHING");
                    if (trg.IsEmpty() || trg.Name != matchTarget.Name)
                    {
                        bapp(b, ":<color=", getColFromRel(matchTarget.Rel), ">", matchTarget.Name);
                    }
                    else b.Append(" ON");
                    b.Append("\n");
                }
                else b.Append("\n");
                yield return true;

                if (autoRotate)
                {
                    b.Append("<color=white>!<color=lightblue>AUTOROTATING");
                    if (trg.IsEmpty() || trg.Name != rotateTarget.Name)
                    {
                        bapp(b, ":<color=", getColFromRel(rotateTarget.Rel), ">", rotateTarget.Name);
                    }
                    else b.Append(" ON");
                    b.Append("\n");
                }
                else b.Append("\n");
                yield return true;

                if (matchSpeed || !trg.IsEmpty())
                {
                    Vector3D tp = Vector3D.Zero;
                    Vector3D tv = Vector3D.Zero;
                    if (!matchSpeed || (matchSpeed && trg.Name == matchTarget.Name))
                    {
                        tp = trg.Position;
                        tv = trg.Velocity;
                    }
                    else if (matchSpeed && matchTarget != null)
                    {
                        tp = matchTarget.getEstPos();
                        tv = matchTarget.Velocity;
                    }
                    yield return true;
                    if (tp != Vector3D.Zero)
                    {
                        var cpat = cpa_time(getPosition(), getVelocity(), tp, tv);
                        b.Append("<color=lightgray>CPA:"); //, rotateTarget.Name);
                        if (cpat < 0) b.Append("moving away");
                        else
                        {
                            var mf = getPosition() + (getVelocity() * cpat);
                            var tf = tp + (tv * cpat);
                            var d = Vector3D.Distance(mf, tf);
                            bapp(b, dist2str(d), " in ", cpat.ToString("0.0"), "s");
                        }
                        b.Append("\n");
                        yield return true;
                    }
                }
                b.Append("\n");
                if (!trg.IsEmpty())
                {
                    double d = (trg.Position - getPosition()).Length();
                    bapp(b, "<color=lightgray>Target: <color=red>", trg.Name, " (", dist2str(d), ")\n");
                }
                else b.Append("<color=lightgray>Target: none\n");
                yield return true;
            }
            {
                //railguns
                if (railGroup.Count > 0)
                {
                    foreach (var blk in railGroup)
                    {
                        var rdy = APIWC.IsWeaponReadyToFire(blk);
                        if (rdy) bapp(b, "     <color=lightgreen>", blk.CustomName);
                        else
                        {
                            var ws = getWS(blk);
                            if (ws != null && ws.settings != null)
                            {
                                var timeleft = (1.0 - ws.chargeProgress) * ws.settings.chargeTicks / 60;
                                if (ws.lastDrawFactor != 0) timeleft /= ws.lastDrawFactor;
                                var chrgt = timeleft.ToString("0.0");

                                if (chrgt.Length < 3) b.Append(" ");
                                bapp(b, "<color=orange>", chrgt, "s ", blk.CustomName);
                            }
                        }
                        var t = APIWC.GetWeaponTarget(blk).GetValueOrDefault();
                        if (t.Type == MyDetectedEntityType.LargeGrid || t.Type == MyDetectedEntityType.SmallGrid) bapp(b, " ► ", t.Name);
                        else b.Append(" ► <color=lightgray>No target");
                        b.Append("\n");
                        yield return true;
                    }
                }
            }
            {
                //movers
                int PMWs = 0;
                foreach (var e in detectedEntitiesL)
                {
                    if (e.isPMW) PMWs++;
                }
                var plo = APIWC.GetProjectilesLockedOn(my_id);
                var plocked = plo.Item2;
                if (plocked > 0)
                {
                    bapp(b, "<color=white>!<color=red>INBOUND TORPS:<color=white>", plocked, "\n");
                }
                if (PMWs > 0)
                {
                    bapp(b, "<color=white>!<color=red>Probable PMWs:<color=white>", PMWs, "\n");
                }
                b.Append("\n");
                yield return true;
            }
            {
                //detected entities
                for (int i = 0; i < detectedEntitiesL.Count; i++)
                {
                    var e = detectedEntitiesL[i];

                    bapp(b, "<color=", getColFromRel(e.Rel), ">");

                    bapp(b, e.Name, " (", dist2str(Math.Sqrt(e.distSqr)), ")");
                    string thrt;
                    if (e.threat < 0.0001) thrt = "0";
                    else if (e.threat > 0.1) thrt = e.threat.ToString("0.0");
                    else if (e.threat > 0.01) thrt = e.threat.ToString("0.00");
                    else thrt = "<0.01";

                    if (e.Rel == MyRelationsBetweenPlayerAndBlock.Enemies) b.Append(" t:" + thrt);

                    bapp(b, " v:", dist2str(e.Velocity.Length()), "/s");
                    yield return true;
                    if (!e.focus.IsEmpty())
                    {
                        b.Append("\n └target:");
                        if (e.focus.Relationship == MyRelationsBetweenPlayerAndBlock.Friends) b.Append("<color=lightgreen>");
                        else b.Append("<color=lightgray>");
                        b.Append(e.focus.Name);
                    }
                    b.Append("\n");
                    yield return true;
                }
            }
            var str = b.ToString();
            if (str != lastRadarStr)
            {
                lastRadarStr = str;
                if (statusLogSprite == null) statusLogSprite = new SpriteHUDLCD(statusLog);
                statusLogSprite.s = statusLog;
                statusLogSprite.setLCD(str);
            }

            #endregion

            yield return false;
        }

        SpriteHUDLCD statusLogSprite = null;





        public static string dist2str(double d)
        {
            if (d > 1000)
            {
                return (d / 1000).ToString("0.0") + "km";
            }
            else return d.ToString("0") + "m";
        }


        public static double cpa_time(Vector3D Tr1_p, Vector3D Tr1_v, Vector3D Tr2_p, Vector3D Tr2_v)
        {
            Vector3D dv = Tr1_v - Tr2_v;

            double dv2 = Vector3D.Dot(dv, dv);
            if (dv2 < 0.00000001) // the  tracks are almost parallel
                return 0.0; // any time is ok.  Use time 0.

            Vector3D w0 = Tr1_p - Tr2_p;
            double cpatime = -Vector3D.Dot(w0, dv) / dv2;

            return cpatime; // time of CPA
        }
    }
}