using System.Collections.Generic;

namespace SpaceEngineersScripts.Inventory
{
    public enum Component
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
    }
}