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
        private readonly Dictionary<string, double> ore = new Dictionary<string, double>();
        private readonly Dictionary<string, double> ingot = new Dictionary<string, double>();
        private readonly Dictionary<string, double> component = new Dictionary<string, double>();
        private readonly Dictionary<string, double> ammo = new Dictionary<string, double>();
        private readonly Dictionary<string, double> other = new Dictionary<string, double>();

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
        private double capacity;
        private double volume;
        private double mass;

        public int CargoBlockCount => allBlocksWithInventory.Count;
        public double Capacity => capacity;
        public double Volume => volume;
        public double Mass => mass;
        public int ItemsToMoveCount => itemsToMove.Count;
        public int SortedContainerCount => sortedContainerBlocks.Count;
        
        public Inventory(Config config, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts): base(config, log, me, gts)
        {
        }

        public void Reset()
        {
            ore.Clear();
            ingot.Clear();
            component.Clear();
            ammo.Clear();
            other.Clear();

            allBlocksWithInventory.Clear();
            sortedContainerBlocks.Clear();
            containerMap.Clear();
            itemsToMove.Clear();

            index = 0;
            
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
            
            foreach (var containerBlock in sortedContainerBlocks)
            {
                var container = new Container(containerBlock);
                container.Register(containerMap);
            }

            foreach (var containers in containerMap.Values)
            {
                containers.Sort();
            }
            
            oreContainers = FindContainers("ore") ?? new List<Container>();
            ingotContainers = FindContainers("ingot") ?? new List<Container>();
            componentContainers = FindContainers("component") ?? new List<Container>();
            toolContainers = FindContainers("tool") ?? FindContainers("");
            ammoContainers = FindContainers("ammo") ?? FindContainers("weapon") ?? toolContainers;
            weaponContainers = FindContainers("weapon") ?? FindContainers("ammo") ?? toolContainers;
            foodContainers = FindContainers("food") ?? toolContainers;
            gasContainers = FindContainers("gas") ?? FindContainers("");
            hydrogenContainers = FindContainers("hydrogen") ?? gasContainers;
            oxygenContainers = FindContainers("oxygen") ?? gasContainers;

            var names = containerMap.Keys.ToList();
            names.Sort();
            foreach (var name in names)
            {
                Log.Debug("Sort: \"{0}\" => {1} blocks", name, containerMap[name].Count);
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
                Log.Warning("Broken block: " + block.CustomName);
                return;
            }

            if (block is IMyCargoContainer && !block.IsWorking)
            {
                Log.Warning("Disabled cargo: " + block.CustomName);
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

            List<Container> containers = null;

            for (int itemIndex = items.Count - 1; itemIndex >= 0; itemIndex--)
            {
                var item = items[itemIndex];
                if (item.Amount <= 0)
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
                            containers = oreContainers;
                        }

                        break;
                    case "MyObjectBuilder_Ingot":
                        summary = ingot;
                        if (allowIngot)
                        {
                            containers = ingotContainers;
                        }

                        break;
                    case "MyObjectBuilder_Component":
                        summary = component;
                        if (allowComponent)
                        {
                            containers = componentContainers;
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
                            containers = ammoContainers;
                        }

                        break;
                    case "MyObjectBuilder_PhysicalGunObject":
                        summary = other;
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
                        summary = other;
                        containers = toolContainers;
                        break;
                    case "MyObjectBuilder_ConsumableItem":
                        summary = other;
                        containers = foodContainers;
                        break;
                    case "MyObjectBuilder_GasContainerObject":
                        summary = other;
                        containers = hydrogenContainers;
                        break;
                    case "MyObjectBuilder_OxygenContainerObject":
                        summary = other;
                        containers = oxygenContainers;
                        break;
                    case "MyObjectBuilder_PhysicalObject":
                        summary = other;
                        containers = toolContainers;
                        break;
                    default:
                        Log.Warning("Skipping item with unknown item.Type.TypeID: {0}", item.Type.TypeId);
                        continue;
                }

                if (containers != null && containers.Count != 0 && itemsToMove.Count < Config.MaxItemsToMove)
                {
                    var itemIsAtTheWrongPlace = containers.Find(container => container.IsTheSameBlock(cargo)) == null;
                    if (itemIsAtTheWrongPlace)
                    {
                        itemsToMove.Add(new ItemToMove
                        {
                            Inventory = null,
                            ItemIndex = itemIndex,
                            ItemType = item.Type.TypeId,
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

        public void Display(TextPanels panels, RawData data)
        {
            DisplaySummary(panels, data, Category.Ore, ore, FormatOreName);
            DisplaySummary(panels, data, Category.Ingot, ingot, FormatIngotName);
            DisplaySummary(panels, data, Category.Component, component, FormatComponentName);
            DisplaySummary(panels, data, Category.Ammo, ammo);
            DisplaySummary(panels, data, Category.Other, other);
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

        private void DisplaySummary(TextPanels allPanels, RawData data, Category category, Dictionary<string, double> summary, ResourceNameFormatter resourceNameFormatter = null)
        {
            var panels = allPanels.Find(category).ToList();
            if (panels.Count == 0)
            {
                Log.Debug("No text panel for {0}", category);
                return;
            }

            var panelRowCount = (int)Math.Floor(Config.PanelRowCount / panels[0].FontSize);
            var panelIndex = 0;

            foreach (var page in FormatSummary(data, category, summary, resourceNameFormatter, panelRowCount))
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
            RawData rawData,
            Category category,
            Dictionary<string, double> summary,
            ResourceNameFormatter resourceNameFormatter,
            int panelRowCount)
        {
            var page = new StringBuilder();
            var lineCount = 0;

            var categoryName = category.ToString();
            var categoryNameLc = categoryName.ToLower();
            
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
                rawData.Append(categoryNameLc + item.Key, item.Value);

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
            // Reverse order, so moving the items don't change the index of the further items
            // (ideally, unless some concurrent moves happen)
            for (int i = itemsToMove.Count - 1; i >= 0; i--)
            {
                var itemToMove = itemsToMove[i];
                
                // Verify that it is still the same item type which needs to be moved
                var item = itemToMove.Inventory.GetItemAt(itemToMove.ItemIndex);
                if (!item.HasValue || item.Value.Type.TypeId != itemToMove.ItemType)
                {
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
                            break;
                        }
                    }
                }
            }
        }
    }
}