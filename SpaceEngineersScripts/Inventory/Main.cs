using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace SpaceEngineersScripts.Inventory
{
    public class Program: MyGridProgram
    {
        private readonly Cfg cfg;
        private readonly Log log;
        private readonly Inventory inventory;
        private readonly TextPanels textPanels;
        private readonly RawData rawData = new RawData();
        
        private IMyTextSurface Surface => Me.GetSurface(0);

        private void ShowLog()
        {
            var text = log.Text;
            Surface.WriteText(log.HighestSeverity.ToString());
            Echo(text);
        }

        // Blocks

        private List<IMyBatteryBlock> batteryBlocks = new List<IMyBatteryBlock>();
        // FIXME: private List<IMyAssembler> assemblerBlocks = new List<IMyAssembler>();
        // FIXME: private IMyAssembler mainAssembler = null;

        // State

        private enum State
        {
            ScanCargo,
            ScanBatteries,
            ScanAssemblerQueues,
            ProduceMissing,
            Report,
            Reset,
        }

        private State state = State.ScanCargo;

        private int batteryIndex;
        private double batteryCharge;
        private double batteryCapacity;

        // FIXME:
        //private Dictionary<string, MyDefinitionId> restockComponents = new Dictionary<string, MyDefinitionId>();
        //private Dictionary<string, int> queuedComponents = new Dictionary<string, int>();



        // Parameter parsing (commands)

        private enum Command
        {
            Default,
            Unknown,
        }

        private Command ParseCommand(string argument)
        {
            switch (argument)
            {
                case "":
                    return Command.Default;
                default:
                    return Command.Unknown;
            }
        }

        public Program()
        {
            cfg = new Cfg();
            log = new Log(cfg);
            textPanels = new TextPanels(cfg, log, Me, GridTerminalSystem);
            inventory = new Inventory(cfg, log, Me, GridTerminalSystem, textPanels, rawData);
            
            Initialize();
            Load();
        }

        private void Initialize()
        {
            Surface.ContentType = ContentType.TEXT_AND_IMAGE;
            Surface.FontSize = 4f;

            Reset();
            ClearDisplays();
            
            var panel = textPanels.Find(Category.Status).FirstOrDefault();
            panel?.WriteText("Loading...");

            Runtime.UpdateFrequency = cfg.UpdateFrequency;
        }

        private void Reset()
        {
            log.Clear();
            textPanels.Reset();
            inventory.Reset();

            state = State.ScanCargo;
            
            batteryIndex = 0;
            batteryCapacity = 0f;
            batteryCharge = 0f;

            rawData.Clear();
            
            // DO NOT CLEAR: restockComponents.Clear();
            // FIXME: queuedComponents.Clear();

            // FIXME: Refactor 
            //GridTerminalSystem.GetBlockGroupWithName(cfg.RestockAssemblersGroup)?.GetBlocksOfType(assemblerBlocks, block => block.IsSameConstructAs(Me));
            //mainAssembler = assemblerBlocks.Where(assembler => assembler.CooperativeMode).FirstOrDefault() ?? assemblerBlocks.FirstOrDefault();
            
            GridTerminalSystem.GetBlocksOfType(batteryBlocks);

            

            log.Info("Text panels: {0}", textPanels.Count);
            log.Info("Blocks with items: {0}", inventory.CargoBlockCount);
            // FIXME: log.Info("Sorted containers: {0}", containerBlocks.Count);
            log.Info("Battery blocks: {0}", batteryBlocks.Count);
            // FIXME: log.Info("Restock assemblers: {0}", assemblerBlocks.Count);
        }

        private void ClearDisplays()
        {
            Surface.WriteText("Init");
            textPanels.ClearScreen();
        }

        private void Load()
        {
            // Load state from Storage here
        }

        public void Save()
        {
            // Save state to Storage here
        }

        public void Main(string argument, UpdateType updateSource)
        {
            log.Debug("Main {0} {1}", updateSource, argument);

            switch (updateSource)
            {
                case UpdateType.None:
                case UpdateType.Terminal:
                case UpdateType.Trigger:
                //case UpdateType.Antenna:
                case UpdateType.Mod:
                case UpdateType.Script:
                case UpdateType.Once:
                case UpdateType.IGC:
                    log.Clear();

                    try {
                        ProcessCommand(argument);
                    }
                    catch (Exception e)
                    {
                        log.Error(e.ToString());
                    }

                    break;

                case UpdateType.Update1:
                case UpdateType.Update10:
                case UpdateType.Update100:
                    try {
                        PeriodicProcessing();
                    }
                    catch (Exception e)
                    {
                        log.Error(e.ToString());
                    }

                    if (log.HighestSeverity >= LogSeverity.Error)
                    {
                        StopPeriodicProcessing();
                        DisplayLog();
                    }

                    break;
            }

            ShowLog();
        }

        private void StopPeriodicProcessing()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        private void ProcessCommand(string argument)
        {
            var command = ParseCommand(argument);
            switch (command)
            {
                case Command.Default:
                    Reset();
                    break;

                default:
                    log.Error("Unknown command");
                    break;
            }
        }

        private void PeriodicProcessing()
        {
            switch (state)
            {
                case State.ScanCargo:
                    for (int batch = 0; batch < cfg.CargoBatchSize; batch++)
                    {
                        inventory.Scan();
                    }
                    if (inventory.Done)
                    {
                        state = State.ScanBatteries;                        
                    }
                    break;

                case State.ScanBatteries:
                    for (int batch = 0; batch < cfg.BatteryBatchSize; batch++)
                    {
                        if (batteryIndex >= batteryBlocks.Count)
                        {
                            // FIXME: state = attemptedToMoveCargo ? State.Report : State.ScanAssemblerQueues;
                            state = State.ScanAssemblerQueues;
                            break;
                        }
                        ScanBattery();
                        batteryIndex++;
                    }
                    break;

                case State.ScanAssemblerQueues:
                    // FIXME: ScanAssemblerQueues();
                    state = State.ProduceMissing;
                    break;
                
                case State.ProduceMissing:
                    // FIXME: ProduceMissing();
                    state = State.Report;
                    break;

                case State.Report:
                    Report();
                    state = State.Reset;
                    break;
                
                case State.Reset:
                    Reset();
                    break;
            }
        }

        private void ScanBattery()
        {
            var battery = batteryBlocks[batteryIndex];
            if (battery == null)
            {
                log.Debug("Battery is missing");
                return;
            }

            if (!battery.IsFunctional)
            {
                log.Warning("Broken battery: " + battery.CustomName);
                return;
            }

            if (!battery.Enabled)
            {
                log.Warning("Disabled battery: " + battery.CustomName);
                return;
            }

            log.Debug("[{0}]: {1}", batteryIndex, battery.CustomName);

            batteryCapacity += battery.MaxStoredPower;
            batteryCharge += battery.CurrentStoredPower;
        }

        private void Report()
        {
            rawData.Append("now", FormatDateTime(DateTime.UtcNow));
            rawData.Append("status", log.HighestSeverity.ToString());
            rawData.Append("batteryCapacity", batteryCapacity);
            rawData.Append("batteryCharge", batteryCharge);
            rawData.Append("cargoCapacity", inventory.CargoCapacity);
            rawData.Append("cargoVolume", inventory.CargoVolume);
            rawData.Append("cargoMass", inventory.CargoMass * 1e-6);

            inventory.Display();

            DisplayStatus();
            DisplayLog();
            DisplayRawData();
        }

        private void DisplayStatus()
        {
            var panel = textPanels.Find(Category.Status).FirstOrDefault();
            if (panel == null)
            {
                log.Warning("No status panel");
                return;
            }

            var text = new StringBuilder();

            text.AppendLine(FormatDateTime(DateTime.UtcNow));
            text.AppendLine("");

            text.AppendLine($"Battery: {batteryCharge / Math.Max(1, batteryCapacity):p0}");
            text.AppendLine($"Energy: {Math.Round(batteryCharge):n2} MWh");
            text.AppendLine("");

            text.AppendLine($"Cargo: {inventory.CargoVolume / Math.Max(1, inventory.CargoCapacity):p0}");
            text.AppendLine($" Capacity: {Math.Round(inventory.CargoCapacity * 1e-6):n0} ML");
            text.AppendLine($" Volume: {Math.Round(inventory.CargoVolume * 1e-6):n0} ML");
            text.AppendLine($" Mass: {Math.Round(inventory.CargoMass * 1e-6):n0} kg");
            text.AppendLine("");

            panel.WriteText(text);
        }

        private void DisplayLog()
        {
            var panel = textPanels.Find(Category.Log).FirstOrDefault();
            if (panel == null)
            {
                log.Info("No log panel");
                return;
            }

            //var text = Util.Wrap(log.ToString(), (int)(PANEL_COLUMN_COUNT / LOG_FONT_SIZE));
            var text = log.ToString();
            panel.WriteText(text);
        }

        private void DisplayRawData()
        {
            var panel = textPanels.Find(Category.Raw).FirstOrDefault();
            if (panel == null)
            {
                return;
            }

            panel.WriteText(rawData.Text);
        }

        private static string FormatDateTime(DateTime dt)
        {
            return $"{dt:yyyy-MM-dd HH:mm:ss} UTC";
        }

        // FIXME: Refactor
        /*
        private void ScanAssemblerQueues()
        {
            foreach (var assembler in assemblerBlocks)
            {
                if (assembler.Closed || !assembler.IsFunctional)
                {
                    continue;
                }
                SummarizeAssemblerQueue(assembler);
            }
        }

        !!! AggregateAssemblerQueue
        private void SummarizeAssemblerQueue(IMyAssembler assembler)
        {
            !!! Summarize only if the assembler is in assembly mode, ignore if the assembler is in disassembly mode!
        
            var queue = new List<MyProductionItem>();
            assembler.GetQueue(queue);
            
            foreach (var item in queue)
            {
                var subtypeName = item.BlueprintId.SubtypeName;
                var amount = queuedComponents.GetValueOrDefault(subtypeName, 0);
                queuedComponents[subtypeName] = amount + (int)item.Amount;
            }
        }
        
        private void ProduceMissing()
        {
            if (mainAssembler == null || restockComponents.Count == 0 || mainAssembler.Closed || !mainAssembler.IsFunctional)
            {
                return;
            }
            
            !!! Enqueue only if the assembler is in assembly mode. Don't touch the queue if it is in disassembly mode! 

            if (cfg.Debug)
            {
                foreach (var kv in component)
                {
                    log.Info(string.Format("CC S:{1} C:{0}", kv.Key, kv.Value));
                }
                log.Info("---");
            
                foreach (var kv in queuedComponents)
                {
                    log.Info(string.Format("QC Q:{1} C:{0}", kv.Key, kv.Value));
                }
                log.Info("---");
            }
            
            foreach (var kv in restockComponents)
            {
                var subtypeName = kv.Key;
                var subtypeNameComponent = subtypeName + "Component";
                
                var stock = (int)component.GetValueOrDefault(subtypeName);
                if (stock == 0)
                {
                    stock = (int)component.GetValueOrDefault(subtypeNameComponent);
                }

                var queued = queuedComponents.GetValueOrDefault(subtypeName);
                if (queued == 0)
                {
                    queued = queuedComponents.GetValueOrDefault(subtypeNameComponent);
                }
                
                var missing = cfg.RestockMinimum - queued - stock;
                if (missing > queued)
                {
                    var definitionId = kv.Value;
                    if (cfg.Debug)
                    {
                        log.Info(string.Format("RF S:{0} Q:{1} M:{2} C:{3}", stock, queued, missing, definitionId.SubtypeName));
                    }
                    mainAssembler.AddQueueItem(definitionId, (MyFixedPoint)(missing + cfg.RestockOverhead));
                }
            }
        }
        */
    }
}