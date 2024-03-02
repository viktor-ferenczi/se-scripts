namespace CollectedScripts
{
    public class TheGalleryBlockInfo
    {
public static PosInt ToPosInt(Vector3I vec) {
    var v = (Vector3)vec;
    return new PosInt {
        X = (int)v.GetDim(0),
        Y = (int)v.GetDim(1),
        Z = (int)v.GetDim(2)
    };
}

Query _panels;

public void Main() {
    if (!_panels.IsDefined()) {
        _panels = Select("Gallery.Display").Where(block => block is IMyTextPanel);
    } else {
        _panels.Refresh();
    }
    Echo(_panels.Count + " displays updated");
    _panels.Refresh();
    _panels.ForEach(block => {
        var panel = (IMyTextPanel)block;
        var position = ToPosInt(panel.Position);
        IMyTerminalBlock device = null;
        for (var offset = 0; offset <= 4; offset++) {
            position.Z++;
            var thingy = panel.CubeGrid.GetCubeBlock(position.ToVector3I());
            if (thingy == null)
                continue;
            device = thingy.FatBlock as IMyTerminalBlock;
            if (device != null)
                break;
        }

        var builder = new StringBuilder();
        builder.AppendLine();
        if (device == null)
            builder.AppendLine("    No Device Found");
        else {
            var properties = new List<ITerminalProperty>();
            var actions = new List<ITerminalAction>();
            device.GetProperties(properties);
            device.GetActions(actions);

            builder.AppendLine("    " + device.DisplayNameText);
            builder.AppendLine("    Properties: ");
            properties.ForEach(property => builder.AppendLine($"      {property.Id}: {property.TypeName}"));
            builder.AppendLine("    Actions: ");
            actions.ForEach(action => builder.AppendLine($"      {action.Id} ({action.Name})"));
        }
        panel.WritePublicText(builder.ToString());
        panel.ShowPublicTextOnScreen();
    });
}

public struct PosInt {
    public int X;
    public int Y;
    public int Z;

    public PosInt(Vector3I ipos) {
        var vpos = (Vector3)ipos;
        X = (int)vpos.GetDim(0);
        Y = (int)vpos.GetDim(1);
        Z = (int)vpos.GetDim(2);
    }

    public PosInt(int x, int y, int z) {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3I ToVector3I() {
        return new Vector3I(X, Y, Z);
    }

    public string ToPosString() {
        return $"{X}x{Y}x{Z}";
    }
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
    return slim?.FatBlock as IMyTerminalBlock;
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

#endregion Mixins

#region Extensions
} /* begin extension section */

public static class Common {
public static bool IsLike(this string subject, string pattern) {
    if (subject == null && pattern == null)
        return true;
    if (string.IsNullOrEmpty(pattern))
        return false;

    var starSubjectPos = -1;
    var starPatternPos = -1;
    var patternPos = 0;
    var subjectPos = 0;
    var n = Math.Max(pattern.Length, subject.Length);
    subject = subject ?? "";
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

/* } end extension section */

#endregion Extensions
    }
}