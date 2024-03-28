/*
 * R e a d m e
 * -----------
 * 
 * In this file you can include any instructions or other comments you want to have injected onto the 
 * top of your final script. You can safely delete this file if you do not want any such comments.
 * Group all printer arms(All advanced rotors, hinges and Omnibeams + 1 lcd and the projector into 1 group with the name: WELDTURRET
 * This includes adding all the printers arms advanced rotors, hinges and tools. 
 */

static bool CYCLE_TOOLS = true;
//whether tools should be disabled while turret is moving
static string TARGETSORT = "SPREAD"; Additional WeldSearchAlgo below, SPREAD(Works) - MINMOVE(Not working) - ZSORT(working i guess)
//which algo to use for target sorting
static double WELD_RANGE = 180;
//meters of range for turrets to be used

static bool USE_ANTENNA = true;//whether we even listen for remote users

static public string antenna_tag = "weldturret2";

Dictionary<string, WeldSearchAlgo> targetSorts
= new Dictionary<string, WeldSearchAlgo>{
{ "SPREAD",new WeldSearchLazySpread() },
{"MINMOVE", new WeldSearchMinMove() },
{"ZSORT", new WeldSearchZSort() }
};

public static Program gProgram = null;
public Program()
{
	gProgram = this;
	Runtime.UpdateFrequency = UpdateFrequency.Update10;
	network = new Network(this);
}
Network network = null;

public void Save()
{

}

static MultigridProjectorProgrammableBlockAgent mgp = null;

int tick = 0;
bool fr = true;
public void Main(string argument, UpdateType updateSource)
{
	tick += 10;
	if(fr)
	{
		fr = false;
		load();
	}
	if (mgp == null)
	{
		Echo("MGP NULL");
		return;
	}
	if(USE_ANTENNA)network.upd();

	if (scanner != null)
	{
		scanner.upd();
		scanner.updateWeldAssignments();
		if (scanner.weldTargets.Count == 0) scanner = null;
	}
	foreach (var t in turrets)
	{
		t.update();
	}

	if (argument == "weld")
	{
		weld();
	}/*else if(argument == "cycle")
			{
				foreach (var t in turrets)
				{
					t.mode(false);
				}
			}*/
	genStatus();
}

WeldTargetComp scanner = null;
void weld()
{
	scanner = new WeldTargetComp(this);

}


List<WeldTurret> turrets = new List<WeldTurret>();
List<IMyProjector> projectors = new List<IMyProjector>();
IMyTextSurface LCD = null;
IMyRadioAntenna antenna = null;
void load()
{
	mgp = new MultigridProjectorProgrammableBlockAgent(Me);
	if (mgp.Available) Echo("Multigrid Projector API ready!");

	var bg = GridTerminalSystem.GetBlockGroupWithName("WELDTURRET");
	if (bg != null)
	{
		List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
		bg.GetBlocks(blocks);

		List<IMyMotorAdvancedStator> hinges = new List<IMyMotorAdvancedStator>();
		List<IMyTerminalBlock> tools = new List<IMyTerminalBlock>();
		foreach (var b in blocks)
		{
			var st = b.BlockDefinition.SubtypeId;
			if (st == "LargeHinge" || st == "SmallHinge" || st == "MediumHinge")
			{
				hinges.Add((IMyMotorAdvancedStator)b);
			}
			else if (b is IMyTextSurface)
			{
				LCD = (IMyTextSurface)b;
				if (LCD.ContentType != ContentType.TEXT_AND_IMAGE)
				{
					LCD.ContentType = ContentType.TEXT_AND_IMAGE;
					LCD.FontSize = 0.666F;
				}
			}
			else if (!(b is IMyMotorAdvancedStator))
			{
				tools.Add(b);
			}
		}
		foreach (var b in blocks)
		{
			var st = b.BlockDefinition.SubtypeId;
			if (st == "LargeAdvancedStator" || st == "SmallAdvancedStatorSmall" || st == "SmallAdvancedStator")
			{
				var r = (IMyMotorAdvancedStator)b;
				IMyMotorAdvancedStator hi = null;//var t = new WeldTurret();
				//t.rotor = r;
				foreach (var h in hinges)
				{
					if (h.CubeGrid == r.TopGrid)
					{
						hi = h;

						break;
					}
				}
				if (hi != null)
				{
					var t = new WeldTurret(r, hi);
					foreach (var w in tools)
					{
						if (w.CubeGrid == t.hinge.TopGrid && w is IMyFunctionalBlock)
							t.tools.Add((IMyFunctionalBlock)w);
					}
					turrets.Add(t);
				}
			}
		}
	}
	Echo("Detected " + turrets.Count + " turret.");
	List<IMyRadioAntenna> ant = new List<IMyRadioAntenna>();
	GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(ant, b => b.Enabled);
	if (ant.Count > 0)
	{
		antenna = ant[0];
		Echo("Antenna found.");
	}
	else
	{
		Runtime.UpdateFrequency = UpdateFrequency.None;
		Echo("No antenna found.");
	}
	projectors.Clear();
	GridTerminalSystem.GetBlocksOfType<IMyProjector>(projectors);

}


