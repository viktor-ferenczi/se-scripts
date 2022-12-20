using System;
using System.Collections.Generic;

namespace SpaceEngineersScripts.Inventory
{
    public enum Component
    {
        BulletproofGlass,
        Computer,
        Construction,
        Detector,
        Display,
        Explosives,
        Girder,
        GravityGenerator,
        InteriorPlate,
        LargeTube,
        Medical,
        MetalGrid,
        Motor,
        PowerCell,
        RadioCommunication,
        Reactor,
        SmallTube,
        SolarCell,
        SteelPlate,
        Superconductor,
        Thrust,
        ZoneChip,
    }

    public enum Ingot
    {
        Cobalt,
        Gold,
        Iron,
        Magnesium,
        Nickel,
        Platinum,
        Silicon,
        Silver,
        Stone,
        Uranium
    }

    public enum Ore
    {
        Cobalt,
        Gold,
        Ice,
        Iron,
        Magnesium,
        Nickel,
        Platinum,
        Scrap,
        Silicon,
        Silver,
        Stone,
        Uranium
    }

    public static class Naming
    {
        public static readonly Dictionary<Component, string> ComponentNames = new Dictionary<Component, string>()
        {
            [Component.BulletproofGlass] = "Bulletproof Glass",
            [Component.Computer] = "Computer",
            [Component.Construction] = "Construction Comp",
            [Component.Detector] = "Detector Comp",
            [Component.Display] = "Display",
            [Component.Explosives] = "Explosive",
            [Component.Girder] = "Girder",
            [Component.GravityGenerator] = "Gravity Gen",
            [Component.InteriorPlate] = "Interior Plate",
            [Component.LargeTube] = "Large Steel Tube",
            [Component.Medical] = "Medical Comp",
            [Component.MetalGrid] = "Metal Grid",
            [Component.Motor] = "Motor",
            [Component.PowerCell] = "Power Cell",
            [Component.RadioCommunication] = "Radio Comp",
            [Component.Reactor] = "Reactor Comp",
            [Component.SmallTube] = "Small Steel Tube",
            [Component.SolarCell] = "Solar Cell",
            [Component.SteelPlate] = "Steel Plate",
            [Component.Superconductor] = "Superconductor",
            [Component.Thrust] = "Thruster Comp",
            [Component.ZoneChip] = "Zone Chip",
        };

        public static readonly Dictionary<Ingot, string> IngotNames = new Dictionary<Ingot, string>()
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

        public static readonly Dictionary<Ore, string> OreNames = new Dictionary<Ore, string>()
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

        public static string FormatOreName(string key)
        {
            Ore ore;
            return Enum.TryParse(key, out ore) ? OreNames[ore] : key;
        }

        public static string FormatIngotName(string key)
        {
            Ingot ingot;
            return Enum.TryParse(key, out ingot) ? IngotNames[ingot] : key;
        }

        public static string FormatComponentName(string key)
        {
            if (key.EndsWith("Component"))
            {
                key = key.Substring(0, key.Length - 9);
            }

            Component component;
            return Enum.TryParse(key, out component) ? ComponentNames[component] : key;
        }
        
        public static bool TryParseComponent(string s, out Component c)
        {
            if (s.EndsWith("Component"))
            {
                s = s.Substring(0, s.Length - 9);
            }
            
            return Enum.TryParse(s, out c);
        }
    }
}