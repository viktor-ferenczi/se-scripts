using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersScripts.Inventory
{
    public class Inventory: ProgramModule
    {
        private readonly TextPanels textPanels;
        private readonly RawData rawData;

        private readonly Dictionary<string, double> ore = new Dictionary<string, double>();
        private readonly Dictionary<string, double> ingot = new Dictionary<string, double>();
        private readonly Dictionary<string, double> component = new Dictionary<string, double>();
        private readonly Dictionary<string, double> ammo = new Dictionary<string, double>();
        private readonly Dictionary<string, double> other = new Dictionary<string, double>();

        private readonly List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        private readonly Dictionary<string, List<Container>> containerMap = new Dictionary<string, List<Container>>();

        private int index;
        private double capacity;
        private double volume;
        private double mass;
        // FIXME: private bool attemptedToMoveCargo;

        public int CargoBlockCount => blocks.Count;
        public double Capacity => capacity;
        public double Volume => volume;
        public double Mass => mass;
        
        public Inventory(Cfg cfg, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts, TextPanels textPanels, RawData rawData): base(cfg, log, me, gts)
        {
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

            blocks.Clear();
            containerMap.Clear();
            
            FindBlocks();

            index = 0;
            
            capacity = 0f;
            volume = 0f;
            mass = 0f;
            // FIXME: attemptedToMoveCargo = false;
        }

        private void FindBlocks()
        {
            var blocks = new List<IMyTerminalBlock>();

            if (Cfg.PullFromConnectedShips)
            {
                Gts.GetBlocksOfType<IMyTerminalBlock>(blocks);
            }
            else
            {
                Gts.GetBlocksOfType<IMyTerminalBlock>(blocks, block => block.IsSameConstructAs(Me));
            }

            this.blocks.AddRange(blocks.Where(block => block.InventoryCount > 0));
            
            var containerBlocks = new List<IMyTerminalBlock>();
            Gts.GetBlockGroupWithName(Cfg.SortedContainersGroup)?.GetBlocksOfType<IMyTerminalBlock>(containerBlocks);

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
                Log.Debug("Sort: \"{0}\" => {1} blocks", name, containerMap[name].Count);
            }
        }

        public bool Done => index >= CargoBlockCount; 
        
        public void Scan()
        {
            if (Done)
            {
                return;
            }
            
            ScanBlock();
            index++;
        }

        private void ScanBlock()
        {
            var block = blocks[index];
            if (block == null)
            {
                Log.Debug("Block is missing");
                return;
            }

            if (!block.IsFunctional)
            {
                Log.Warning("Broken block: " + block.CustomName);
                return;
            }

            if (block is IMyCargoContainer && !block.IsWorking)
            {
                Log.Warning("Disabled cargo: " + block.CustomName);
                return;
            }

            Log.Debug("[{0}]: {1}", index, block.CustomName);

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

            capacity += blockInventory.MaxVolume.RawValue;
            volume += blockInventory.CurrentVolume.RawValue;
            mass += blockInventory.CurrentMass.RawValue;
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
                            containers = FindContainers("ore") ?? new List<Container>();
                        }

                        break;
                    case "MyObjectBuilder_Ingot":
                        summary = ingot;
                        if (allowIngot)
                        {
                            containers = FindContainers("ingot") ?? new List<Container>();
                        }

                        break;
                    case "MyObjectBuilder_Component":
                        summary = component;
                        if (allowComponent)
                        {
                            containers = FindContainers("component") ?? new List<Container>();
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
                        Log.Warning("Skipping item with unknown item.Type.TypeID: {0}", item.Type.TypeId);
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
            DisplaySummary(Category.Ore, ore, FormatOreName);
            DisplaySummary(Category.Ingot, ingot, FormatIngotName);
            DisplaySummary(Category.Component, component, FormatComponentName);
            DisplaySummary(Category.Ammo, ammo);
            DisplaySummary(Category.Other, other);
        }
        
        private static string FormatOreName(string key)
        {
            Ore enumValue;
            return Enum.TryParse(key, out enumValue) ? Naming.OreNames[enumValue] : key;
        }

        private static string FormatIngotName(string key)
        {
            Ingot enumValue;
            return Enum.TryParse(key, out enumValue) ? Naming.IngotNames[enumValue] : key;
        }

        private static string FormatComponentName(string key)
        {
            Component enumValue;
            return Enum.TryParse(key, out enumValue) ? Naming.ComponentNames[enumValue] : key;
        }

        private delegate string ResourceNameFormatter(string key);

        private void DisplaySummary(Category category, Dictionary<string, double> summary, ResourceNameFormatter resourceNameFormatter = null)
        {
            var panels = textPanels.Find(category).ToList();
            if (panels.Count == 0)
            {
                Log.Debug("No text panel for {0}", category);
                return;
            }

            var panelRowCount = (int)Math.Floor(Cfg.PanelRowCount / panels[0].FontSize);
            var panelIndex = 0;

            foreach (var page in FormatSummary(category, summary, resourceNameFormatter, panelRowCount))
            {
                if (panelIndex >= panels.Count)
                {
                    Log.Warning("Not enough panels to display full {0} information", category);
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
            Category category,
            Dictionary<string, double> summary,
            ResourceNameFormatter resourceNameFormatter,
            int panelRowCount)
        {
            var page = new StringBuilder();
            var lineCount = 0;

            var categoryName = category.ToString();
            var categoryNameLc = categoryName.ToLower();
            
            if (Cfg.ShowHeaders)
            {
                page.AppendLine(categoryName);
                page.AppendLine(new String('-', categoryName.Length));
                lineCount = 2;
            }

            var maxValue = summary.Count == 0 ? 0 : summary.Values.Max();
            var maxWidth = maxValue >= 10 ? $"{Math.Round(maxValue / Cfg.DisplayPrecision) * Cfg.DisplayPrecision:n0}".Length : 1;

            var sortedSummary = summary.ToList().OrderBy(pair => pair.Key);
            foreach (KeyValuePair<string, double> item in sortedSummary)
            {
                rawData.Append(categoryNameLc + item.Key, item.Value);

                var formattedAmount = $"{Math.Round(item.Value / Cfg.DisplayPrecision) * Cfg.DisplayPrecision:n0}";
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