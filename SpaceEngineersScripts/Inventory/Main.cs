using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace SpaceEngineersScripts.Inventory
{
    // ReSharper disable once UnusedType.Global
    public class Program : MyGridProgram
    {
        private readonly Config config;
        private readonly Log log;

        private readonly TextPanels panels;
        private readonly Inventory inventory;
        private readonly Electric electric;
        private readonly Production production;

        private readonly RawData data;

        private State state = State.ScanInventory;

        private IMyTextSurface Surface => Me.GetSurface(0);


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
            config = new Config();
            log = new Log(config);

            if (!LoadConfig())
                return;

            panels = new TextPanels(config, log, Me, GridTerminalSystem);
            inventory = new Inventory(config, log, Me, GridTerminalSystem);
            electric = new Electric(config, log, Me, GridTerminalSystem);
            production = new Production(config, log, Me, GridTerminalSystem);

            data = new RawData();

            Initialize();
            Load();
        }

        private bool LoadConfig()
        {
            if (string.IsNullOrEmpty(Me.CustomData))
            {
                Me.CustomData = config.ToString();
            }
            else
            {
                var defaults = new Config();
                var errors = new List<string>();
                if (!config.TryParse(Me.CustomData, defaults, errors))
                {
                    log.Warning("Configuration errors:");
                    foreach (var line in errors)
                    {
                        log.Warning(line);
                    }

                    return false;
                }
            }

            return true;
        }

        private void Initialize()
        {
            Surface.ContentType = ContentType.TEXT_AND_IMAGE;
            Surface.FontSize = 4f;

            Reset();

            ClearDisplays();

            var panel = panels.Find(Category.Status).FirstOrDefault();
            panel?.WriteText("Loading...");

            Start();
        }

        private void Reset()
        {
            log.Clear();

            panels.Reset();
            inventory.Reset();
            electric.Reset();
            production.Reset();

            data.Clear();

            if (panels.TextPanelCount == 0)
            {
                Echo("No text panels");
            }
            
            log.Info("Text panels: {0}", panels.TextPanelCount);
            log.Info("Blocks with items: {0}", inventory.CargoBlockCount);
            log.Info("Battery blocks: {0}", electric.BatteryBlockCount);
            log.Info("Sorted containers: {0}", inventory.SortedContainerCount);
            log.Info("Restock assemblers: {0}", production.IsMainAssemblerAvailable ? production.AssemblerCount : 0);
        }

        private void ClearDisplays()
        {
            Surface.WriteText("Init");
            panels.ClearScreen();
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

                    try
                    {
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
                    try
                    {
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
            while (ProcessStep())
            {
            }
        }

        private void Start()
        {
            state = State.Reset;
            Runtime.UpdateFrequency = config.UseUpdate100 ? UpdateFrequency.Update100 : UpdateFrequency.Update10;
        }

        private void Stop()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        private bool ProcessStep()
        {
            switch (state)
            {
                case State.Reset:
                    Reset();
                    state = State.ScanBatteries;
                    break;

                case State.ScanBatteries:
                    for (int batch = 0; batch < config.BatteryBatchSize; batch++)
                    {
                        electric.Scan();
                        if (electric.Done)
                        {
                            state = State.ScanInventory;
                        }
                    }
                    break;

                case State.ScanInventory:
                    for (int batch = 0; batch < config.CargoBatchSize; batch++)
                    {
                        inventory.Scan();
                        if (inventory.Done)
                        {
                            state = State.MoveItems;
                        }
                    }
                    break;

                case State.MoveItems:
                    if (config.EnableItemSorting)
                    {
                        inventory.MoveItems();
                    }
                    state = State.ScanAssemblerQueues;
                    return inventory.MovedItemsCount == 0;

                case State.ScanAssemblerQueues:
                    production.ScanAssemblerQueues();
                    state = State.ProduceMissing;
                    return !production.IsMainAssemblerAvailable;

                case State.ProduceMissing:
                    production.ProduceMissing(inventory);
                    state = State.Report;
                    return production.EnqueueCount == 0;

                case State.Report:
                    Report();
                    if (config.SingleRun)
                    {
                        Stop();
                        break;
                    }
                    state = State.Reset;
                    break;
            }

            return false;
        }

        private void Report()
        {
            log.Info("Enqueued for production: {0}", production.EnqueueCount);
            log.Info("Items moved: {0}", inventory.MovedItemsCount);
            log.Info("");
            
            ShowLog();
            
            data.Append("now", FormatDateTime(DateTime.UtcNow));
            data.Append("status", log.HighestSeverity.ToString());
            data.Append("batteryCapacity", electric.Capacity);
            data.Append("batteryCharge", electric.Charge);
            data.Append("cargoCapacity", inventory.Capacity);
            data.Append("cargoVolume", inventory.Volume);
            data.Append("cargoMass", inventory.Mass * 1e-6);

            inventory.Display(panels, data);

            DisplayStatus();
            DisplayLog();
            DisplayRawData();
        }

        private void DisplayStatus()
        {
            var panel = panels.Find(Category.Status).FirstOrDefault();
            if (panel == null)
            {
                log.Warning("No status panel");
                return;
            }

            var text = new StringBuilder();

            text.AppendLine(FormatDateTime(DateTime.UtcNow));
            text.AppendLine("");

            var previous = Math.Round(electric.Previous);
            var current = Math.Round(electric.Charge);
            var change = current < previous ? "\\" : (current > previous ? "/" : "--");

            text.AppendLine($"Battery: {electric.Charge / Math.Max(1, electric.Capacity):p0} {change}");
            text.AppendLine($"Energy: {Math.Round(electric.Charge):n2} MWh");
            text.AppendLine("");

            text.AppendLine($"Cargo: {inventory.Volume / Math.Max(1, inventory.Capacity):p0}");
            text.AppendLine($"Capacity: {Math.Round(inventory.Capacity * 1e-6):n0} ML");
            text.AppendLine($"Volume: {Math.Round(inventory.Volume * 1e-6):n0} ML");
            text.AppendLine($"Mass: {Math.Round(inventory.Mass * 1e-6):n0} kg");
            text.AppendLine("");

            panel.WriteText(text);
        }

        private void DisplayLog()
        {
            var panel = panels.Find(Category.Log).FirstOrDefault();
            if (panel == null)
            {
                Echo("No log panel");
                return;
            }

            var text = log.ToString();
            if (config.WrapLog)
            {
                text = Util.Wrap(text, (int)(config.PanelColumnCount / config.LogFontSize));
            }
            panel.WriteText(text);
        }

        private void DisplayRawData()
        {
            var panel = panels.Find(Category.Raw).FirstOrDefault();
            if (panel == null)
            {
                return;
            }

            panel.WriteText(data.Text);
        }

        private static string FormatDateTime(DateTime dt)
        {
            return $"{dt:yyyy-MM-dd HH:mm:ss} UTC";
        }

        private void ShowLog()
        {
            var text = log.ToString();
            Surface.WriteText(log.HighestSeverity.ToString());
            Echo(text);
        }
    }
}