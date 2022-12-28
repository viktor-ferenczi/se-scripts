using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersScripts.Inventory
{
    public class Inventory: ProgramModule
    {
        private readonly Dictionary<string, double> oreStock = new Dictionary<string, double>();
        private readonly Dictionary<string, double> ingotStock = new Dictionary<string, double>();
        private readonly Dictionary<string, double> componentStock = new Dictionary<string, double>();
        private readonly Dictionary<string, double> ammoStock = new Dictionary<string, double>();
        private readonly Dictionary<string, double> otherStock = new Dictionary<string, double>();

        private readonly List<IMyTerminalBlock> allBlocksWithInventory = new List<IMyTerminalBlock>();
        private readonly List<IMyTerminalBlock> sortedContainerBlocks = new List<IMyTerminalBlock>();
        private readonly Dictionary<string, List<Container>> containerMap = new Dictionary<string, List<Container>>();
        private readonly List<ItemToMove> itemsToMove = new List<ItemToMove>();

        private List<Container> oreContainers;
        private List<Container> ingotContainers;
        private List<Container> componentContainers;
        private List<Container> toolContainers;
        private List<Container> ammoContainers;
        private List<Container> weaponContainers;
        private List<Container> foodContainers;
        private List<Container> gasContainers;
        private List<Container> hydrogenContainers;
        private List<Container> oxygenContainers;
        
        private int index;
        private int movedItemsCount;
        
        private double capacity;
        private double volume;
        private double mass;

        public double Capacity => capacity;
        public double Volume => volume;
        public double Mass => mass;
        
        public int CargoBlockCount => allBlocksWithInventory.Count;
        public int SortedContainerCount => sortedContainerBlocks.Count;
        public int MovedItemsCount => movedItemsCount;
        
        public IReadOnlyDictionary<string, double> OreStock => oreStock; 
        public IReadOnlyDictionary<string, double> IngotStock => ingotStock;
        public IReadOnlyDictionary<string, double> ComponentStock => componentStock;
        public IReadOnlyDictionary<string, double> AmmoStock => ammoStock;
        public IReadOnlyDictionary<string, double> OtherStock => otherStock;
        
        public Inventory(Config config, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts): base(config, log, me, gts)
        {
        }

        public void Reset()
        {
            oreStock.Clear();
            ingotStock.Clear();
            componentStock.Clear();
            ammoStock.Clear();
            otherStock.Clear();

            allBlocksWithInventory.Clear();
            sortedContainerBlocks.Clear();
            containerMap.Clear();
            itemsToMove.Clear();

            index = 0;
            movedItemsCount = 0;
            
            capacity = 0f;
            volume = 0f;
            mass = 0f;

            FindBlocks();
        }

        private void FindBlocks()
        {
            var terminalBlocks = new List<IMyTerminalBlock>();

            if (Config.PullFromConnectedShips)
            {
                Gts.GetBlocksOfType<IMyTerminalBlock>(terminalBlocks);
            }
            else
            {
                Gts.GetBlocksOfType<IMyTerminalBlock>(terminalBlocks, block => block.IsSameConstructAs(Me));
            }

            allBlocksWithInventory.AddRange(terminalBlocks.Where(block => block.InventoryCount > 0));
            
            Gts.GetBlockGroupWithName(Config.SortedContainersGroup)?.GetBlocksOfType<IMyTerminalBlock>(sortedContainerBlocks);

            var allContainers = new List<Container>();
            foreach (var containerBlock in sortedContainerBlocks)
            {
                var container = new Container(containerBlock);
                container.Register(containerMap);
                allContainers.Add(container);
            }

            foreach (var containers in containerMap.Values)
            {
                containers.Sort();
            }

            if (Config.Debug)
            {
                var names = containerMap.Keys.ToList();
                names.Sort();
                foreach (var name in names)
                {
                    Log.Debug("Sort: \"{0}\" => {1} blocks", name, containerMap[name].Count);
                }
            }
            
            oreContainers = FindContainers("ore") ?? allContainers;
            ingotContainers = FindContainers("ingot") ?? allContainers;
            componentContainers = FindContainers("component") ?? allContainers;
            toolContainers = FindContainers("tool") ?? allContainers;
            ammoContainers = FindContainers("ammo") ?? FindContainers("weapon") ?? toolContainers;
            weaponContainers = FindContainers("weapon") ?? FindContainers("ammo") ?? toolContainers;
            foodContainers = FindContainers("food") ?? toolContainers;
            gasContainers = FindContainers("gas") ?? toolContainers;
            hydrogenContainers = FindContainers("hydrogen") ?? gasContainers;
            oxygenContainers = FindContainers("oxygen") ?? gasContainers;

            if (Config.Debug)
            {
                Log.Debug("OreContainers: {0}", oreContainers.Count);
                Log.Debug("IngotContainers: {0}", ingotContainers.Count);
                Log.Debug("ComponentContainers: {0}", componentContainers.Count);
                Log.Debug("ToolContainers: {0}", toolContainers.Count);
                Log.Debug("AmmoContainers: {0}", ammoContainers.Count);
                Log.Debug("WeaponContainers: {0}", weaponContainers.Count);
                Log.Debug("FoodContainers: {0}", foodContainers.Count);
                Log.Debug("GasContainers: {0}", gasContainers.Count);
                Log.Debug("HydrogenContainers: {0}", hydrogenContainers.Count);
                Log.Debug("OxygenContainers: {0}", oxygenContainers.Count);
            }
        }

        private List<Container> FindContainers(string name)
        {
            return containerMap.GetValueOrDefault(name);
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
            var block = allBlocksWithInventory[index];
            if (block == null)
            {
                Log.Debug("Block is missing");
                return;
            }

            if (!block.IsFunctional)
            {
                Log.Warning("Broken block: {0}", block.CustomName);
                return;
            }

            if (block is IMyCargoContainer && !block.IsWorking)
            {
                Log.Warning("Disabled cargo: {0}", block.CustomName);
                return;
            }

            Log.Debug("[{0}]: {1}", index, block.CustomName);

            AggregateBlockCapacity(block);
            AggregateBlockContents(block);
        }

        private void AggregateBlockCapacity(IMyTerminalBlock block)
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

        private void AggregateBlockContents(IMyTerminalBlock cargo)
        {
            for (int inventoryIndex = 0; inventoryIndex < cargo.InventoryCount; inventoryIndex++)
            {
                AggregateCargoInventory(cargo, inventoryIndex);
            }
        }

        private void AggregateCargoInventory(IMyTerminalBlock cargo, int inventoryIndex)
        {
            if ((cargo.CustomName ?? "").ToLower().Contains("ignore"))
            {
                return;
            }
            
            var allowAmmo = !(cargo is IMyUserControllableGun);
            var allowOre = !(cargo is IMyRefinery || cargo is IMyGasGenerator);
            var allowIngot = !(cargo is IMyReactor || cargo is IMyAssembler);
            // FIXME: Detect the safe zone by its interface
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

            for (int itemIndex = items.Count - 1; itemIndex >= 0; itemIndex--)
            {
                var item = items[itemIndex];
                if (item.Amount <= 0)
                {
                    continue;
                }

                var typeId = item.Type.TypeId;
                var subtypeId = item.Type.SubtypeId;
                var lowercaseSubTypeName = subtypeId.ToLower();

                Dictionary<string, double> summary;
                List<Container> containers = null;

                switch (typeId)
                {
                    case "MyObjectBuilder_Ore":
                        summary = oreStock;
                        if (allowOre)
                        {
                            containers = oreContainers;
                        }

                        break;
                    case "MyObjectBuilder_Ingot":
                        summary = ingotStock;
                        if (allowIngot)
                        {
                            containers = ingotContainers;
                        }

                        break;
                    case "MyObjectBuilder_Component":
                        summary = componentStock;
                        if (allowComponent)
                        {
                            containers = componentContainers;
                        }
                        break;
                    case "MyObjectBuilder_AmmoMagazine":
                        summary = ammoStock;
                        if (allowAmmo)
                        {
                            containers = ammoContainers;
                        }

                        break;
                    case "MyObjectBuilder_PhysicalGunObject":
                        summary = otherStock;
                        if (lowercaseSubTypeName.Contains("weld") ||
                            lowercaseSubTypeName.Contains("grind") ||
                            lowercaseSubTypeName.Contains("drill"))
                        {
                            containers = toolContainers;
                        }
                        else
                        {
                            containers = weaponContainers;
                        }

                        break;
                    case "MyObjectBuilder_Datapad":
                        summary = otherStock;
                        containers = toolContainers;
                        break;
                    case "MyObjectBuilder_ConsumableItem":
                        summary = otherStock;
                        containers = foodContainers;
                        break;
                    case "MyObjectBuilder_GasContainerObject":
                        summary = otherStock;
                        containers = hydrogenContainers;
                        break;
                    case "MyObjectBuilder_OxygenContainerObject":
                        summary = otherStock;
                        containers = oxygenContainers;
                        break;
                    case "MyObjectBuilder_PhysicalObject":
                        summary = otherStock;
                        containers = toolContainers;
                        break;
                    default:
                        Log.Warning("Skipping item with unknown item.Type.TypeID: {0}", typeId);
                        continue;
                }

                if (containers != null && containers.Count != 0 && itemsToMove.Count < Config.MaxItemsToMove)
                {
                    var isAtTheWrongPlace = containers.Find(container => container.IsTheSameBlock(cargo)) == null;
                    if (isAtTheWrongPlace)
                    {
                        itemsToMove.Add(new ItemToMove
                        {
                            Inventory = blockInventory,
                            ItemIndex = itemIndex,
                            ItemType = typeId,
                            ItemSubtype = subtypeId,
                            TargetContainers = containers,
                        });
                    }
                }

                Aggregate(summary, subtypeId, (double)item.Amount);
            }
        }

        private static void Aggregate(IDictionary<string, double> summary, string key, double amount)
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

        public void Display(TextPanels panels)
        {
            DisplaySummary(panels, Category.Ore, oreStock, Naming.FormatOreName);
            DisplaySummary(panels, Category.Ingot, ingotStock, Naming.FormatIngotName);
            DisplaySummary(panels, Category.Component, componentStock, Naming.FormatComponentName);
            DisplaySummary(panels, Category.Ammo, ammoStock);
            DisplaySummary(panels, Category.Other, otherStock);
        }

        private delegate string ResourceNameFormatter(string key);

        private void DisplaySummary(TextPanels allPanels, Category category, Dictionary<string, double> summary, ResourceNameFormatter resourceNameFormatter = null)
        {
            var panels = allPanels.Find(category).ToList();
            if (panels.Count == 0)
            {
                Log.Debug("No text panel for {0}", category);
                return;
            }

            var panelRowCount = (int)Math.Floor(Config.PanelRowCount / panels[0].FontSize);
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
            if (Config.ShowHeaders)
            {
                page.AppendLine(categoryName);
                page.AppendLine(new String('-', categoryName.Length));
                lineCount = 2;
            }

            var maxValue = summary.Count == 0 ? 0 : summary.Values.Max();
            var maxWidth = maxValue >= 10 ? $"{Math.Round(maxValue / Config.DisplayPrecision) * Config.DisplayPrecision:n0}".Length : 1;

            var sortedSummary = summary.ToList().OrderBy(pair => pair.Key);
            foreach (KeyValuePair<string, double> item in sortedSummary)
            {
                var formattedAmount = $"{Math.Round(item.Value / Config.DisplayPrecision) * Config.DisplayPrecision:n0}";
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
        
        public void MoveItems()
        {
            Log.Debug("Items to move: {0}", itemsToMove.Count);
            
            // Reverse order, so moving the items don't change the index of the further items
            // (ideally, unless some concurrent moves happen)
            for (int i = itemsToMove.Count - 1; i >= 0; i--)
            {
                var itemToMove = itemsToMove[i];
                
                // Verify that it is still the same item type which needs to be moved
                var item = itemToMove.Inventory.GetItemAt(itemToMove.ItemIndex);
                if (!item.HasValue || item.Value.Type.TypeId != itemToMove.ItemType || item.Value.Type.SubtypeId != itemToMove.ItemSubtype)
                {
                    Log.Debug("Could not find item [{0}] to move: {1}", itemToMove.ItemIndex, itemToMove.ItemType);
                    continue;
                }
                    
                // Verify successful move by the change in source inventory mass
                var originalMass = itemToMove.Inventory.CurrentMass;
                foreach (var container in itemToMove.TargetContainers)
                {
                    if (container.CollectItem(itemToMove.Inventory, itemToMove.ItemIndex))
                    {
                        // FIXME: Fragile
                        if (itemToMove.Inventory.CurrentMass != originalMass)
                        {
                            movedItemsCount++;
                            break;
                        }
                    }
                }
            }
        }
    }
}