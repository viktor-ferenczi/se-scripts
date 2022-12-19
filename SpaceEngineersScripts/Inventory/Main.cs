using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersScripts.Inventory
{
    public class Program: MyGridProgram
    {
        private readonly Cfg cfg;
        private readonly Log log;
        
        private IMyTextSurface Surface => Me.GetSurface(0);

        private void ShowLog()
        {
            var text = log.Text;
            Surface.WriteText(log.HighestSeverity.ToString());
            Echo(text);
        }

        // Blocks

        private List<IMyTerminalBlock> cargoBlocks = new List<IMyTerminalBlock>();
        private List<IMyBatteryBlock> batteryBlocks = new List<IMyBatteryBlock>();
        private List<IMyTextPanel> textPanels = new List<IMyTextPanel>();
        private List<IMyAssembler> assemblerBlocks = new List<IMyAssembler>();
        private IMyAssembler mainAssembler = null;

        // State

        private enum State
        {
            ScanCargo,
            ScanBatteries,
            ScanAssemblerQueues,
            ProduceMissing,
            Report,
            Reset,
        }

        private State state = State.ScanCargo;

        private int cargoIndex;
        private double cargoCapacity;
        private double cargoVolume;
        private double cargoMass;
        private bool attemptedToMoveCargo;

        private int batteryIndex;
        private double batteryCharge;
        private double batteryCapacity;

        private StringBuilder rawData = new StringBuilder();

        Dictionary<string, double> ore = new Dictionary<string, double>();
        Dictionary<string, double> ingot = new Dictionary<string, double>();
        Dictionary<string, double> component = new Dictionary<string, double>();
        Dictionary<string, double> ammo = new Dictionary<string, double>();
        Dictionary<string, double> other = new Dictionary<string, double>();

        private Dictionary<string, MyDefinitionId> restockComponents = new Dictionary<string, MyDefinitionId>();
        private Dictionary<string, int> queuedComponents = new Dictionary<string, int>();

        class Container : IComparable<Container>
        {
            private readonly IMyTerminalBlock container;
            private readonly IMyInventory inventory;

            public Container(IMyTerminalBlock container)
            {
                this.container = container;
                inventory = container.GetInventory();
            }

            private struct NameMapping
            {
                public readonly string customNamePart;
                public readonly string nameInMap;

                public NameMapping(string customNamePart, string nameInMap)
                {
                    this.customNamePart = customNamePart;
                    this.nameInMap = nameInMap;
                }
            }

            private NameMapping[] nameMappings =
            {
                new NameMapping("ore", "ore"),
                new NameMapping("ingot", "ingot"),
                new NameMapping("component", "component"),
                new NameMapping("weapon", "weapon"),
                new NameMapping("ammo", "ammo"),
                new NameMapping("hydrogen", "hydrogen"),
                new NameMapping("oxygen", "oxygen"),
                new NameMapping("gas", "gas"),
                new NameMapping("bottle", "gas"),
                new NameMapping("generator", "gas"),
                new NameMapping("tool", "tool"),
                new NameMapping("food", "food"),
                new NameMapping("consumable", "food"),
            };

            public object Name {
                get
                {
                    return container.CustomName;
                }
            }

            public void Register(Dictionary<string, List<Container>> map)
            {
                var name = container.CustomName.ToLower();
                foreach (var nameMapping in nameMappings)
                {
                    if (name.Contains(nameMapping.customNamePart))
                    {
                        Map(map, nameMapping.nameInMap);
                        return;
                    }
                }
                Map(map, "");
            }

            private void Map(Dictionary<string, List<Container>> map, string name)
            {
                List<Container> containers;

                if (!map.TryGetValue(name, out containers))
                {
                    containers = new List<Container>();
                    map[name] = containers;
                }

                containers.Add(this);
            }

            public bool CollectItem(IMyInventory source, int itemIndex)
            {
                return inventory.TransferItemFrom(source, itemIndex, stackIfPossible: true);
            }

            public int CompareTo(Container other)
            {
                return string.Compare(container.CustomName, other.container.CustomName, StringComparison.InvariantCultureIgnoreCase);
            }

            public bool IsTheSameBlock(IMyTerminalBlock block)
            {
                return block == container;
            }
        }

        private readonly Dictionary<string, List<Container>> containerMap = new Dictionary<string, List<Container>>();

        // Parameter parsing (commands)

        private enum Command
        {
            Default,
            Unknown,
        }

        private Command ParseCommand(string argument)
        {
            switch (argument)
            {
                case "":
                    return Command.Default;
                default:
                    return Command.Unknown;
            }
        }

        public Program()
        {
            cfg = new Cfg();
            log = new Log(cfg);
            
            Initialize();
            Load();
        }

        private void Initialize()
        {
            Surface.ContentType = ContentType.TEXT_AND_IMAGE;
            Surface.FontSize = 4f;

            Reset();
            ClearDisplays();
            
            var panel = FindPanels("status").FirstOrDefault();
            panel?.WriteText("Loading...");

            Runtime.UpdateFrequency = cfg.UpdateFrequency;
        }

        private void Reset()
        {
            log.Clear();

            state = State.ScanCargo;

            cargoIndex = 0;
            cargoCapacity = 0f;
            cargoVolume = 0f;
            cargoMass = 0f;
            attemptedToMoveCargo = false;

            batteryIndex = 0;
            batteryCapacity = 0f;
            batteryCharge = 0f;

            rawData.Clear();

            ore.Clear();
            ingot.Clear();
            component.Clear();
            ammo.Clear();
            other.Clear();
            
            // DO NOT CLEAR: restockComponents.Clear();
            queuedComponents.Clear();

            cargoBlocks.Clear();
            textPanels.Clear();
            containerMap.Clear();

            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, block => block.IsSameConstructAs(Me));
            cargoBlocks.AddRange(blocks.Where(block => block.InventoryCount > 0));

            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteryBlocks);
            GridTerminalSystem.GetBlockGroupWithName(cfg.PanelsGroup)?.GetBlocksOfType<IMyTextPanel>(textPanels);

            GridTerminalSystem.GetBlockGroupWithName(cfg.RestockAssemblersGroup)?.GetBlocksOfType<IMyAssembler>(assemblerBlocks);
            mainAssembler = assemblerBlocks.Where(assembler => assembler.CooperativeMode).FirstOrDefault() ?? assemblerBlocks.FirstOrDefault();
            
            var containerBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlockGroupWithName(cfg.SortedContainersGroup)?.GetBlocksOfType<IMyTerminalBlock>(containerBlocks);

            foreach (var containerBlock in containerBlocks)
            {
                var container = new Container(containerBlock);
                container.Register(containerMap);
            }

            foreach (var containers in containerMap.Values)
            {
                containers.Sort();
            }

            var names = containerMap.Keys.ToList();
            names.Sort();
            foreach (var name in names)
            {
                log.Debug("Sort: \"{0}\" => {1} blocks", name, containerMap[name].Count);
            }

            if (textPanels == null || textPanels.Count == 0)
            {
                log.Error("No text panels in group {0}", cfg.PanelsGroup);
                return;
            }

            foreach (var panel in textPanels)
            {
                panel.ContentType = ContentType.TEXT_AND_IMAGE;

                if (panel.CustomName.ToLower().Contains("status"))
                {
                    panel.Font = "InfoMessageBoxText";
                    panel.FontSize = cfg.StatusFontSize;
                }
                else if (panel.CustomName.ToLower().Contains("log"))
                {
                    panel.Font = "InfoMessageBoxText";
                    panel.FontSize = cfg.LogFontSize;
                }
                else
                {
                    panel.Font = "Monospace";
                    panel.FontSize = cfg.DefaultFontSize;
                }

                panel.TextPadding = panel.FontSize;
            }

            log.Info("Text panels: {0}", textPanels.Count);
            log.Info("Blocks with items: {0}", cargoBlocks.Count);
            log.Info("Sorted containers: {0}", containerBlocks.Count);
            log.Info("Battery blocks: {0}", batteryBlocks.Count);
            log.Info("Restock assemblers: {0}", assemblerBlocks.Count);
        }

        private void ClearDisplays()
        {
            Surface.WriteText("Init");
            
            foreach (var panel in textPanels)
            {
                panel.WriteText("");
            }
        }

        private void Load()
        {
            // Load state from Storage here
        }

        public void Save()
        {
            // Save state to Storage here
        }

        public void Main(string argument, UpdateType updateSource)
        {
            log.Debug("Main {0} {1}", updateSource, argument);

            switch (updateSource)
            {
                case UpdateType.None:
                case UpdateType.Terminal:
                case UpdateType.Trigger:
                //case UpdateType.Antenna:
                case UpdateType.Mod:
                case UpdateType.Script:
                case UpdateType.Once:
                case UpdateType.IGC:
                    log.Clear();

                    try {
                        ProcessCommand(argument);
                    }
                    catch (Exception e)
                    {
                        log.Error(e.ToString());
                    }

                    break;

                case UpdateType.Update1:
                case UpdateType.Update10:
                case UpdateType.Update100:
                    try {
                        PeriodicProcessing();
                    }
                    catch (Exception e)
                    {
                        log.Error(e.ToString());
                    }

                    if (log.HighestSeverity >= LogSeverity.Error)
                    {
                        StopPeriodicProcessing();
                        DisplayLog();
                    }

                    break;
            }

            ShowLog();
        }

        private void StopPeriodicProcessing()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        private void ProcessCommand(string argument)
        {
            var command = ParseCommand(argument);
            switch (command)
            {
                case Command.Default:
                    Reset();
                    break;

                default:
                    log.Error("Unknown command");
                    break;
            }
        }

        private void PeriodicProcessing()
        {
            switch (state)
            {
                case State.ScanCargo:
                    for (int batch = 0; batch < cfg.CargoBatchSize; batch++)
                    {
                        if (cargoIndex >= cargoBlocks.Count)
                        {
                            state = State.ScanBatteries;
                            break;
                        }
                        ScanCargo();
                        cargoIndex++;
                    }
                    break;

                case State.ScanBatteries:
                    for (int batch = 0; batch < cfg.BatteryBatchSize; batch++)
                    {
                        if (batteryIndex >= batteryBlocks.Count)
                        {
                            state = attemptedToMoveCargo ? State.Report : State.ScanAssemblerQueues;
                            break;
                        }
                        ScanBattery();
                        batteryIndex++;
                    }
                    break;

                case State.ScanAssemblerQueues:
                    ScanAssemblerQueues();
                    state = State.ProduceMissing;
                    break;
                
                case State.ProduceMissing:
                    ProduceMissing();
                    state = State.Report;
                    break;

                case State.Report:
                    Report();
                    state = State.Reset;
                    break;
                
                case State.Reset:
                    Reset();
                    break;
            }
        }

        private void ScanCargo()
        {
            var block = cargoBlocks[cargoIndex];
            if (block == null)
            {
                log.Debug("Block is missing");
                return;
            }

            if (!block.IsFunctional)
            {
                log.Warning("Broken block: " + block.CustomName);
                return;
            }

            if (block is IMyCargoContainer && !block.IsWorking)
            {
                log.Warning("Disabled cargo: " + block.CustomName);
                return;
            }

            log.Debug("[{0}]: {1}", cargoIndex, block.CustomName);

            SummarizeBlockCapacity(block);
            SummarizeBlockContents(block);
        }

        private void SummarizeBlockCapacity(IMyTerminalBlock block)
        {
            IMyInventory blockInventory = block.GetInventory(0);

            if (blockInventory == null)
            {
                return;
            }

            cargoCapacity += blockInventory.MaxVolume.RawValue;
            cargoVolume += blockInventory.CurrentVolume.RawValue;
            cargoMass += blockInventory.CurrentMass.RawValue;
        }

        private void SummarizeBlockContents(IMyTerminalBlock cargo)
        {
            for (int inventoryIndex = 0; inventoryIndex < cargo.InventoryCount; inventoryIndex++)
            {
                SummarizeCargoInventory(cargo, inventoryIndex);
            }
        }

        private void SummarizeCargoInventory(IMyTerminalBlock cargo, int inventoryIndex)
        {
            var allowAmmo = !(cargo is IMyUserControllableGun);
            var allowOre = !(cargo is IMyRefinery || cargo is IMyGasGenerator);
            var allowIngot = !(cargo is IMyReactor || cargo is IMyAssembler);

            // FIXME: Detect the safe zone by its interface. What is that?
            var allowComponent = !(cargo is IMyShipWelder || cargo.GetProperty("SafeZoneCreate") != null);

            var blockInventory = cargo.GetInventory(inventoryIndex);
            if (blockInventory == null)
            {
                return;
            }

            var items = new List<MyInventoryItem>();
            blockInventory.GetItems(items);

            if (items.Count == 0)
            {
                return;
            }

            bool mainAssemblerIsUsable = mainAssembler != null && !mainAssembler.Closed && mainAssembler.IsFunctional;
            
            List<Container> containers = null;

            for (int itemIndex = items.Count - 1; itemIndex >= 0; itemIndex--)
            {
                var item = items[itemIndex];

                if (item == null || item.Amount <= 0)
                {
                    continue;
                }

                var subtypeId = item.Type.SubtypeId;
                var lowercaseSubTypeName = subtypeId.ToLower();

                Dictionary<string, double> summary;
                switch (item.Type.TypeId)
                {
                    case "MyObjectBuilder_Ore":
                        summary = ore;
                        if (allowOre)
                        {
                            containers = FindContainers("ore");
                        }
                        break;
                    case "MyObjectBuilder_Ingot":
                        summary = ingot;
                        if (allowIngot)
                        {
                            containers = FindContainers("ingot") ?? FindContainers("");
                        }
                        break;
                    case "MyObjectBuilder_Component":
                        summary = component;
                        if (allowComponent)
                        {
                            containers = FindContainers("component") ?? FindContainers("");
                        }
                        if (mainAssemblerIsUsable && !restockComponents.ContainsKey(subtypeId))
                        {
                            // See https://forum.keenswh.com/threads/how-to-add-an-individual-component-to-the-assembler-queue.7393616/
                            // See https://steamcommunity.com/app/244850/discussions/0/527273452877873614/
                            var definitionString = "MyObjectBuilder_BlueprintDefinition/" + subtypeId;
                            var definitionId = MyDefinitionId.Parse(definitionString);
                            if (!mainAssembler.CanUseBlueprint(definitionId))
                            {
                                definitionId = MyDefinitionId.Parse(definitionString + "Component");
                            }
                            if (mainAssembler.CanUseBlueprint(definitionId))
                            {
                                restockComponents[subtypeId] = definitionId;
                            }
                        }
                        break;
                    case "MyObjectBuilder_AmmoMagazine":
                        summary = ammo;
                        if (allowAmmo)
                        {
                            containers = FindContainers("ammo") ?? FindContainers("weapon") ?? FindContainers("tool") ?? FindContainers("");
                        }
                        break;
                    case "MyObjectBuilder_PhysicalGunObject":
                        summary = other;
                        if (lowercaseSubTypeName.Contains("weld") ||
                            lowercaseSubTypeName.Contains("grind") ||
                            lowercaseSubTypeName.Contains("drill"))
                        {
                            containers = FindContainers("tool") ?? FindContainers("");
                        }
                        else
                        {
                            containers = FindContainers("weapon") ?? FindContainers("ammo") ??
                                         FindContainers("tool") ?? FindContainers("");
                        }

                        break;
                    case "MyObjectBuilder_Datapad":
                        summary = other;
                        containers = FindContainers("tool") ?? FindContainers("");
                        break;
                    case "MyObjectBuilder_GasContainerObject":
                        summary = other;
                        containers = FindContainers("hydrogen") ?? FindContainers("gas") ?? FindContainers("");
                        break;
                    case "MyObjectBuilder_OxygenContainerObject":
                        summary = other;
                        containers = FindContainers("oxygen") ?? FindContainers("gas") ?? FindContainers("");
                        break;
                    case "MyObjectBuilder_PhysicalObject":
                        summary = other;
                        containers = FindContainers("tool") ?? FindContainers("");
                        break;
                    case "MyObjectBuilder_ConsumableItem":
                        summary = other;
                        containers = FindContainers("food") ?? FindContainers("tool") ?? FindContainers("");
                        break;
                    default:
                        log.Warning("Skipping item with unknown item.Type.TypeID: {0}", item.Type.TypeId);
                        continue;
                }

                if (containers != null && containers.Count != 0)
                {
                    var alreadyInTheRightPlace = containers.Find(container => container.IsTheSameBlock(cargo)) != null;

                    if (!alreadyInTheRightPlace)
                    {
                        attemptedToMoveCargo = true;
                        foreach (var container in containers)
                        {
                            var mass = cargo.Mass;
                            if (container.CollectItem(blockInventory, itemIndex))
                            {
                                if (cargo.Mass != mass)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }

                Accumulate(summary, subtypeId, (double) item.Amount);
            }
        }

        private List<Container> FindContainers(string name)
        {
            return containerMap.GetValueOrDefault(name);
        }

        private void ScanBattery()
        {
            var battery = batteryBlocks[batteryIndex];
            if (battery == null)
            {
                log.Debug("Battery is missing");
                return;
            }

            if (!battery.IsFunctional)
            {
                log.Warning("Broken battery: " + battery.CustomName);
                return;
            }

            if (!battery.Enabled)
            {
                log.Warning("Disabled battery: " + battery.CustomName);
                return;
            }

            log.Debug("[{0}]: {1}", batteryIndex, battery.CustomName);

            batteryCapacity += battery.MaxStoredPower;
            batteryCharge += battery.CurrentStoredPower;
        }

        private void Report()
        {
            AppendRawData("now", FormatDateTime(DateTime.UtcNow));
            AppendRawData("status", log.HighestSeverity.ToString());
            AppendRawData("batteryCapacity", batteryCapacity);
            AppendRawData("batteryCharge", batteryCharge);
            AppendRawData("cargoCapacity", cargoCapacity);
            AppendRawData("cargoVolume", cargoVolume);
            AppendRawData("cargoMass", cargoMass * 1e-6);

            GridTerminalSystem.GetBlockGroupWithName(cfg.PanelsGroup)?.GetBlocksOfType<IMyTextPanel>(textPanels);

            foreach (var panel in textPanels)
            {
                log.Debug("Panel {0}", panel.CustomName);
            }

            DisplaySummary("ore", ore, formatOreName);
            DisplaySummary("ingot", ingot, formatIngotName);
            DisplaySummary("component", component, formatComponentName);
            DisplaySummary("ammo", ammo, null);
            DisplaySummary("other", other, null);

            DisplayStatus();
            DisplayLog();
            DisplayRawData();
        }

        private string formatOreName(string key)
        {
            Ore enumValue;
            if (Ore.TryParse(key, out enumValue))
            {
                return Naming.OreNames[enumValue];
            }

            return key;
        }

        private string formatIngotName(string key)
        {
            Ingot enumValue;
            if (Ingot.TryParse(key, out enumValue))
            {
                return Naming.IngotNames[enumValue];
            }

            return key;
        }

        private string formatComponentName(string key)
        {
            Component enumValue;
            if (Component.TryParse(key, out enumValue))
            {
                return Naming.ComponentNames[enumValue];
            }

            return key;
        }

        private delegate string ResourceNameFormatter(string key);

        private void DisplaySummary(string kind, Dictionary<string, double> summary, ResourceNameFormatter resourceNameFormatter)
        {
            var panels = FindPanels(kind).ToList();
            if (panels.Count == 0)
            {
                log.Debug("No text panel for {0}", kind);
                return;
            }

            var panelRowCount = (int)Math.Floor(cfg.PanelRowCount / panels[0].FontSize);
            var panelIndex = 0;

            foreach (var page in FormatSummary(kind, summary, resourceNameFormatter, panelRowCount))
            {
                if (panelIndex >= panels.Count)
                {
                    log.Warning("Not enough panels to display full {0} information", kind);
                    break;
                }

                panels[panelIndex++].WriteText(page);
            }

            while (panelIndex < panels.Count)
            {
                panels[panelIndex++].WriteText("");
            }
        }

        private IOrderedEnumerable<IMyTextPanel> FindPanels(string kind)
        {
            return textPanels
                .Where(panel => panel.CustomName.ToLower().Contains(kind))
                .OrderBy(panel => panel.CustomName);
        }

        private IEnumerable<StringBuilder> FormatSummary(
            string kind,
            Dictionary<string, double> summary,
            ResourceNameFormatter resourceNameFormatter,
            int panelRowCount)
        {
            var page = new StringBuilder();
            var lineCount = 0;

            if (cfg.ShowHeaders)
            {
                page.AppendLine(Capitalize(kind));
                page.AppendLine(new String('-', kind.Length));
                lineCount = 2;
            }

            var maxValue = summary.Count == 0 ? 0 : summary.Values.Max();
            var maxWidth = maxValue >= 10
                ? string.Format("{0:n0}", Math.Round(maxValue / cfg.DisplayPrecision) * cfg.DisplayPrecision).Length
                : 1;

            var sortedSummary = summary.ToList().OrderBy(pair => pair.Key);
            foreach (KeyValuePair<string, double> item in sortedSummary)
            {
                AppendRawData(kind + item.Key, item.Value);

                var formattedAmount = string.Format("{0:n0}", Math.Round(item.Value / cfg.DisplayPrecision) * cfg.DisplayPrecision);
                var name = resourceNameFormatter == null ? item.Key : resourceNameFormatter(item.Key);
                var line = formattedAmount.PadLeft(maxWidth) + " " + name;

                page.AppendLine(line);
                lineCount++;

                if (lineCount >= panelRowCount)
                {
                    yield return page;
                    page = new StringBuilder();
                    lineCount = 0;
                }
            }

            if (page.Length > 0)
            {
                yield return page;
            }
        }

        private void DisplayStatus()
        {
            var panel = FindPanels("status").FirstOrDefault();
            if (panel == null)
            {
                log.Warning("No status panel");
                return;
            }

            var text = new StringBuilder();

            text.AppendLine(FormatDateTime(DateTime.UtcNow));
            text.AppendLine("");

            text.AppendLine(string.Format("Battery: {0:p0}", batteryCharge / Math.Max(1, batteryCapacity)));
            text.AppendLine(string.Format("Energy: {0:n2} MWh", Math.Round(batteryCharge)));
            text.AppendLine("");

            text.AppendLine(string.Format("Cargo: {0:p0}", cargoVolume / Math.Max(1, cargoCapacity)));
            text.AppendLine(string.Format(" Capacity: {0:n0} ML", Math.Round(cargoCapacity * 1e-6)));
            text.AppendLine(string.Format(" Volume: {0:n0} ML", Math.Round(cargoVolume * 1e-6)));
            text.AppendLine(string.Format(" Mass: {0:n0} kg", Math.Round(cargoMass * 1e-6)));
            text.AppendLine("");

            panel.WriteText(text);
        }

        private void DisplayLog()
        {
            var panel = FindPanels("log").FirstOrDefault();
            if (panel == null)
            {
                log.Info("No log panel");
                return;
            }

            //var text = Wrap(log.ToString(), (int)(PANEL_COLUMN_COUNT / LOG_FONT_SIZE));
            var text = log.ToString();
            panel.WriteText(text);
        }

        private void DisplayRawData()
        {
            var panel = FindPanels("raw").FirstOrDefault();
            if (panel == null)
            {
                return;
            }

            panel.WriteText(rawData);
        }

        private static string FormatDateTime(DateTime dt)
        {
            return string.Format("{0:yyyy-MM-dd HH:mm:ss} UTC", dt);
        }

        // Utility functions

        private void AppendRawData(string name, string value)
        {
            rawData.AppendLine(string.Format("{0}: \"{1}\"", name, value));
        }

        private void AppendRawData(string name, double value)
        {
            rawData.AppendLine(string.Format("{0}: {1}", name, value));
        }

        private static void Accumulate(IDictionary<string, double> summary, string key, double amount)
        {
            if (summary.ContainsKey(key))
            {
                summary[key] += amount;
            }
            else
            {
                summary[key] = amount;
            }
        }

        private static string Capitalize(string text)
        {
            return text.Substring(0, 1).ToUpper() + text.Substring(1);
        }

        private static string Wrap(string text, int width)
        {
            var output = new StringBuilder();
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.TrimEnd();

                var position = 0;
                while (trimmed.Length > position + width)
                {
                    output.AppendLine(trimmed.Substring(position, width));
                    position += width;
                }

                if (position < trimmed.Length)
                {
                    output.AppendLine(trimmed.Substring(position));
                }
            }
            return output.ToString();
        }

        private void ScanAssemblerQueues()
        {
            foreach (var assembler in assemblerBlocks)
            {
                if (assembler.Closed || !assembler.IsFunctional)
                {
                    continue;
                }
                SummarizeAssemblerQueue(assembler);
            }
        }

        private void SummarizeAssemblerQueue(IMyAssembler assembler)
        {
            var queue = new List<MyProductionItem>();
            assembler.GetQueue(queue);
            
            foreach (var item in queue)
            {
                var subtypeName = item.BlueprintId.SubtypeName;
                var amount = queuedComponents.GetValueOrDefault(subtypeName, 0);
                queuedComponents[subtypeName] = amount + (int)item.Amount;
            }
        }
        
        private void ProduceMissing()
        {
            if (mainAssembler == null || restockComponents.Count == 0 || mainAssembler.Closed || !mainAssembler.IsFunctional)
            {
                return;
            }

            if (cfg.Debug)
            {
                foreach (var kv in component)
                {
                    log.Info(string.Format("CC S:{1} C:{0}", kv.Key, kv.Value));
                }
                log.Info("---");
            
                foreach (var kv in queuedComponents)
                {
                    log.Info(string.Format("QC Q:{1} C:{0}", kv.Key, kv.Value));
                }
                log.Info("---");
            }
            
            foreach (var kv in restockComponents)
            {
                var subtypeName = kv.Key;
                var subtypeNameComponent = subtypeName + "Component";
                
                var stock = (int)component.GetValueOrDefault(subtypeName);
                if (stock == 0)
                {
                    stock = (int)component.GetValueOrDefault(subtypeNameComponent);
                }

                var queued = queuedComponents.GetValueOrDefault(subtypeName);
                if (queued == 0)
                {
                    queued = queuedComponents.GetValueOrDefault(subtypeNameComponent);
                }
                
                var missing = cfg.RestockMinimum - queued - stock;
                if (missing > queued)
                {
                    var definitionId = kv.Value;
                    if (cfg.Debug)
                    {
                        log.Info(string.Format("RF S:{0} Q:{1} M:{2} C:{3}", stock, queued, missing, definitionId.SubtypeName));
                    }
                    mainAssembler.AddQueueItem(definitionId, (MyFixedPoint)(missing + cfg.RestockOverhead));
                }
            }
        }
    }
}