public struct BlockLocation
{
    public readonly int GridIndex;
    public readonly Vector3I Position;

    public BlockLocation(int gridIndex, Vector3I position)
    {
        GridIndex = gridIndex;
        Position = position;
    }

    public override int GetHashCode()
    {
        return (((((GridIndex * 397) ^ Position.X) * 397) ^ Position.Y) * 397) ^ Position.Z;
    }
}

public enum BlockState
{
    // Block state is still unknown, not determined by the background worker yet
    Unknown = 0,

    // The block is not buildable due to lack of connectivity or colliding objects
    NotBuildable = 1,

    // The block has not built yet and ready to be built (side connections are good and no colliding objects)
    Buildable = 2,

    // The block is being built, but not to the level required by the blueprint (needs more welding)
    BeingBuilt = 4,

    // The block has been built to the level required by the blueprint or more
    FullyBuilt = 8,

    // There is mismatching block in the place of the projected block with a different definition than required by the blueprint
    Mismatch = 128
}

public class MultigridProjectorProgrammableBlockAgent
{
    private const string CompatibleMajorVersion = "0.";

    private readonly Delegate[] api;

    public bool Available { get; }
    public string Version { get; }

    // Returns the number of subgrids in the active projection, returns zero if there is no projection
    public int GetSubgridCount(long projectorId)
    {
        if (!Available)
            return 0;

        var fn = (Func<long, int>)api[1];
        return fn(projectorId);
    }

    // Returns the preview grid (aka hologram) for the given subgrid, it always exists if the projection is active, even if fully built
    public IMyCubeGrid GetPreviewGrid(long projectorId, int subgridIndex)
    {
        if (!Available)
            return null;

        var fn = (Func<long, int, IMyCubeGrid>)api[2];
        return fn(projectorId, subgridIndex);
    }

    // Returns the already built grid for the given subgrid if there is any, null if not built yet (the first subgrid is always built)
    public IMyCubeGrid GetBuiltGrid(long projectorId, int subgridIndex)
    {
        if (!Available)
            return null;

        var fn = (Func<long, int, IMyCubeGrid>)api[3];
        return fn(projectorId, subgridIndex);
    }

    // Returns the build state of a single projected block
    public BlockState GetBlockState(long projectorId, int subgridIndex, Vector3I position)
    {
        if (!Available)
            return BlockState.Unknown;

        var fn = (Func<long, int, Vector3I, int>)api[4];
        return (BlockState)fn(projectorId, subgridIndex, position);
    }

    // Writes the build state of the preview blocks into blockStates in a given subgrid and volume of cubes with the given state mask
    public bool GetBlockStates(Dictionary<Vector3I, BlockState> blockStates, long projectorId, int subgridIndex, BoundingBoxI box, int mask)
    {
        if (!Available)
            return false;

        var blockIntStates = new Dictionary<Vector3I, int>();
        var fn = (Func<Dictionary<Vector3I, int>, long, int, BoundingBoxI, int, bool>)api[5];
        if (!fn(blockIntStates, projectorId, subgridIndex, box, mask))
            return false;

        foreach (var pair in blockIntStates)
            blockStates[pair.Key] = (BlockState)pair.Value;

        return true;
    }

