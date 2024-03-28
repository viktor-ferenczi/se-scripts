using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;
using Sandbox.ModAPI.Ingame;
using System.Security.Cryptography;

namespace WelderTurretController
{
    partial class Program : MyGridProgram
    {
        static BoundingBoxI MaxBox = new BoundingBoxI(Vector3I.MinValue, Vector3I.MaxValue);
        static Dictionary<Vector3I, BlockState> BlockStates = new Dictionary<Vector3I, BlockState>();

        class WeldTarget// : IComparable
        {
            public long gid = 0;
            public Vector3I gpos;
            public Vector3D wpos;
            public int priority = 0;
            public WeldTarget(long gid, Vector3I gpos, Vector3D wpos, int priority)
            {
                this.gid = gid;
                this.gpos = gpos;
                this.wpos = wpos;
                this.priority = priority;
            }
            /*
			public int CompareTo(object obj)
			{
				var x = this;
				var y = (WeldTarget)obj;
				
			}*/
        }
        class WeldTargetComp
        {
            Program p = null;
            Dictionary<IMyProjector, long> pscans = new Dictionary<IMyProjector, long>();
            int lfsc = 0;

            List<IMyTerminalBlock> partialBlocks = new List<IMyTerminalBlock>();
            List<Vector3D> beingBuilt = new List<Vector3D>();
            List<Vector3D> buildable = new List<Vector3D>();
            List<Vector3D> mismatch = new List<Vector3D>();


            public List<WeldTarget> weldTargets = new List<WeldTarget>();

            WeldTarget get_t(WeldTarget m)
            {
                if (m == null) return null;
                return get_t(m.gid, m.gpos);
            }
            WeldTarget get_t(long gid, Vector3I gpos)
            {
                foreach (var w in weldTargets)
                {
                    if (w.gid == gid && w.gpos == gpos) return w;
                }
                return null;
            }
            void add_t(long gid, Vector3I gpos, Vector3D wpos, int priority)
            {
                /*WeldTarget e = null;
				foreach(var w in weldTargets)
				{
					if(w.gid == gid && w.gpos == gpos)
					{
						e = w;break;
					}
				}
				if (e == null)*/
                weldTargets.Add(new WeldTarget(gid, gpos, wpos, priority));
                /*else
				{
					e.wpos = wpos;
					e.priority = priority;
				}*/

            }
            //todo: grinder mode these?

            public int vsort(Vector3D x, Vector3D y)
            {
                var xr = x.X.CompareTo(y.X);
                if (xr != 0) return xr;
                xr = x.Y.CompareTo(y.Y);
                if (xr != 0) return xr;
                return x.Z.CompareTo(y.Z);
            }


            public WeldTargetComp(Program p)
            {
                this.p = p;
                partialBlocks.Clear();
                p.GridTerminalSystem.GetBlocks(partialBlocks);
            }
            public void upd()
            {
                bool changed = false;
                for (var i = 0; i < partialBlocks.Count;)
                {
                    var b = partialBlocks[i];
                    var sb = b.CubeGrid.GetCubeBlock(b.Position);
                    if (sb.IsFullIntegrity)
                    {
                        partialBlocks.RemoveAt(i);
                        changed = true;
                    }
                    else i++;
                }
                foreach (var p in p.projectors)
                {
                    long scan = -1;
                    pscans.TryGetValue(p, out scan);
                    var ns = mgp.GetScanNumber(p.EntityId);
                    if (ns != scan)
                    {
                        changed = true;
                        pscans[p] = ns;
                    }
                }

                if (changed || (p.tick - lfsc) > 60 * 5)
                {
                    lfsc = p.tick;
                    //beingBuilt.Clear();
                    //buildable.Clear();
                    mismatch.Clear();
                    weldTargets.Clear();
                    foreach (var p in p.projectors)
                    {
                        var c = mgp.GetSubgridCount(p.EntityId);
                        for (var i = 0; i < c; i++)
                        {
                            var pg = mgp.GetPreviewGrid(p.EntityId, i);
                            BlockStates.Clear();
                            mgp.GetBlockStates(BlockStates, p.EntityId, i, MaxBox,
                            (int)BlockState.Buildable | (int)BlockState.BeingBuilt | (int)BlockState.Mismatch);

                            foreach (var kvp in BlockStates)
                            {
                                Vector3D np = pg.GridIntegerToWorld(kvp.Key);
                                int bm = (int)kvp.Value;
                                if ((bm & (int)BlockState.Mismatch) != 0)
                                {
                                    mismatch.Add(np);
                                }
                                else if ((bm & (int)BlockState.BeingBuilt) != 0)
                                {
                                    add_t(pg.EntityId, kvp.Key, np, 1);
                                    //beingBuilt.Add(np);
                                }
                                else if ((bm & (int)BlockState.Buildable) != 0)
                                {
                                    add_t(pg.EntityId, kvp.Key, np, 2);
                                    //buildable.Add(np);
                                }
                            }
                        }
                    }
                    foreach (var e in partialBlocks)
                    {
                        add_t(p.Me.CubeGrid.EntityId, e.Position, e.GetPosition(), 0);
                    }
                }
            }

            public void updateWeldAssignments()
            {
                /*
				 * generate lists of targets that are not currently 'owned' by a turret
				 *
				 iterate weldturrets. if weldturrets are targeting a block that still exists, skip.
				 if a turret is no longer targeting a validd block

				 target whatever block is closest to last target?
				 alternatively, target whatever bloc is furthest from the other weld targets...
				 */
                //var freeturret = false;
                List<WeldTarget> occupied = new List<WeldTarget>();
                List<WeldTurret> redir = new List<WeldTurret>();
                foreach (var t in p.turrets)
                {
                    var e = get_t(t.wtarget);
                    if (e == null) redir.Add(t);
                    else
                    {
                        occupied.Add(e);
                    }
                }
                if (redir.Count > 0)
                {
                    Vector3D aggregate = Vector3D.Zero;
                    int c = 0;
                    foreach (var e in occupied)
                    {
                        aggregate += e.wpos;
                        c++;
                    }
                    aggregate /= c;
                    sds = aggregate;

                    weldTargets.Sort(statdistsort);

                    foreach (var tur in redir)
                    {
                        WeldTarget t = null;
                        foreach (var e in weldTargets)
                        {
                            if (occupied.Contains(e)) continue;
                            t = e;
                            break;
                        }
                        if (t == null)
                        {
                            if (weldTargets.Count > 0) t = weldTargets[0];
                        }
                        tur.setTarget(t);
                        if (t != null && !occupied.Contains(t)) occupied.Add(t);
                    }
                }
            }
            static public bool prefType = true;
            public Vector3D sds = new Vector3D();
            public int statdistsort(WeldTarget x, WeldTarget y)
            {
                if (x.priority != y.priority && prefType) return x.priority.CompareTo(y.priority);
                var a = (sds - x.wpos).LengthSquared();
                var b = (sds - y.wpos).LengthSquared();
                return b.CompareTo(a);
            }

            /*public Vector3D weldtarget()
			{
				if (partialBlocks.Count > 0) return partialBlocks[0].GetPosition();
				else if (beingBuilt.Count > 0) return beingBuilt[0];
				else if (buildable.Count > 0) return buildable[0];
				else return Vector3D.Zero;
			}*/
        }
    }
}
