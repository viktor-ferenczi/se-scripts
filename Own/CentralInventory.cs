/* Central Inventory

Periodically builds a summary on cargo and power status.
Displays human readable summary on text panels.
Produces machine readable information to be used by other programs.
Optionally sorts items into specific containers.

=== How to set up inventory display ===

Build a programmable block.
Copy-paste all code from the CodeEditor region below into the block.
Compile and run the code in the block.

Build large text panels with the following words in their name (case insensitive):
- Resource
- Ore
- Ingot
- Component
- Ammo
- Other
- Status
- Log
- Raw

Assign your text panels to the "Inventory Panels" group.

Run the programmable block again after making changes to the group.

Text panel content mode, font and character size will be overridden
by the script in order to nicely fit the contents. Colors are free
to configure to match your environment.

You may need two Component panels and two Other panels to fit all items.
All text panels inside each resource type must have the same size.
Panels of the same type are concatenated in ascending name order.

The Status panel displays top level summaries.

The Error panel displays warnings and errors.

The Raw panel displays raw inventory information in a YAML like format.
It can be used by compatible programs to quickly acquire inventory
information without walking on all the blocks again.

=== How to set up inventory sorting ===

Sorting uses the same programmable block as the display functionality,
do not add a separate one. You can use only sorting by setting up the
programmable block like above, but without the displays configured.

Include the following words in the name of the cargo blocks you expect
the items to be placed into (case insensitive):
- Ore
- Ingot
- Component
- Weapon
- Ammo
- Tool
- Food

Make sure your have the following words in the name of your gas tanks or
generator in order to automatically pull bottles for refilling:
- Hydrogen or H2
- Oxygen or O2
- Generator

Sorting is enabled by assigning your target containers (where you expect
the sorted items to be placed) to the "Sorted Containers" group.

Run the programmable block again after making changes to the group.

For safety reasons sorting functionality will NOT pull
- ammo out of any turrets or interior guns
- ore out of refineries
- ice out of gas generators
- ingots out of assemblers
- ingots (Uranium) out of reactors
- components out of enabled welders
- zone chips out from safe zone generators

Sorting collects items from the programmable block's own grid.
This is to prevent pulling out cargo from all docked ships.

=== Remarks ===

Display updates and sorting will be slower if you have more blocks.
It is normal and the script is designed this way to prevent the PB
from burning down on multiplayer servers.

You can adjust BATCH_SIZE to change the block scanning speed, higher
value will result in faster processing at the cost of more computation
and higher risk of your PB ending up in smoke and fire. If your
PB burns down, then try to decrease BATCH_SIZE. It may need to be
tuned for the server you are playing on.

Mod compatibility is entirely untested. Works well with vanialla Space Engineers
in Experimental mode and scripts enabled as of 2019-11-07. Also works
on Alehouse PvP Two multiplayer server.

=== Credits ===

Resource name tables were taken from Projector2LCD by Juggernaut93:
https://steamcommunity.com/sharedfiles/filedetails/?id=1500259551

*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Skeleton;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Weapons;
using VRage;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Profiler;
using VRageMath;
using ContentType = VRage.Game.GUI.TextPanel.ContentType;
using IMyAirtightSlideDoor = Sandbox.ModAPI.Ingame.IMyAirtightSlideDoor;
using IMyAssembler = Sandbox.ModAPI.Ingame.IMyAssembler;
using IMyBatteryBlock = Sandbox.ModAPI.Ingame.IMyBatteryBlock;
using IMyBlockGroup = Sandbox.ModAPI.Ingame.IMyBlockGroup;
using IMyCargoContainer = Sandbox.ModAPI.Ingame.IMyCargoContainer;
using IMyCubeBlock = VRage.Game.ModAPI.Ingame.IMyCubeBlock;
using IMyGasGenerator = Sandbox.ModAPI.Ingame.IMyGasGenerator;
using IMyReactor = Sandbox.ModAPI.Ingame.IMyReactor;
using IMyRefinery = Sandbox.ModAPI.Ingame.IMyRefinery;
using IMyShipWelder = Sandbox.ModAPI.Ingame.IMyShipWelder;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyTextPanel = Sandbox.ModAPI.Ingame.IMyTextPanel;
using IMyTextSurface = Sandbox.ModAPI.Ingame.IMyTextSurface;
using IMyUserControllableGun = Sandbox.ModAPI.Ingame.IMyUserControllableGun;
using MyInventoryItem = VRage.Game.ModAPI.Ingame.MyInventoryItem;

namespace CentralInventory
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        // Config

        private const string PANELS_GROUP = "Inventory Panels";
        private const string SORTED_CONTAINERS_GROUP = "Sorted Containers";
        private const string REFILL_ASSEMBLERS_GROUP = "Refill Assemblers";
        private const int REFILL_MINIMUM = 30;
        private const int PANEL_ROW_COUNT = 17;
        private const int PANEL_COLUMN_COUNT = 25;
        private const double DISPLAY_PRECISION = 1;
        private const int CARGO_BATCH_SIZE = 3;
        private const int BATTERY_BATCH_SIZE = 10;
        private const bool SHOW_HEADERS = true;
        private const float DEFAULT_FONT_SIZE = 1f;
        private const float STATUS_FONT_SIZE = 1.6f;
        private const float LOG_FONT_SIZE = 0.667f;
        private const UpdateFrequency UPDATE_FREQUENCY = UpdateFrequency.Update10;

        // Debugging

        enum LogSeverity
        {
            Ok,
            Warning,
            Error,
        }

        private bool DEBUG = false;
        private LogSeverity highestLogLogSeverity = LogSeverity.Ok;
        private readonly StringBuilder log = new StringBuilder();

        private void Log(string formatString, params object[] args)
        {
            log.AppendFormat(formatString + "\n", args);
        }

        private void Debug(string formatString, params object[] args)
        {
            if (DEBUG)
            {
                Log("D: " + formatString, args);
            }
        }

        private void Warning(string formatString, params object[] args)
        {
            Log("W: " + formatString, args);
            IncreaseSeverity(LogSeverity.Warning);
        }

        private void Error(string formatString, params object[] args)
        {
            Log("E: " + formatString, args);
            IncreaseSeverity(LogSeverity.Error);
        }

        private void ClearLog()
        {
            highestLogLogSeverity = LogSeverity.Ok;
            log.Clear();
        }

        private void ShowLog()
        {
            Echo(log.ToString());
            Surface.WriteText(highestLogLogSeverity.ToString());
        }

        private void IncreaseSeverity(LogSeverity severity)
        {
            if (highestLogLogSeverity < severity)
            {
                highestLogLogSeverity = severity;
            }
        }

        private IMyTextSurface Surface
        {
            get
            {
                return Me.GetSurface(0);
            }
        }

        // Tables

        private enum Component
        {
            BulletproofGlass,
            ComputerComponent,
            ConstructionComponent,
            DetectorComponent,
            Display,
            ExplosivesComponent,
            GirderComponent,
            GravityGeneratorComponent,
            InteriorPlate,
            LargeTube,
            MedicalComponent,
            MetalGrid,
            MotorComponent,
            PowerCell,
            RadioCommunicationComponent,
            ReactorComponent,
            SmallTube,
            SolarCell,
            SteelPlate,
            Superconductor,
            ThrustComponent,
            ZoneChip,
        }

        private readonly Dictionary<Component, string> componentNames = new Dictionary<Component, string>()
        {
            [Component.BulletproofGlass] = "Bulletproof Glass",
            [Component.ComputerComponent] = "Computer",
            [Component.ConstructionComponent] = "Construction Component",
            [Component.DetectorComponent] = "Detector Component",
            [Component.Display] = "Display",
            [Component.ExplosivesComponent] = "Explosive",
            [Component.GirderComponent] = "Girder",
            [Component.GravityGeneratorComponent] = "Gravity Gen Component",
            [Component.InteriorPlate] = "Interior Plate",
            [Component.LargeTube] = "Large Steel Tube",
            [Component.MedicalComponent] = "Medical Component",
            [Component.MetalGrid] = "Metal Grid",
            [Component.MotorComponent] = "Motor Component",
            [Component.PowerCell] = "Power Cell",
            [Component.RadioCommunicationComponent] = "Radio-Comm Component",
            [Component.ReactorComponent] = "Reactor Component",
            [Component.SmallTube] = "Small Steel Tube",
            [Component.SolarCell] = "Solar Cell",
            [Component.SteelPlate] = "Steel Plate",
            [Component.Superconductor] = "Superconductor Component",
            [Component.ThrustComponent] = "Thruster Component",
            [Component.ZoneChip] = "Zone Chip",
        };

        private enum Ingot
        {
            Cobalt, Gold, Iron, Magnesium, Nickel, Platinum, Silicon, Silver, Stone, Uranium
        }

        private readonly Dictionary<Ingot, string> ingotNames = new Dictionary<Ingot, string>()
        {
            [Ingot.Cobalt] = "Cobalt Ingot",
            [Ingot.Gold] = "Gold Ingot",
            [Ingot.Iron] = "Iron Ingot",
            [Ingot.Magnesium] = "Magnesium Powder",
            [Ingot.Nickel] = "Nickel Ingot",
            [Ingot.Platinum] = "Platinum Ingot",
            [Ingot.Silicon] = "Silicon Wafer",
            [Ingot.Silver] = "Silver Ingot",
            [Ingot.Stone] = "Gravel",
            [Ingot.Uranium] = "Uranium Ingot",
        };

        private enum Ore
        {
            Cobalt, Gold, Ice, Iron, Magnesium, Nickel, Platinum, Scrap, Silicon, Silver, Stone, Uranium
        }

        private readonly Dictionary<Ore, string> oreNames = new Dictionary<Ore, string>()
        {
            [Ore.Cobalt] = "Cobalt Ore",
            [Ore.Gold] = "Gold Ore",
            [Ore.Ice] = "Ice Ore",
            [Ore.Iron] = "Iron Ore",
            [Ore.Magnesium] = "Magnesium Ore",
            [Ore.Nickel] = "Nickel Ore",
            [Ore.Platinum] = "Platinum Ore",
            [Ore.Scrap] = "Scrap Metal",
            [Ore.Silicon] = "Silicon Ore",
            [Ore.Silver] = "Silver Ore",
            [Ore.Stone] = "Stone",
            [Ore.Uranium] = "Uranium Ore",
        };

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
            Report,
            ScanAssemblerQueues,
            ProduceMissing,
            Reset,
        }

        private State state = State.ScanCargo;

        private int cargoIndex;
        private double cargoCapacity;
        private double cargoVolume;
        private double cargoMass;

        private int batteryIndex;
        private double batteryCharge;
        private double batteryCapacity;

        private StringBuilder rawData = new StringBuilder();

        Dictionary<string, double> ore = new Dictionary<string, double>();
        Dictionary<string, double> ingot = new Dictionary<string, double>();
        Dictionary<string, double> component = new Dictionary<string, double>();
        Dictionary<string, double> ammo = new Dictionary<string, double>();
        Dictionary<string, double> other = new Dictionary<string, double>();

        private Dictionary<string, MyDefinitionId> refillComponents = new Dictionary<string, MyDefinitionId>();
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
            Initialize();
            Load();
        }

        private void Initialize()
        {
            Surface.ContentType = ContentType.TEXT_AND_IMAGE;
            Surface.FontSize = 4f;

            Reset();
            ClearDisplays();

            Runtime.UpdateFrequency = UPDATE_FREQUENCY;
        }

        private void Reset()
        {
            ClearLog();

            state = State.ScanCargo;

            cargoIndex = 0;
            cargoCapacity = 0f;
            cargoVolume = 0f;
            cargoMass = 0f;

            batteryIndex = 0;
            batteryCapacity = 0f;
            batteryCharge = 0f;

            rawData.Clear();

            ore.Clear();
            ingot.Clear();
            component.Clear();
            ammo.Clear();
            other.Clear();
            
            // DO NOT CLEAR: refillComponents.Clear();
            queuedComponents.Clear();

            cargoBlocks.Clear();
            textPanels.Clear();
            containerMap.Clear();

            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, block => block.IsSameConstructAs(Me));
            cargoBlocks.AddRange(blocks.Where(block => block.InventoryCount > 0));

            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteryBlocks);
            GridTerminalSystem.GetBlockGroupWithName(PANELS_GROUP)?.GetBlocksOfType<IMyTextPanel>(textPanels);

            GridTerminalSystem.GetBlockGroupWithName(REFILL_ASSEMBLERS_GROUP)?.GetBlocksOfType<IMyAssembler>(assemblerBlocks);
            mainAssembler = assemblerBlocks.Where(assembler => assembler.CooperativeMode).FirstOrDefault() ?? assemblerBlocks.FirstOrDefault();
            
            var containerBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlockGroupWithName(SORTED_CONTAINERS_GROUP)?.GetBlocksOfType<IMyTerminalBlock>(containerBlocks);

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
                Debug("Sort: \"{0}\" => {1} blocks", name, containerMap[name].Count);
            }

            if (textPanels == null || textPanels.Count == 0)
            {
                Error("No text panels in group {0}", PANELS_GROUP);
                return;
            }

            foreach (var panel in textPanels)
            {
                panel.ContentType = ContentType.TEXT_AND_IMAGE;

                if (panel.CustomName.ToLower().Contains("status"))
                {
                    panel.Font = "InfoMessageBoxText";
                    panel.FontSize = STATUS_FONT_SIZE;
                }
                else if (panel.CustomName.ToLower().Contains("log"))
                {
                    panel.Font = "InfoMessageBoxText";
                    panel.FontSize = LOG_FONT_SIZE;
                }
                else
                {
                    panel.Font = "Monospace";
                    panel.FontSize = DEFAULT_FONT_SIZE;
                }
            }

            Log("Text panels: {0}", textPanels.Count);
            Log("Blocks with items: {0}", cargoBlocks.Count);
            Log("Sorted containers: {0}", containerBlocks.Count);
            Log("Battery blocks: {0}", batteryBlocks.Count);
            Log("Refill assemblers: {0}", assemblerBlocks.Count);
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
            Debug("Main {0} {1}", updateSource, argument);

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
                    ClearLog();

                    try {
                        ProcessCommand(argument);
                    }
                    catch (Exception e)
                    {
                        Error(e.ToString());
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
                        Error(e.ToString());
                    }

                    if (highestLogLogSeverity >= LogSeverity.Error)
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
                    Error("Unknown command");
                    break;
            }
        }

        private void PeriodicProcessing()
        {
            switch (state)
            {
                case State.ScanCargo:
                    for (int batch = 0; batch < CARGO_BATCH_SIZE; batch++)
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
                    for (int batch = 0; batch < BATTERY_BATCH_SIZE; batch++)
                    {
                        if (batteryIndex >= batteryBlocks.Count)
                        {
                            state = State.ScanAssemblerQueues;
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
                Debug("Block is missing");
                return;
            }

            if (!block.IsFunctional)
            {
                Warning("Broken block: " + block.CustomName);
                return;
            }

            if (block is IMyCargoContainer && !block.IsWorking)
            {
                Warning("Disabled cargo: " + block.CustomName);
                return;
            }

            Debug("[{0}]: {1}", cargoIndex, block.CustomName);

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
                        if (mainAssemblerIsUsable && !refillComponents.ContainsKey(subtypeId))
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
                                refillComponents[subtypeId] = definitionId;
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
                        Warning("Skipping item with unknown item.Type.TypeID: {0}", item.Type.TypeId);
                        continue;
                }

                if (containers != null)
                {
                    var alreadyInTheRightPlace = containers.Find(container => container.IsTheSameBlock(cargo)) != null;

                    if (!alreadyInTheRightPlace)
                    {
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
                Debug("Battery is missing");
                return;
            }

            if (!battery.IsFunctional)
            {
                Warning("Broken battery: " + battery.CustomName);
                return;
            }

            if (!battery.Enabled)
            {
                Warning("Disabled battery: " + battery.CustomName);
                return;
            }

            Debug("[{0}]: {1}", batteryIndex, battery.CustomName);

            batteryCapacity += battery.MaxStoredPower;
            batteryCharge += battery.CurrentStoredPower;
        }

        private void Report()
        {
            AppendRawData("now", FormatDateTime(DateTime.UtcNow));
            AppendRawData("status", highestLogLogSeverity.ToString());
            AppendRawData("batteryCapacity", batteryCapacity);
            AppendRawData("batteryCharge", batteryCharge);
            AppendRawData("cargoCapacity", cargoCapacity);
            AppendRawData("cargoVolume", cargoVolume);
            AppendRawData("cargoMass", cargoMass * 1e-6);

            GridTerminalSystem.GetBlockGroupWithName(PANELS_GROUP)?.GetBlocksOfType<IMyTextPanel>(textPanels);

            foreach (var panel in textPanels)
            {
                Debug("Panel {0}", panel.CustomName);
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
                return oreNames[enumValue];
            }

            return key;
        }

        private string formatIngotName(string key)
        {
            Ingot enumValue;
            if (Ingot.TryParse(key, out enumValue))
            {
                return ingotNames[enumValue];
            }

            return key;
        }

        private string formatComponentName(string key)
        {
            Component enumValue;
            if (Component.TryParse(key, out enumValue))
            {
                return componentNames[enumValue];
            }

            return key;
        }

        private delegate string ResourceNameFormatter(string key);

        private void DisplaySummary(string kind, Dictionary<string, double> summary, ResourceNameFormatter resourceNameFormatter)
        {
            var panels = FindPanels(kind).ToList();
            if (panels.Count == 0)
            {
                Debug("No text panel for {0}", kind);
                return;
            }

            var panelRowCount = (int)Math.Floor(PANEL_ROW_COUNT / panels[0].FontSize);
            var panelIndex = 0;

            foreach (var page in FormatSummary(kind, summary, resourceNameFormatter, panelRowCount))
            {
                if (panelIndex >= panels.Count)
                {
                    Warning("Not enough panels to display full {0} information", kind);
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

            if (SHOW_HEADERS)
            {
                page.AppendLine(Capitalize(kind));
                page.AppendLine(new String('-', kind.Length));
                lineCount = 2;
            }

            var maxValue = summary.Count == 0 ? 0 : summary.Values.Max();
            var maxWidth = maxValue >= 10
                ? string.Format("{0:n0}", Math.Round(maxValue / DISPLAY_PRECISION) * DISPLAY_PRECISION).Length
                : 1;

            var sortedSummary = summary.ToList().OrderBy(pair => pair.Key);
            foreach (KeyValuePair<string, double> item in sortedSummary)
            {
                AppendRawData(kind + item.Key, item.Value);

                var formattedAmount = string.Format("{0:n0}", Math.Round(item.Value / DISPLAY_PRECISION) * DISPLAY_PRECISION);
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
                Warning("No status panel");
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
                Log("No log panel");
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
                if (item.BlueprintId.TypeId.ToString() == "MyObjectBuilder_Component")
                {
                    var subtypeName = item.BlueprintId.SubtypeName;
                    var amount = queuedComponents.GetValueOrDefault(subtypeName, 0);
                    queuedComponents[subtypeName] = amount + (int)item.Amount;
                }
            }
        }
        
        private void ProduceMissing()
        {
            if (mainAssembler == null || refillComponents.Count == 0 || mainAssembler.Closed || !mainAssembler.IsFunctional)
            {
                return;
            }
            
            foreach (var kv in component)
            {
                Log(string.Format("CC S:{1} C:{0}", kv.Key, kv.Value));
            }
            Log("---");
            
            foreach (var kv in queuedComponents)
            {
                Log(string.Format("QC Q:{1} C:{0}", kv.Key, kv.Value));
            }
            Log("---");
            
            foreach (var kv in refillComponents)
            {
                var subtypeName = kv.Key;
                var stock = (int)component.GetValueOrDefault(subtypeName);
                var queued = queuedComponents.GetValueOrDefault(subtypeName);
                var missing = REFILL_MINIMUM - queued - stock;
                if (missing > 0)
                {
                    var definitionId = kv.Value;
                    Log(string.Format("RF S:{0} Q:{1} M:{2} C:{3}", stock, queued, missing, definitionId.SubtypeName));
                    //mainAssembler.AddQueueItem(definitionId, (MyFixedPoint)missing);
                }
            }
        }

        #endregion
    }
}