    // Returns the base connections of the blueprint: base position => top subgrid and top part position (only those connected in the blueprint)
    public Dictionary<Vector3I, BlockLocation> GetBaseConnections(long projectorId, int subgridIndex)
    {
        if (!Available)
            return null;

        var basePositions = new List<Vector3I>();
        var gridIndices = new List<int>();
        var topPositions = new List<Vector3I>();
        var fn = (Func<long, int, List<Vector3I>, List<int>, List<Vector3I>, bool>)api[6];
        if (!fn(projectorId, subgridIndex, basePositions, gridIndices, topPositions))
            return null;

        var baseConnections = new Dictionary<Vector3I, BlockLocation>();
        for (var i = 0; i < basePositions.Count; i++)
            baseConnections[basePositions[i]] = new BlockLocation(gridIndices[i], topPositions[i]);

        return baseConnections;
    }

    // Returns the top connections of the blueprint: top position => base subgrid and base part position (only those connected in the blueprint)
    public Dictionary<Vector3I, BlockLocation> GetTopConnections(long projectorId, int subgridIndex)
    {
        if (!Available)
            return null;

        var topPositions = new List<Vector3I>();
        var gridIndices = new List<int>();
        var basePositions = new List<Vector3I>();
        var fn = (Func<long, int, List<Vector3I>, List<int>, List<Vector3I>, bool>)api[7];
        if (!fn(projectorId, subgridIndex, topPositions, gridIndices, basePositions))
            return null;

        var topConnections = new Dictionary<Vector3I, BlockLocation>();
        for (var i = 0; i < topPositions.Count; i++)
            topConnections[topPositions[i]] = new BlockLocation(gridIndices[i], basePositions[i]);

        return topConnections;
    }

    // Returns the grid scan sequence number, incremented each time the preview grids/blocks change in any way in any of the subgrids.
    // Reset to zero on loading a blueprint or clearing (or turning OFF) the projector.
    public long GetScanNumber(long projectorId)
    {
        if (!Available)
            return 0;

        var fn = (Func<long, long>)api[8];
        return fn(projectorId);
    }

    // Returns YAML representation of all information available via API functions.
    // Returns an empty string if the grid scan sequence number is zero (see above).
    // The format may change in incompatible ways only on major version increases.
    // New fields may be introduced without notice with any MGP release as the API changes.
    public string GetYaml(long projectorId)
    {
        if (!Available)
            return "";

        var fn = (Func<long, string>)api[9];
        return fn(projectorId);
    }

    // Returns the hash of all block states of a subgrid, updated when the scan number increases.
    // Changes only if there is any block state change. Can be used to monitor for state changes efficiently.
    // Reset to zero on loading a blueprint or clearing (or turning OFF) the projector.
    public ulong GetStateHash(long projectorId, int subgridIndex)
    {
        if (!Available)
            return 0;

        var fn = (Func<long, int, ulong>)api[10];
        return fn(projectorId, subgridIndex);
    }

    // Returns true if the subgrid is fully built (completed)
    public bool IsSubgridComplete(long projectorId, int subgridIndex)
    {
        if (!Available)
            return false;

        var fn = (Func<long, int, bool>)api[11];
        return fn(projectorId, subgridIndex);
    }

    public MultigridProjectorProgrammableBlockAgent(IMyProgrammableBlock programmableBlock)
    {
        api = programmableBlock.GetProperty("MgpApi")?.As<Delegate[]>().GetValue(programmableBlock);
        if (api == null || api.Length < 12)
            return;

        var getVersion = api[0] as Func<string>;
        if (getVersion == null)
            return;

        Version = getVersion();
        if (Version == null || !Version.StartsWith(CompatibleMajorVersion))
            return;

        Available = true;
    }
}

enum MSG
{
	SETTARGET,
	TURRETPING,
}
class Network
{
	Program p;
	IMyBroadcastListener listener = null;

