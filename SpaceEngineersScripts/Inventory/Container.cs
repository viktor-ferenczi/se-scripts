using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersScripts.Inventory
{
    public class Container : IComparable<Container>
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

        public object Name
        {
            get { return container.CustomName; }
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
}