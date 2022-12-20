using System.Collections.Generic;
using Sandbox.ModAPI;
using IMyGridTerminalSystem = Sandbox.ModAPI.Ingame.IMyGridTerminalSystem;
using IMyProgrammableBlock = Sandbox.ModAPI.Ingame.IMyProgrammableBlock;

namespace SpaceEngineersScripts.Inventory
{
    public class Electric : ProgramModule
    {
        private readonly List<IMyBatteryBlock> batteryBlocks = new List<IMyBatteryBlock>();
        
        private int index;
        private double charge;
        private double capacity;
        private double previous;

        public int BatteryBlockCount => batteryBlocks.Count;
        public double Capacity => capacity;
        public double Charge => charge;
        public double Previous => previous;

        public Electric(Cfg cfg, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts) : base(cfg, log, me, gts)
        {
        }

        public void Reset()
        {
            batteryBlocks.Clear();

            index = 0;
            
            previous = charge;
            
            charge = 0.0;
            capacity = 0.0;

            FindBlocks();
        }

        private void FindBlocks()
        {
            Gts.GetBlocksOfType(batteryBlocks);
        }

        public bool Done => index >= BatteryBlockCount;

        public void Scan()
        {
            if (Done)
            {
                return;
            }

            ScanBattery();
            index++;
        }

        private void ScanBattery()
        {
            var battery = batteryBlocks[index];
            if (battery == null)
            {
                Log.Debug("Battery is missing");
                return;
            }

            if (!battery.IsFunctional)
            {
                Log.Warning("Broken battery: " + battery.CustomName);
                return;
            }

            if (!battery.Enabled)
            {
                Log.Warning("Disabled battery: " + battery.CustomName);
                return;
            }

            Log.Debug("[{0}]: {1}", index, battery.CustomName);

            capacity += battery.MaxStoredPower;
            charge += battery.CurrentStoredPower;
        }
    }
}