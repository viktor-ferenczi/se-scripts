namespace SpaceEngineersScripts.Inventory
{
    public class Config: BaseConfig
    {
        public Config()
        {
            // Defaults
            this["SingleRun"] = false;
            this["PanelsGroup"] = "Inventory Panels";
            this["SortedContainersGroup"] = "Sorted Containers";
            this["RestockAssemblersGroup"] = "Restock Assemblers";
            this["PullFromConnectedShips"] = false;
            this["MaxItemsToMove"] = 20;
            this["RestockMinimum"] = 10;
            this["RestockOverhead"] = 2;
            this["PanelRowCount"] = 17;
            this["PanelColumnCount"] = 25;
            this["DisplayPrecision"] = 1;
            this["CargoBatchSize"] = 2;
            this["BatteryBatchSize"] = 5;
            this["ShowHeaders"] = true;
            this["DefaultFontSize"] = 1f;
            this["StatusFontSize"] = 1.6f;
            this["LogFontSize"] = 0.667f;
            this["UseUpdate100"] = false;
            this["Debug"] = false;
        }
        
        public bool SingleRun { get { return (bool)this["SingleRun"]; } set { this["SingleRun"] = value; } }
        public string PanelsGroup { get { return (string)this["PanelsGroup"]; } set { this["PanelsGroup"] = value; } }
        public string SortedContainersGroup { get { return (string)this["SortedContainersGroup"]; } set { this["SortedContainersGroup"] = value; } }
        public string RestockAssemblersGroup { get { return (string)this["RestockAssemblersGroup"]; } set { this["RestockAssemblersGroup"] = value; } }
        public bool PullFromConnectedShips { get { return (bool)this["PullFromConnectedShips"]; } set { this["PullFromConnectedShips"] = value; } }
        public int MaxItemsToMove { get { return (int)this["MaxItemsToMove"]; } set { this["MaxItemsToMove"] = value; } }
        public int RestockMinimum { get { return (int)this["RestockMinimum"]; } set { this["RestockMinimum"] = value; } }
        public int RestockOverhead { get { return (int)this["RestockOverhead"]; } set { this["RestockOverhead"] = value; } }
        public int PanelRowCount { get { return (int)this["PanelRowCount"]; } set { this["PanelRowCount"] = value; } }
        public int PanelColumnCount { get { return (int)this["PanelColumnCount"]; } set { this["PanelColumnCount"] = value; } }
        public float DisplayPrecision { get { return (float)this["DisplayPrecision"]; } set { this["DisplayPrecision"] = value; } }
        public int CargoBatchSize { get { return (int)this["CargoBatchSize"]; } set { this["CargoBatchSize"] = value; } }
        public int BatteryBatchSize { get { return (int)this["BatteryBatchSize"]; } set { this["BatteryBatchSize"] = value; } }
        public bool ShowHeaders { get { return (bool)this["ShowHeaders"]; } set { this["ShowHeaders"] = value; } }
        public float DefaultFontSize { get { return (float)this["DefaultFontSize"]; } set { this["DefaultFontSize"] = value; } }
        public float StatusFontSize { get { return (float)this["StatusFontSize"]; } set { this["StatusFontSize"] = value; } }
        public float LogFontSize { get { return (float)this["LogFontSize"]; } set { this["LogFontSize"] = value; } }
        public bool UseUpdate100 { get { return (bool)this["UseUpdate100"]; } set { this["UseUpdate100"] = value; } }
        public bool Debug { get { return (bool)this["Debug"]; } set { this["Debug"] = value; } }
    }
}