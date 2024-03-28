namespace CollectedScripts
{
    public class TheGalleryAirlock
    {
public const string RESET = "RESET";
public const string IN = "IN";
public const string OUT = "OUT";
public const string UPDATE = "UPDATE";

public static readonly Color CYCLING_COLOR = new Color(1f, 0.25f, 0.25f);
public static readonly Color DEPRESSURIZED_COLOR = new Color(1f, 0.5f, 0f);
public static readonly Color PRESSURIZED_COLOR = new Color(0.5f, 1f, 0.75f);

IMyAirVent _airVent;
List<IMyTerminalBlock> _buttonPanels = new List<IMyTerminalBlock>();
IMyDoor _innerDoor;
bool _isInitialized;
IMyLightingBlock _light;

Action _nextAction;
IMyDoor _outerDoor;
TimeSpan _timeout;
IMyTimerBlock _timer;

public void FindDevices() {
    _light = GetBlockRelativeTo(Me, 0, -1, 0) as IMyLightingBlock;
    _airVent = GetBlockRelativeTo(Me, 0, 1, -2) as IMyAirVent;
    _timer = GetBlockRelativeTo(Me, 0, 1, 0) as IMyTimerBlock;
    _outerDoor = GetBlockRelativeTo(Me, 1, 0, -1) as IMyDoor;
    _innerDoor = GetBlockRelativeTo(Me, -1, 0, -1) as IMyDoor;
    _buttonPanels.Clear();
    GetBlocksRelativeTo(Me, _buttonPanels,
        new Position(0, 0, -1),
        new Position(-2, 0, -1),
        new Position(2, 0, -1));
}

public void Main(string argument) {
    if (!_isInitialized)
        Initialize();

    switch (argument.ToUpper()) {
        case RESET:
            BeginReset();
            break;

        case UPDATE:
            Update();
            break;

        case IN:
            if (_nextAction != null)
                break;
            BeginCycleIn();
            break;

        case OUT:
            if (_nextAction != null)
                break;
            BeginCycleOut();
            break;
    }
}

public void OpenOuterDoor() {
    // Temporary workaround since the sliding doors don't respond to direct on/open commands
    _innerDoor.ApplyAction(Actions.ONOFF_OFF);
    //_outerDoor.ApplyAction(Actions.ONOFF_ON);
    _outerDoor.ApplyAction(Actions.OPEN_ON);
}

public void CloseOuterDoor() {
    // Temporary workaround since the sliding doors don't respond to direct on/open commands
    _innerDoor.ApplyAction(Actions.ONOFF_ON);
    //_outerDoor.ApplyAction(Actions.ONOFF_ON);
    _outerDoor.ApplyAction(Actions.OPEN_OFF);
}

public void LockOuterDoor() {
    // Temporary workaround since the sliding doors don't respond to direct on/open commands
    // _outerDoor.ApplyAction(Actions.ONOFF_OFF);
}

public void OpenInnerDoor() {
    // Temporary workaround since the sliding doors don't respond to direct on/open commands
    _outerDoor.ApplyAction(Actions.ONOFF_OFF);
    //_innerDoor.ApplyAction(Actions.ONOFF_ON);
    _innerDoor.ApplyAction(Actions.OPEN_ON);
}

public void CloseInnerDoor() {
    // Temporary workaround since the sliding doors don't respond to direct on/open commands
    _outerDoor.ApplyAction(Actions.ONOFF_ON);
    //_innerDoor.ApplyAction(Actions.ONOFF_ON);
    _innerDoor.ApplyAction(Actions.OPEN_OFF);
}

public void LockInnerDoor() {
    // Temporary workaround since the sliding doors don't respond to direct on/open commands
    // _innerDoor.ApplyAction(Actions.ONOFF_OFF);
}

public void Pressurize() {
    _airVent.ApplyAction(Actions.DEPRESSURIZE_OFF);
    if (_airVent.GetOxygenLevel() >= 1)
        WhilePressurizing();
}

public void Depressurize() {
    _airVent.ApplyAction(Actions.DEPRESSURIZE_ON);
    if (_airVent.GetOxygenLevel() <= 0)
        WhileDepressurizing();
}

public void EnableWarningLights() {
    _light.SetValue(Properties.COLOR, CYCLING_COLOR);
    _light.SetValue(Properties.BLINK_INTERVAL, 1f);
}

public void DisableWarningLights() {
    _light.SetValue(Properties.BLINK_INTERVAL, 0f);
}

public void EnqueueNext(Action action) {
    _nextAction = action;
    _timer.ApplyAction(Actions.TRIGGERNOW);
}

public void LockButtonPanels() {
    _buttonPanels.ForEach(block => { block.ApplyAction(Actions.ONOFF_OFF); });
}

public void UnlockButtonPanels() {
    _buttonPanels.ForEach(block => { block.ApplyAction(Actions.ONOFF_ON); });
}

public void Update() {
    if (_nextAction == null)
        return;
    var action = _nextAction;
    _nextAction = null;
    action();
}

void BeginReset() {
    BeginCycle();
    FindDevices();
    CloseInnerDoor();
    CloseOuterDoor();
    EnqueueNext(WhileClosingDoors);
}

void WhileClosingDoors() {
    if (_outerDoor.OpenRatio > 0 || _innerDoor.OpenRatio > 0) {
        EnqueueNext(WhileClosingDoors);
        return;
    }

    LockInnerDoor();
    LockOuterDoor();
    Pressurize();
    _timeout = TimeSpan.FromSeconds(4);
    EnqueueNext(WhilePressurizing);
}

public void BeginCycleIn() {
    BeginCycle();
    CloseOuterDoor();
    EnqueueNext(WhileClosingOuterDoor);
}

public void WhileClosingOuterDoor() {
    if (_outerDoor.OpenRatio > 0) {
        EnqueueNext(WhileClosingOuterDoor);
        return;
    }

    LockOuterDoor();
    Pressurize();
    _timeout = TimeSpan.FromSeconds(10);
    EnqueueNext(WhilePressurizing);
}

public void WhilePressurizing() {
    // If the outer door is open, close it, cancel and enable warning lights
    if (_outerDoor.Open || _outerDoor.OpenRatio > 0) {
        CloseOuterDoor();
        EnableWarningLights();
        return;
    }

    if (_airVent.GetOxygenLevel() < 0.99f) {
        if (_timeout.TotalSeconds <= 0) {
            Echo("Pressurization Timed Out\n Override Denied");
            return;
        }

        EnqueueNext(WhilePressurizing);
        _timeout -= Runtime.TimeSinceLastRun;
        return;
    }

    // Otherwise unlock and open the inner door
    OpenInnerDoor();
    _light.SetValue(Properties.COLOR, PRESSURIZED_COLOR);

    EndCycle();
}

public void BeginCycleOut() {
    BeginCycle();
    CloseInnerDoor();

    EnqueueNext(WhileClosingInnerDoor);
}

public void WhileClosingInnerDoor() {
    if (_innerDoor.OpenRatio > 0) {
        EnqueueNext(WhileClosingInnerDoor);
        return;
    }

    LockInnerDoor();
    Depressurize();
    _timeout = TimeSpan.FromSeconds(10);
    EnqueueNext(WhileDepressurizing);
}

public void WhileDepressurizing() {
    // If the inner door is open, close it, cancel and enable warning lights
    if (_innerDoor.Open || _innerDoor.OpenRatio > 0) {
        CloseInnerDoor();
        EnableWarningLights();
        return;
    }

    if (_airVent.GetOxygenLevel() > 0) {
        if (_timeout.TotalSeconds <= 0) {
            Echo("Depressurization Timed Out\nOverride Authorized");
        } else {
            EnqueueNext(WhileDepressurizing);
            _timeout -= Runtime.TimeSinceLastRun;
            return;
        }
    }

    // Otherwise unlock and open the outer door
    OpenOuterDoor();
    _light.SetValue(Properties.COLOR, DEPRESSURIZED_COLOR);

    EndCycle();
}

public void BeginCycle() {
    LockButtonPanels();
    EnableWarningLights();
}

public void EndCycle() {
    DisableWarningLights();
    UnlockButtonPanels();
}

public void Initialize() {
    _isInitialized = true;
    FindDevices();
}


#region Mixins

public Query Select(string namePattern = null) {
    return new Query(namePattern, GridTerminalSystem);
}

public IMyTerminalBlock GetBlockRelativeTo(IMyTerminalBlock baseBlock, int x, int y, int z) {
    var matrix = new MatrixI(baseBlock.Orientation);
    // For some reason the vectors are inversed.
    var pos = new Vector3I(-x, -y, -z);
    Vector3I transformed;
    Vector3I.Transform(ref pos, ref matrix, out transformed);
    transformed += baseBlock.Position;
    var slim = baseBlock.CubeGrid.GetCubeBlock(transformed);
    if (slim == null)
        return null;
    return slim.FatBlock as IMyTerminalBlock;
}

public void GetBlocksRelativeTo(IMyTerminalBlock baseBlock, List<IMyTerminalBlock> list,
    params Position[] positions) {
    var matrix = new MatrixI(baseBlock.Orientation);
    for (var i = 0; i < positions.Length; i++) {
        // For some reason the vectors are inversed.
        var pos = new Vector3I(-positions[i].X, -positions[i].Y, -positions[i].Z);
        Vector3I transformed;
        Vector3I.Transform(ref pos, ref matrix, out transformed);
        transformed += baseBlock.Position;
        var slim = baseBlock.CubeGrid.GetCubeBlock(transformed);
        if (slim == null) {
            list.Add(null);
            continue;
        }
        var fat = slim.FatBlock as IMyTerminalBlock;
        list.Add(fat);
    }
}

public struct Query {
    readonly string _namePattern;
    readonly IMyGridTerminalSystem _gts;
    string _groupPattern;
    HashSet<IMyCubeGrid> _gridWhitelist;
    Func<IMyTerminalBlock, bool> _predicate;
    bool _isValid;
    List<IMyTerminalBlock> _targetList;

    public Query(string namePattern, IMyGridTerminalSystem gts) {
        _gts = gts;
        _namePattern = namePattern;
        _groupPattern = null;
        _gridWhitelist = null;
        _predicate = null;
        _isValid = false;
        _targetList = null;
    }

    public int Count {
        get {
            Realize();
            return _targetList.Count;
        }
    }

    public IMyTerminalBlock this[int index] {
        get {
            Realize();
            return _targetList[index];
        }
    }

    public bool IsDefined() {
        return _gts != null;
    }

    public Query InGroup(string groupPattern) {
        var newQuery = this;
        newQuery._groupPattern = groupPattern;
        return newQuery;
    }

    public Query InGrid(IMyCubeGrid cubeGrid) {
        if (cubeGrid == null)
            return this;
        var newQuery = this;
        var oldList = _gridWhitelist;
        newQuery._gridWhitelist = oldList != null ? new HashSet<IMyCubeGrid>(oldList) : new HashSet<IMyCubeGrid>();
        newQuery._gridWhitelist.Add(cubeGrid);
        return newQuery;
    }

    public Query InGrids(params IMyCubeGrid[] cubeGrids) {
        return InGrids((IEnumerable<IMyCubeGrid>)cubeGrids);
    }

    public Query InGrids(IEnumerable<IMyCubeGrid> cubeGrids) {
        if (cubeGrids == null)
            return this;
        var newQuery = this;
        var oldList = _gridWhitelist;
        newQuery._gridWhitelist = oldList != null ? new HashSet<IMyCubeGrid>(oldList) : new HashSet<IMyCubeGrid>();
        var e = cubeGrids.GetEnumerator();
        while (e.MoveNext())
            newQuery._gridWhitelist.Add(e.Current);
        return newQuery;
    }

    public Query Where(Func<IMyTerminalBlock, bool> predicate) {
        var newQuery = this;
        var oldPredicate = newQuery._predicate;
        if (oldPredicate != null)
            newQuery._predicate = block => oldPredicate(block) && predicate(block);
        newQuery._predicate = predicate;
        return newQuery;
    }

    public Query OfType<T>() {
        return Where(block => block is T);
    }

    void Realize() {
        if (_isValid)
            return;
        Refresh();
    }

    public void Invalidate() {
        _isValid = false;
    }

    public void Refresh() {
        _isValid = true;

        if (_targetList == null) {
            _targetList = new List<IMyTerminalBlock>();
        } else {
            _targetList.Clear();
        }

        if (_groupPattern != null) {
            _gts.GetBlockGroups(BlockGroupsDummy);
            for (var i = 0; i < BlockGroupsDummy.Count; i++) {
                var group = BlockGroupsDummy[i];
                if (!IsLike(group.Name, _groupPattern)) {
                    continue;
                }
                group.GetBlocksOfType<IMyTerminalBlock>(_targetList, Filter);
            }
            BlockGroupsDummy.Clear();
        } else {
            _gts.GetBlocksOfType<IMyTerminalBlock>(_targetList, Filter);
        }
    }

    bool Filter(IMyTerminalBlock block) {
        if (_gridWhitelist != null) {
            if (!_gridWhitelist.Contains(block.CubeGrid))
                return false;
        }
        if (_namePattern != null && !IsLike(block.CustomName, _namePattern))
            return false;
        if (_predicate != null && !_predicate(block))
            return false;
        return true;
    }

    static bool IsLike(string subject, string pattern) {
        if (subject == null && pattern == null)
            return true;
        if (string.IsNullOrEmpty(pattern))
            return false;

        var starSubjectPos = -1;
        var starPatternPos = -1;
        var patternPos = 0;
        var subjectPos = 0;
        subject = subject ?? "";
        var n = Math.Max(pattern.Length, subject.Length);
        while (patternPos < n || subjectPos < n) {
            var subjectCh = subjectPos < subject.Length ? char.ToUpperInvariant(subject[subjectPos]) : '\0';
            var patternCh = patternPos < pattern.Length ? char.ToUpperInvariant(pattern[patternPos]) : '\0';
            switch (patternCh) {
                case '?':
                    subjectPos++;
                    patternPos++;
                    continue;

                case '*':
                    while (patternPos < pattern.Length && pattern[patternPos] == '*')
                        patternPos++;
                    if (patternPos == pattern.Length)
                        return true;
                    starSubjectPos = subjectPos;
                    starPatternPos = patternPos;
                    break;

                default:
                    if (starSubjectPos >= 0) {
                        if (subjectCh != patternCh) {
                            if (subjectPos == subject.Length)
                                return false;
                            starSubjectPos++;
                            subjectPos = starSubjectPos;
                            patternPos = starPatternPos;
                            continue;
                        }
                    } else {
                        if (subjectCh != patternCh)
                            return false;
                    }
                    patternPos++;
                    subjectPos++;
                    continue;
            }
        }
        return true;
    }

    public void ForEach(Action<IMyTerminalBlock> action) {
        Realize();
        _targetList.ForEach(action);
    }

    public IMyTerminalBlock FirstOrDefault() {
        Realize();
        if (_targetList.Count == 0)
            return null;
        return _targetList[0];
    }

    public IMyTerminalBlock First() {
        var block = FirstOrDefault();
        if (block == null) {
            var errorBuilder = new StringBuilder();
            errorBuilder.Append("Could not find block");
            //if (typeof(T) != typeof(IMyTerminalBlock))
            //    errorBuilder.Append(" of type " + typeof(T).Name);
            if (_namePattern != null)
                errorBuilder.Append(" matching \"" + _namePattern + "\"");
            if (_groupPattern != null)
                errorBuilder.Append(" within groups matching \"" + _groupPattern + "\"");
            throw new Exception(errorBuilder.ToString());
        }
        return block;
    }

    public bool IsEmpty() {
        Realize();
        return _targetList.Count == 0;
    }
}

public struct Position {
    public Position(int x, int y, int z) {
        X = x;
        Y = y;
        Z = z;
    }

    public int X;
    public int Y;
    public int Z;
}

static readonly List<IMyBlockGroup> BlockGroupsDummy = new List<IMyBlockGroup>();

public static class Properties {
    public const string COLOR = "Color";
    public const string BLINK_INTERVAL = "Blink Interval";
}

public static class Actions {
    public const string TRIGGERNOW = "TriggerNow";
    public const string START = "Start";
    public const string STOP = "Stop";
    public const string OPEN_OFF = "Open_Off";
    public const string OPEN_ON = "Open_On";
    public const string ONOFF_ON = "OnOff_On";
    public const string ONOFF_OFF = "OnOff_Off";
    public const string DEPRESSURIZE_ON = "Depressurize_On";
    public const string DEPRESSURIZE_OFF = "Depressurize_Off";
}

#endregion Mixins
    }
}