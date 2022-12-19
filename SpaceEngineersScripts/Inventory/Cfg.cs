using Sandbox.ModAPI.Ingame;

namespace SpaceEngineersScripts.Inventory
{
    public class Cfg
    {
        public string PanelsGroup = "Inventory Panels";
        public string SortedContainersGroup = "Sorted Containers";
        public string RestockAssemblersGroup = "Restock Assemblers";
        public bool PullFromConnectedShips = false;        
        public int RestockMinimum = 10;
        public int RestockOverhead = 2;
        public int PanelRowCount = 17;
        public int PanelColumnCount = 25;
        public double DisplayPrecision = 1;
        public int CargoBatchSize = 3;
        public int BatteryBatchSize = 10;
        public bool ShowHeaders = true;
        public float DefaultFontSize = 1f;
        public float StatusFontSize = 1.6f;
        public float LogFontSize = 0.667f;
        public UpdateFrequency UpdateFrequency = UpdateFrequency.Update10;
        public bool Debug = false;
    }
}