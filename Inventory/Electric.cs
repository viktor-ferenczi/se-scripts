using System.Collections.Generic;
using Sandbox.ModAPI;
using IMyGridTerminalSystem = Sandbox.ModAPI.Ingame.IMyGridTerminalSystem;
using IMyProgrammableBlock = Sandbox.ModAPI.Ingame.IMyProgrammableBlock;

namespace Inventory
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

        public Electric(Config config, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts) : base(config, log, me, gts)
        {
        }

        public void Reset()
        {
            batteryBlocks.Clear();

            index = 0;
            
            previous = charge;
            
            charge = 0.0;
            capacity = 0.0;

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
                Log.Warning("Broken battery: {0}", battery.CustomName);
                return;
            }

            if (!battery.Enabled && !battery.CustomName.ToLower().Contains("emergency"))
            {
                Log.Warning("Disabled battery: {0}", battery.CustomName);
                return;
            }

            Log.Debug("[{0}]: {1}", index, battery.CustomName);

            capacity += battery.MaxStoredPower;
            charge += battery.CurrentStoredPower;
        }
    }
}