	public Network(Program p)
	{
		listener = p.IGC.RegisterBroadcastListener(antenna_tag);
		this.p = p;
	}
	public void upd()
	{
		p.culldead();
		p.sendpings();
		while (listener.HasPendingMessage)
		{
			MyIGCMessage myIGCMessage = listener.AcceptMessage();
			if (myIGCMessage.Tag != antenna_tag) continue;
			var d = myIGCMessage.Data;
			try
			{
				if (d == null) continue;

				if (d is MyTuple<int, int, Vector3D>)
				{
					var dat = (MyTuple<int, int, Vector3D>)d;
					var m = dat.Item1;
					if (m == (int)MSG.SETTARGET)
					{
						foreach (var t in p.turrets)
						{
							if (!t.remote && t.ID == dat.Item2)
							{
								t.setTarget(dat.Item3);
								break;
							}
						}
					}else if(m == (int)MSG.TURRETPING)
					{
						if ((p.Me.GetPosition() - dat.Item3).LengthSquared() < WELD_RANGE * WELD_RANGE * 2)
						{
							p.recping(dat.Item2, dat.Item3);
						}
					}
				}
			}
			catch (Exception) { }
		}
	}
}

int lstst = 0;
void genStatus()
{
	if (tick - lstst > 60 * 2)
	{

		lstst = tick;
		bool welding = scanner != null && scanner.weldTargets.Count > 0;

		StringBuilder b = new StringBuilder();
		bapp(b, "WELDER TURRET CONTROLLER\n\n");
		bapp(b, "Status:", welding ? "Welding" : "Idle", "\n");
		int l = 0;
		int r = 0;
		foreach (var t in turrets)
		{
			if (t.remote) r++;
			else l++;
		}
		bapp(b, "Turrets: ", turrets.Count, " (", l, " local, ", r, " remote)\n");
		bapp(b, "\n");
		var n = 0;
		foreach (var t in turrets)
		{
			bapp(b, "t#", n++, ">", v2ss(t.target), "\n");
		}

			if (welding)
		{

			//if (REMOTE == false)
			{
				IMyProjector c = null;
				foreach (var p in projectors)
				{
					if (p.IsProjecting && p.Enabled)
					{
						c = p;
						break;
					}
				}
				if (c != null)
				{
					int tb = c.TotalBlocks;
					int rb = c.RemainingBlocks;
					int ra = c.RemainingArmorBlocks;
					double perc = 100 - ((double)rb * 100 / tb);
					bapp(b, "Weld progress ", perc.ToString("0.0"), "%\n");
					bapp(b, "Total blocks in blueprint: ", tb, "\n");
					bapp(b, "Functional blocks remaining: ", rb - ra, "\n");
					bapp(b, "Armor blocks remaining: ", ra, "\n");

				}
			}
		}
		string w = b.ToString();
		if (w != lastwrite)
		{
			lastwrite = w;
			LCD.WriteText(w);
		}
	}
}

string lastwrite = "";

static void bapp(StringBuilder b, params object[] args)
{
	foreach (object a in args)
	{
		b.Append(a.ToString());
	}
}

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

static BoundingBoxI MaxBox = new BoundingBoxI(Vector3I.MinValue, Vector3I.MaxValue);
static Dictionary<Vector3I, BlockState> BlockStates = new Dictionary<Vector3I, BlockState>();

abstract class WeldSearchAlgo
{
	public List<WeldTarget> currenttargets = new List<WeldTarget>();
	public List<WeldTurret> busy = new List<WeldTurret>();
	public List<WeldTurret> retask = new List<WeldTurret>();

	abstract public void presort();
	abstract public int sort(WeldTarget x, WeldTarget y);
	virtual public void setTargets()
	{
		var weldTargets = gProgram.scanner.weldTargets;
		foreach (var tur in retask)
		{
			WeldTarget t = null;
			foreach (var e in weldTargets)
			{
				if (currenttargets.Contains(e)) continue;
				if (!tur.canTarget(e.wpos)) continue;
				t = e;
				break;
			}
			if (t == null)
			{
				if (weldTargets.Count > 0) t = weldTargets[0];
			}
			tur.setTarget(t);
			if (t != null && !currenttargets.Contains(t)) currenttargets.Add(t);
		}
	}
}

class WeldSearchLazySpread : WeldSearchAlgo

{
	Vector3D sds;

	override public void presort()
	{
		sds = Vector3D.Zero;
		int c = 0;
		foreach (var e in currenttargets)
		{
			sds += e.wpos;
			c++;
		}
		sds /= c;
	}
	bool prefType = true;
	public override int sort(WeldTarget x, WeldTarget y)
	{
		if (x.priority != y.priority && prefType) return x.priority.CompareTo(y.priority);
		var a = (sds - x.wpos).LengthSquared();
		var b = (sds - y.wpos).LengthSquared();
		return b.CompareTo(a);
	}
}
class WeldSearchMinMove : WeldSearchAlgo

