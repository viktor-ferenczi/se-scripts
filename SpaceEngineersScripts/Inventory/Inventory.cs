using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersScripts.Inventory
{
    public class Inventory
    {
        private readonly Cfg cfg;
        private readonly Log log;
        private readonly IMyProgrammableBlock me;
        private readonly IMyGridTerminalSystem gts;
        private readonly TextPanels textPanels;
        private readonly RawData rawData;

        private readonly Dictionary<string, double> ore = new Dictionary<string, double>();
        private readonly Dictionary<string, double> ingot = new Dictionary<string, double>();
        private readonly Dictionary<string, double> component = new Dictionary<string, double>();
        private readonly Dictionary<string, double> ammo = new Dictionary<string, double>();
        private readonly Dictionary<string, double> other = new Dictionary<string, double>();

        private readonly List<IMyTerminalBlock> cargoBlocks = new List<IMyTerminalBlock>();
        private readonly Dictionary<string, List<Container>> containerMap = new Dictionary<string, List<Container>>();

        private int cargoIndex;
        private double cargoCapacity;
        private double cargoVolume;
        private double cargoMass;
        // FIXME: private bool attemptedToMoveCargo;

        public int CargoBlockCount => cargoBlocks.Count;
        public double CargoCapacity => cargoCapacity;
        public double CargoVolume => cargoVolume;
        public double CargoMass => cargoMass;

        public Inventory(Cfg cfg, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts, TextPanels textPanels, RawData rawData)
        {
            this.cfg = cfg;
            this.log = log;
            this.me = me;
            this.gts = gts;
            this.textPanels = textPanels;
            this.rawData = rawData;
        }

        public void Reset()
        {
            ore.Clear();
            ingot.Clear();
            component.Clear();
            ammo.Clear();
            other.Clear();

            cargoBlocks.Clear();
            containerMap.Clear();
            
            FindBlocks();

            cargoIndex = 0;
            cargoCapacity = 0f;
            cargoVolume = 0f;
            cargoMass = 0f;
            // FIXME: attemptedToMoveCargo = false;
        }

        private void FindBlocks()
        {
            var blocks = new List<IMyTerminalBlock>();

            if (cfg.PullFromConnectedShips)
            {
                gts.GetBlocksOfType<IMyTerminalBlock>(blocks);
            }
            else
            {
                gts.GetBlocksOfType<IMyTerminalBlock>(blocks, block => block.IsSameConstructAs(me));
            }

            cargoBlocks.AddRange(blocks.Where(block => block.InventoryCount > 0));
            
            var containerBlocks = new List<IMyTerminalBlock>();
            gts.GetBlockGroupWithName(cfg.SortedContainersGroup)?.GetBlocksOfType<IMyTerminalBlock>(containerBlocks);

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
        }

        public bool Done => cargoIndex >= cargoBlocks.Count; 

        public void Scan()
        {
            if (Done)
            {
                return;
            }
            
            ScanBlock();
            cargoIndex++;
        }

        private void ScanBlock()
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

            // FIXME: Factor out
            //bool mainAssemblerIsUsable = mainAssembler != null && !mainAssembler.Closed && mainAssembler.IsFunctional;

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

                        // FIXME: Factor out
                        /*
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
                        */

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
                        // FIXME: Factor out
                        /*
                        attemptedToMoveCargo = true;
                        foreach (var container in containers)
                        {
                            var mass = cargo.Mass;
                            if (container.CollectItem(blockInventory, itemIndex))
                            {
                                // FIXME: Fragile
                                if (cargo.Mass != mass)
                                {
                                    break;
                                }
                            }
                        }
                        */
                    }
                }

                Accumulate(summary, subtypeId, (double)item.Amount);
            }
        }

        private List<Container> FindContainers(string name)
        {
            return containerMap.GetValueOrDefault(name);
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

        public void Display()
        {
            DisplaySummary("ore", ore, formatOreName);
            DisplaySummary("ingot", ingot, formatIngotName);
            DisplaySummary("component", component, formatComponentName);
            DisplaySummary("ammo", ammo, null);
            DisplaySummary("other", other, null);
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
            var panels = textPanels.Find(kind).ToList();
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
                page.AppendLine(Util.Capitalize(kind));
                page.AppendLine(new String('-', kind.Length));
                lineCount = 2;
            }

            var maxValue = summary.Count == 0 ? 0 : summary.Values.Max();
            var maxWidth = maxValue >= 10 ? $"{Math.Round(maxValue / cfg.DisplayPrecision) * cfg.DisplayPrecision:n0}".Length : 1;

            var sortedSummary = summary.ToList().OrderBy(pair => pair.Key);
            foreach (KeyValuePair<string, double> item in sortedSummary)
            {
                rawData.Append(kind + item.Key, item.Value);

                var formattedAmount = $"{Math.Round(item.Value / cfg.DisplayPrecision) * cfg.DisplayPrecision:n0}";
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
    }
}