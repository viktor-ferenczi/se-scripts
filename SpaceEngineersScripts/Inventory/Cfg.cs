using Sandbox.ModAPI.Ingame;

namespace SpaceEngineersScripts.Inventory
{
    public static class Cfg
    {
        public const string PanelsGroup = "Inventory Panels";
        public const string SortedContainersGroup = "Sorted Containers";
        public const string RestockAssemblersGroup = "Restock Assemblers";
        public const int RestockMinimum = 10;
        public const int RestockOverhead = RestockMinimum / 5;
        public const int PanelRowCount = 17;
        public const int PanelColumnCount = 25;
        public const double DisplayPrecision = 1;
        public const int CargoBatchSize = 3;
        public const int BatteryBatchSize = 10;
        public const bool ShowHeaders = true;
        public const float DefaultFontSize = 1f;
        public const float StatusFontSize = 1.6f;
        public const float LogFontSize = 0.667f;
        public const UpdateFrequency UpdateFrequency = Sandbox.ModAPI.Ingame.UpdateFrequency.Update10;
    }
}