{
	public override void presort()
	{

	}

	public override void setTargets()
	{
		var weldTargets = gProgram.scanner.weldTargets;
		foreach (var tur in retask)
		{
			curT = tur.target;
			weldTargets.Sort(sort);

			WeldTarget t = null;
			foreach (var e in weldTargets)
			{
				if (currenttargets.Contains(e)) continue;
				if (!tur.canTarget(e.wpos)) continue;
				t = e;
				break;
			}
			if (t == null)
			{
				if (weldTargets.Count > 0) t = weldTargets[0];
			}
			tur.setTarget(t);
			if (t != null && !currenttargets.Contains(t)) currenttargets.Add(t);
		}
	}
    Vector3D curT = Vector3D.Zero;
    public override int sort(WeldTarget x, WeldTarget y)
	{
		var a = (x.wpos - curT).LengthSquared();
		var b = (y.wpos - curT).LengthSquared();
		return a.CompareTo(b);
	}
}
class WeldSearchZSort : WeldSearchAlgo
{
	public override void presort()
	{

	}

	public override int sort(WeldTarget a, WeldTarget b)
	{
		var x = a.wpos;
		var y = b.wpos;
		var xr = x.X.CompareTo(y.X);
		if (xr != 0) return xr;
		xr = x.Y.CompareTo(y.Y);
		if (xr != 0) return xr;
		return x.Z.CompareTo(y.Z);
	}
}

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
		weldTargets.Add(new WeldTarget(gid, gpos, wpos, priority));
	}

	public WeldTargetComp(Program p)
	{
		this.p = p;
		partialBlocks.Clear();
		p.GridTerminalSystem.GetBlocks(partialBlocks);
		wSearch = p.targetSorts[TARGETSORT];
	}
	int ls = -1;
	void fk(int n)
	{
		//if (n == ls) return;
		//ls = n;
		//p.Me.CustomData = n.ToString();
	}
	public void upd()
	{
	if(mgp == null)
	{
			p.Echo("MGP NULL");
			return;
		}
		fk(0);
		bool changed = false;
		for (var i = 0; i < partialBlocks.Count;)
		{
			var b = partialBlocks[i];
			fk(1);
			if (b == null) break;
			var sb = b.CubeGrid.GetCubeBlock(b.Position);
			fk(2);
			if (sb == null || sb.IsFullIntegrity)
			{
				partialBlocks.RemoveAt(i);
				fk(3);
				changed = true;
			}
			else i++;
		}
		fk(4);
		foreach (var p in p.projectors)
		{
			if(p == null || !p.IsProjecting || !p.Enabled) continue;
			fk(5);
			long scan = -1;
			pscans.TryGetValue(p, out scan);
			fk(6);
			var ns = mgp.GetScanNumber(p.EntityId);
			fk(7);
			if (ns != scan)
			{
				fk(8);
				changed = true;
				pscans[p] = ns;
			}
		}
		fk(9);
		if (changed || (p.tick - lfsc) > 60 * 5)
		{
			lfsc = p.tick;
			//beingBuilt.Clear();
			//buildable.Clear();
			mismatch.Clear();
			weldTargets.Clear();
			fk(10);
			foreach (var p in p.projectors)
			{
				if (p == null || !p.IsProjecting || !p.Enabled) continue;
				var c = mgp.GetSubgridCount(p.EntityId);
				fk(11);
				for (var i = 0; i < c; i++)
				{
					fk(12);
					var pg = mgp.GetPreviewGrid(p.EntityId, i);
					fk(13);
					if (pg == null) continue;
					BlockStates.Clear();
					mgp.GetBlockStates(BlockStates, p.EntityId, i, MaxBox,
					(int)BlockState.Buildable | (int)BlockState.BeingBuilt | (int)BlockState.Mismatch);
					fk(14);
					foreach (var kvp in BlockStates)
					{
						fk(15);
						Vector3D np = pg.GridIntegerToWorld(kvp.Key);
						fk(16);
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
						fk(17);
					}
				}
			}
			fk(18);
			try
			{
				foreach (var e in partialBlocks)
				{
					if(e != null)add_t(p.Me.CubeGrid.EntityId, e.Position, e.GetPosition(), 0);
				}
				fk(19);
			}catch(Exception)
			{
				p.Echo("wut");
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
		//List<WeldTarget> occupied = new List<WeldTarget>();
		//List<WeldTurret> redir =	new List<WeldTurret>();
		//List<>
		wSearch.currenttargets.Clear();
		wSearch.retask.Clear();
		wSearch.busy.Clear();
		foreach (var t in p.turrets)
		{
			var e = get_t(t.wtarget);
			if (e == null)
			{
				wSearch.retask.Add(t);
			}
			else
			{
				wSearch.currenttargets.Add(e);
				wSearch.busy.Add(t);
			}
        }
		if(wSearch.retask.Count > 0)
		{
			wSearch.presort();

			weldTargets.Sort(wSearch.sort);

			wSearch.setTargets();

			/*Vector3D aggregate = Vector3D.Zero;
					int c = 0;
					foreach(var e in occupied)
					{
						aggregate += e.wpos;
						c++;
					}
					aggregate /= c;
					sds = aggregate;

					weldTargets.Sort(getsort);

					foreach (var tur in redir)
					{
						WeldTarget t = null;
						foreach (var e in weldTargets)
						{
							if (occupied.Contains(e)) continue;
							t = e;
							break;
						}
						if(t == null)
						{
							if(weldTargets.Count > 0)t=weldTargets[0];
						}
						tur.setTarget(t);
						if (t != null && !occupied.Contains(t)) occupied.Add(t);

						//Comparison<WeldTarget> comparison  = statdistsort;
					}*/
		}
	}

	/*class TargetAlgorithm
			{
				public string name = "";
				public Func<WeldTarget, WeldTarget, int> sorter = null;
			}*/

	static public WeldSearchAlgo wSearch = null;

	/*static public Func<WeldTarget, WeldTarget, int> sorter;
			static public int getsort(WeldTarget x, WeldTarget y)
			{
				return sorter(x, y);
			}*/

	/*static public bool prefType = true;
			static public Vector3D sds = new Vector3D();
			static public int lazyspread(WeldTarget x, WeldTarget y)
			{
				if(x.priority != y.priority && prefType) return x.priority.CompareTo(y.priority);
				var a = (sds - x.wpos).LengthSquared();
				var b = (sds -y.wpos).LengthSquared();
				return b.CompareTo(a);
			}
			public int zerosort(Vector3D x, Vector3D y)
			{
				var xr = x.X.CompareTo(y.X);
				if (xr != 0) return xr;
				xr = x.Y.CompareTo(y.Y);
				if (xr != 0) return xr;
				return x.Z.CompareTo(y.Z);
			}*/

	//Func<int, WeldTarget, WeldTarget> F = statdistsort;
	/*public int absdiistsort(WeldTarget x, WeldTarget y)
			{
				var xd = 0d;
				var yd = 0d;
				foreach(var t in p.turrets)
				{
					if(t.target != Vector3D.Zero)
					{
						xd += (t.target - x.wpos).LengthSquared();
						yd += (t.target - y.wpos).LengthSquared();
					}
				}
				return yd.CompareTo(xd);
			}*/

	/*public Vector3D weldtarget()
			{
				if (partialBlocks.Count > 0) return partialBlocks[0].GetPosition();
				else if (beingBuilt.Count > 0) return beingBuilt[0];
				else if (buildable.Count > 0) return buildable[0];
				else return Vector3D.Zero;
			}*/
}

static Random rnd = new Random();
class WeldTurret
{
	public WeldTurret(IMyMotorAdvancedStator rotor, IMyMotorAdvancedStator hinge)
	{
		ID = rnd.Next();
		this.rotor = rotor;
		this.hinge = hinge;
		remote = false;
		this.setTarget(Vector3D.Zero);
	}
	public WeldTurret(int ID, Vector3D rpos)
	{
		this.ID = ID;
		this.ping = gProgram.tick;
		remote = true;
		target = Vector3D.Zero;
		this.rpos = rpos;
	}
	public Vector3D target = new Vector3D(1,0,0);
	Vector3D heading = Vector3D.Zero;
	public WeldTarget wtarget = null;

	public bool canTarget(Vector3D pos)
	{//WELD_RANGE
		Vector3D p = rpos;
		if (!remote) p = hinge.GetPosition();
		Vector3D o = pos-p;
		if (remote) return o.LengthSquared() < WELD_RANGE * WELD_RANGE;
		return Vector3D.Dot(o, rotor.WorldMatrix.Up) > 0 && o.LengthSquared() < WELD_RANGE* WELD_RANGE;
	}
	public void setTarget(WeldTarget w)
	{
		wtarget = w;
		setTarget(w != null ? w.wpos : Vector3D.Zero);
	}
	public void setTarget(Vector3D pos)
	{
		if (pos != Vector3D.Zero && !canTarget(pos)) return;

		if(remote)
		{
			if (target != pos)
			{
				target = pos;
				MyTuple<int, int, Vector3D> pkt =
				new MyTuple<int, int, Vector3D>((int)MSG.SETTARGET, ID, pos);
				gProgram.IGC.SendBroadcastMessage(antenna_tag, pkt);
			}
		}else
		{
			if (target != pos)
			{
				target = pos;
				if (target != Vector3D.Zero)heading = (target - hinge.Top.GetPosition()).Normalized();
				else heading = rotor.WorldMatrix.Up;
				headingDirty = true;
			}
		}
	}

	bool headingDirty = false;
	public void update()
	{
		if (remote) return;
		rotate();
		if (target != Vector3D.Zero)
		{
		//	rotate(target);
		}
		else set(false);
	}
	public bool remote = true;

	public int ID = -1;
	public int ping = -1;
	public Vector3D rpos = Vector3D.Zero;
	public IMyMotorAdvancedStator hinge = null;
	public IMyMotorAdvancedStator rotor = null;
	public List<IMyFunctionalBlock> tools = new List<IMyFunctionalBlock>();

	double lalign = 999;
	double rrr = 0.0174533;

	void rotate()
	{
		//Vector3D heading = (target - hinge.Top.GetPosition()).Normalized();

		if (headingDirty)
		{
			var a = AimRotorAtPosition(rotor, heading, hinge.WorldMatrix.Forward, 1, 1f / 6f, true);
			var b = AimRotorAtPosition(rotor, heading, hinge.WorldMatrix.Backward, 1, 1f / 6f, true);
			if (a < b) AimRotorAtPosition(rotor, heading, hinge.WorldMatrix.Forward);
			else AimRotorAtPosition(rotor, heading, hinge.WorldMatrix.Backward);

			AimRotorAtPosition(hinge, heading, hinge.Top.WorldMatrix.Left);

			var align = Vector3D.Angle(heading, hinge.Top.WorldMatrix.Left);
			set(align < rrr * 2 || (!CYCLE_TOOLS && target != Vector3D.Zero));
			//mode(true);
			lalign = align;
			if(align < rrr*0.5)
			{
				rotor.TargetVelocityRad = 0;
				hinge.TargetVelocityRad = 0;
				headingDirty = false;
			}
		}
	}
	bool lon = false;
    void set(bool on)
    {
        if (on == lon) return;
        lon = on;
        foreach (var b in tools)
        {
            if (on)
            {
                b.Enabled = on;
                var action = b.GetActionWithName("ToolCore_Shoot_Action");
                if (action != null)
                {
                    action.Apply(b);
                }
            }
            else
            {
                var action = b.GetActionWithName("ToolCore_Shoot_Action");
                if (action != null)
                {
                    action.Apply(b);
                }
            }
        }
    }

}
static int stale = 60 * 5;
void culldead()
{
	for (int i = 0; i < turrets.Count;)
	{
		var e = turrets[i];
		if (e.remote && tick - e.ping > stale+60) turrets.RemoveAt(i);
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
					new MyTuple<int, int, Vector3D>((int)MSG.TURRETPING, e.ID, e.hinge.GetPosition());
			gProgram.IGC.SendBroadcastMessage(antenna_tag, pkt);
		}
	}
}
void recping(int ID, Vector3D tpos)
{
	foreach (var t in turrets)
	{
		if (t.ID == ID)
		{
			t.ping = tick;
			t.rpos = tpos;
			return;
		}
	}
	turrets.Add(new WeldTurret(ID, tpos));
}