using System.Collections.Generic;

namespace SpaceEngineersScripts.Inventory
{
    public class Config : BaseConfig
    {
        protected override void AddOptions()
        {
            // Defaults
            Defaults["Debug"] = false;
            Defaults["WrapLog"] = false;
            Defaults["SingleRun"] = false;
            Defaults["EnableItemSorting"] = true;
            Defaults["EnableComponentRestocking"] = true;
            Defaults["DisplayPanelsGroup"] = "Inventory Panels";
            Defaults["SortedContainersGroup"] = "Sorted Containers";
            Defaults["RestockAssemblersGroup"] = "Restock Assemblers";
            Defaults["PullFromConnectedShips"] = false;
            Defaults["MaxItemsToMove"] = 20;
            Defaults["PanelRowCount"] = 17;
            Defaults["PanelColumnCount"] = 25;
            Defaults["DisplayPrecision"] = 1.0;
            Defaults["CargoBatchSize"] = 2;
            Defaults["BatteryBatchSize"] = 5;
            Defaults["ShowHeaders"] = true;
            Defaults["DefaultFontSize"] = 1f;
            Defaults["StatusFontSize"] = 1.6f;
            Defaults["LogFontSize"] = 0.8f;
            Defaults["UseUpdate100"] = false;

            // Restock components
            Defaults["RestockBulletproofGlass"] = 10;
            Defaults["RestockComputer"] = 10;
            Defaults["RestockConstruction"] = 10;
            Defaults["RestockDetector"] = 1;
            Defaults["RestockDisplay"] = 10;
            Defaults["RestockExplosives"] = 0;
            Defaults["RestockGirder"] = 100;
            Defaults["RestockGravityGenerator"] = 0;
            Defaults["RestockInteriorPlate"] = 100;
            Defaults["RestockLargeTube"] = 10;
            Defaults["RestockMedical"] = 0;
            Defaults["RestockMetalGrid"] = 10;
            Defaults["RestockMotor"] = 100;
            Defaults["RestockPowerCell"] = 10;
            Defaults["RestockRadioCommunication"] = 1;
            Defaults["RestockReactor"] = 0;
            Defaults["RestockSmallTube"] = 50;
            Defaults["RestockSolarCell"] = 10;
            Defaults["RestockSteelPlate"] = 100;
            Defaults["RestockSuperconductor"] = 0;
            Defaults["RestockThrust"] = 0;
        }

        public bool Debug => (bool)this["Debug"];
        public bool WrapLog => (bool)this["WrapLog"];
        public bool SingleRun => (bool)this["SingleRun"];
        public bool EnableItemSorting => (bool)this["EnableItemSorting"];
        public bool EnableComponentRestocking => (bool)this["EnableComponentRestocking"];
        public string DisplayPanelsGroup => (string)this["DisplayPanelsGroup"];
        public string SortedContainersGroup => (string)this["SortedContainersGroup"];
        public string RestockAssemblersGroup => (string)this["RestockAssemblersGroup"];
        public bool PullFromConnectedShips => (bool)this["PullFromConnectedShips"];
        public int MaxItemsToMove => (int)this["MaxItemsToMove"];
        public int PanelRowCount => (int)this["PanelRowCount"];
        public int PanelColumnCount => (int)this["PanelColumnCount"];
        public double DisplayPrecision => (double)this["DisplayPrecision"];
        public int CargoBatchSize => (int)this["CargoBatchSize"];
        public int BatteryBatchSize => (int)this["BatteryBatchSize"];
        public bool ShowHeaders => (bool)this["ShowHeaders"];
        public float DefaultFontSize => (float)this["DefaultFontSize"];
        public float StatusFontSize => (float)this["StatusFontSize"];
        public float LogFontSize => (float)this["LogFontSize"];
        public bool UseUpdate100 => (bool)this["UseUpdate100"];

        public IReadOnlyDictionary<Component, int> GetRestockTargetAmounts()
        {
            var d = new Dictionary<Component, int>();
            foreach (var p in this)
            {
                if (p.Key.StartsWith("Restock"))
                {
                    Component c;
                    if (Naming.TryParseComponent(p.Key.Substring(7), out c))
                    {
                        d[c] = (int)p.Value;
                    }
                }
            }

            return d;
        }
    }
}