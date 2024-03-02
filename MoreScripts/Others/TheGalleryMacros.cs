namespace CollectedScripts
{
    public class TheGalleryMacros
    {
// ReSharper disable CheckNamespace

public void Main(string argument)
{
    if (!_isInitialized)
        Initialize();
    var commandLine = new CommandLine(argument);
    if (commandLine.IsEmpty())
        return;
    if (commandLine.ArgumentCount == 0) {
        Echo("Syntax Error");
        return;
    }

    Command command;
    if (!_commands.TryGetValue(commandLine[0], out command)) {
        Echo("Unknown command");
        return;
    }

    command.Run(commandLine);
}

public void Initialize()
{
    _isInitialized = true;

    _commands["ren"] = new RenameCommand(GridTerminalSystem, Echo, RunMacros);
    _commands["loc"] = new LocationCommand(GridTerminalSystem, Echo, RunMacros);

    _macros["%n%"] = new NumberMacro();
    _macros["%type%"] = new TypeMacro();
}

public string RunMacros(string input, IMyTerminalBlock block)
{
    var macroList = _macros;
    return _macroMatcher.Replace(input, match => {
        Macro macro;
        if (macroList.TryGetValue(match.Value, out macro))
            return macro.Transform(block);
        return match.Value;
    });
}

public class LocationCommand : Command
{
    public LocationCommand(IMyGridTerminalSystem gts, Action<string> echo, Func<string, IMyTerminalBlock, string> runMacros)
        : base("loc", gts, echo, runMacros)
    { }

    public override void Run(CommandLine commandLine)
    {
        var except = commandLine.ValueOf("except");
        var group = commandLine.ValueOf("group");

        var requiredArgumentCount = group != null ? 2 : 3;
        if (commandLine.ArgumentCount < requiredArgumentCount)
        {
            commandLine.Insert(0, "");
            Help(commandLine);
        }

        var select = (group == null || commandLine.ArgumentCount >= 3) ? commandLine[1] : null;

        var echo = Echo;
        Select(group, select, except, block => {
            var loc = block.Position;
            echo(block.CustomName + ": " + loc.X + "," + loc.Y + "," + loc.Z);
        });
    }

    public void Help(CommandLine commandLine) { }
}

public class RenameCommand : Command
{
    StringBuilder _stringBuilder = new StringBuilder();

    public RenameCommand(IMyGridTerminalSystem gts, Action<string> echo, Func<string, IMyTerminalBlock, string> runMacros)
        : base("ren", gts, echo, runMacros)
    { }

    public override void Run(CommandLine commandLine)
    {
        var except = commandLine.ValueOf("except");
        var group = commandLine.ValueOf("group");

        var requiredArgumentCount = group != null ? 2 : 3;
        if (commandLine.ArgumentCount < requiredArgumentCount) {
            commandLine.Insert(0, "");
            Help(commandLine);
        }

        var select = (group == null || commandLine.ArgumentCount >= 3) ? commandLine[1] : null;
        var newName = (group != null && commandLine.ArgumentCount == 2) ? commandLine[1] : commandLine[2];

        var count = 0;
        Select(group, select, except, block => {
            var transformedName = TransformName(newName, block);
            block.SetCustomName(transformedName);
            count++;
        });

        Echo("Renamed " + count + " block(s)");
    }

    string TransformName(string newName, IMyTerminalBlock block)
    {
        _stringBuilder.Clear();
        newName = RunMacros(newName, block);
        var wasAppended = false;
        for (var i = 0; i < newName.Length; i++)
        {
            var c = newName[i];
            if (c == '*')
            {
                if (!wasAppended)
                {
                    _stringBuilder.Append(block.CustomName);
                    wasAppended = true;
                }
            }
            else
                _stringBuilder.Append(c);
        }
        return _stringBuilder.ToString();
    }

    public void Help(CommandLine commandLine) {}
}

public abstract class Command
{
    protected Command(string key, IMyGridTerminalSystem gts, Action<string> echo, Func<string, IMyTerminalBlock, string> runMacros)
    {
        Key = key;
        Echo = echo;
        RunMacros = runMacros;
        GridTerminalSystem = gts;
    }

    public string Key { get; private set; }
    public Action<string> Echo { get; private set; }
    public Func<string, IMyTerminalBlock, string> RunMacros { get; private set; }
    public IMyGridTerminalSystem GridTerminalSystem { get; private set; }

    public abstract void Run(CommandLine commandLine);

    List<IMyBlockGroup> _blockGroups;
    List<IMyTerminalBlock> _blocks;

    public void Select(string groupName, string select, string except, Action<IMyTerminalBlock> action)
    {
        if (_blocks == null) _blocks = new List<IMyTerminalBlock>();
        if (!string.IsNullOrEmpty(groupName)) {
            if (_blockGroups == null) _blockGroups = new List<IMyBlockGroup>();
            _blockGroups.Clear();
            GridTerminalSystem.GetBlockGroups(_blockGroups);
            _blockGroups.RemoveAll(group => !group.Name.IsLike(groupName));
            _blockGroups.ForEach(group => {
                group.GetBlocksOfType<IMyTerminalBlock>(_blocks, block => (select == null || block.CustomName.IsLike(@select)) && (except == null || !block.CustomName.IsLike(except)));
            });
        } else {
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(_blocks, block => (select == null || block.CustomName.IsLike(select))
                                                                                   && (except == null || !block.CustomName.IsLike(except)));
        }

        _blocks.ForEach(action);
    }
}

public class TypeMacro : Macro
{
    Dictionary<string, string> _blockIds;

    public TypeMacro()
        : base("type")
    {
        _blockIds = new Dictionary<string, string>();
        _blockIds["Large Cargo Container"] = "Cargo";
        _blockIds["Small Cargo Container"] = "Cargo";
        _blockIds["Medium Cargo Container"] = "Cargo";
        _blockIds["Programmable block"] = "Terminal";
        _blockIds["Small Thruster"] = "IonThruster";
        _blockIds["Large Thruster"] = "IonThruster";
        _blockIds["Timer Block"] = "Timer";
        _blockIds["Gyroscope"] = "Gyro";
        _blockIds["Merge Block"] = "Hardpoint";
        _blockIds["Gravity Generator"] = "GravGen";
        _blockIds["Airtight Hangar Door"] = "HangarDoor";
        _blockIds["Interior Light"] = "Light";
        _blockIds["Door"] = "InternalDoor";
        _blockIds["Sliding Door"] = "ExternalDoor";
        _blockIds["Small Atmospheric Thruster"] = "Turbine";
        _blockIds["Large Atmospheric Thruster"] = "Turbine";
    }

    public override string Transform(IMyTerminalBlock block)
    {
        var name = block.DefinitionDisplayNameText;
        string refactoredName;
        if (_blockIds.TryGetValue(name, out refactoredName))
            return refactoredName;
        return block.DefinitionDisplayNameText.Replace(" ", "");
    }
}

public class NumberMacro : Macro
{
    Dictionary<string, int> _blockNos = new Dictionary<string, int>();

    public NumberMacro() : base("n") {}

    public override string Transform(IMyTerminalBlock block)
    {
        int no;
        var type = block.BlockDefinition.ToString();
        _blockNos.TryGetValue(type, out no);
        no++;
        _blockNos[type] = no;
        return no.ToString();
    }
}

public abstract class Macro
{
    public string Type { get; private set; }

    protected Macro(string type)
    {
        Type = type;
    }

    public abstract string Transform(IMyTerminalBlock block);
}

bool _isInitialized;
Dictionary<string, Command> _commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
Dictionary<string, Macro> _macros = new Dictionary<string, Macro>(StringComparer.OrdinalIgnoreCase);
System.Text.RegularExpressions.Regex _macroMatcher = new System.Text.RegularExpressions.Regex(@"%\w+%");


#region Mixins

public class CommandLine
{
    public CommandLine(string argument, params string[] valueSwitches)
    {
        _source = argument;
        var valSwitches = new HashSet<string>(valueSwitches, StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();
        var quoted = false;
        var i = 0;
        SkipWhitespace(ref i, argument);
        string lastSwitch = null;
        while (i < argument.Length) {
            var ch = argument[i];
            switch (ch) {
                case '-':
                    if (quoted) goto default;
                    PushArgument(ref lastSwitch, valSwitches, builder);
                    ParseSwitch(ref i, argument, builder, ref lastSwitch);
                    break;

                case ' ':
                    if (quoted) goto default;
                    PushArgument(ref lastSwitch, valSwitches, builder);
                    i++;
                    break;

                case '"':
                    i++;
                    quoted = !quoted;
                    if (!quoted && i < argument.Length && argument[i] == '"') {
                        quoted = true;
                        i++;
                    }
                    break;

                default:
                    builder.Append(ch);
                    i++;
                    break;
            }
        }
        PushArgument(ref lastSwitch, valSwitches, builder);
    }

    public bool IsEmpty()
    {
        return _switches.Count == 0 && _arguments.Count == 0;
    }

    void PushArgument(ref string lastSwitch, HashSet<string> valSwitches, StringBuilder builder)
    {
        if (builder.Length == 0)
            return;
        var argument = builder.ToString();
        builder.Clear();
        if (lastSwitch != null && valSwitches.Contains(lastSwitch)) {
            _switches[lastSwitch] = argument;
            lastSwitch = null;
        } else _arguments.Add(argument);
    }

    void SkipWhitespace(ref int i, string argument)
    {
        while (i < argument.Length && i == ' ')
            i++;
    }

    void ParseSwitch(ref int i, string argument, StringBuilder builder, ref string lastSwitch)
    {
        i++;
        builder.Clear();
        while (i < argument.Length) {
            var ch = argument[i];
            if (ch == ' ')
                break;
            builder.Append(ch);
            i++;
        }
        if (builder.Length > 0) {
            lastSwitch = builder.ToString();
            _switches.Add(lastSwitch, null);
        }
        builder.Clear();
        SkipWhitespace(ref i, argument);
    }

    public string this[int index]
    {
        get {
            if (index < 0 || index >= _arguments.Count)
                return null;
            return _arguments[index];
        }
    }

    public int ArgumentCount { get { return _arguments.Count; } }

    public bool IsSet(string switchName)
    {
        return _switches.ContainsKey(switchName);
    }

    public string ValueOf(string switchName)
    {
        string value;
        if (_switches.TryGetValue(switchName, out value))
            return value;
        return null;
    }

    public void Insert(int i, string value)
    {
        _arguments.Insert(i, value);
    }

    public override string ToString()
    {
        return _source;
    }

    List<string> _arguments = new List<string>();
    string _source;
    Dictionary<string, string> _switches = new Dictionary<string, string>();
}

#endregion Mixins

#region Extensions

} /* begin extension section */
public static class Common
{
    public static bool IsLike(this string subject, string pattern)
    {
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
        while (patternPos < n || subjectPos < n)
        {
            var subjectCh = subjectPos < subject.Length ? char.ToUpperInvariant(subject[subjectPos]) : '\0';
            var patternCh = patternPos < pattern.Length ? char.ToUpperInvariant(pattern[patternPos]) : '\0';
            switch (patternCh)
            {
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
                    if (starSubjectPos >= 0)
                    {
                        if (subjectCh != patternCh)
                        {
                            if (subjectPos == subject.Length)
                                return false;
                            starSubjectPos++;
                            subjectPos = starSubjectPos;
                            patternPos = starPatternPos;
                            continue;
                        }
                    }
                    else
                    {
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