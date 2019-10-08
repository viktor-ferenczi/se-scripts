/*
 * Central Inventory
 *
 * Create a programmable block.
 * Copy-paste all code from the CodeEditor region below into the block.
 * Compile and run the code in the block.
 *
 * This program will periodically scan all of your cargo blocks.
 * Updates will be less frequent if you have more cargo blocks.
 *
 * It will make a summary available on the block's CustomData for other
 * compatible blocks to read, so it does not have to be collected again.
 *
 * Assign your text panels to the "Inventory Panels" group.
 *
 * Text panels must have the following in their name (case insensitive):
 * - Resource
 * - Ore
 * - Ingot
 * - Component
 * - Ammo
 * - Other
 * - Status
 * - Raw
 *
 * Set Content to Text and Images and Font to Monospaced on all panels.
 *
 * Component and Other may need two text panels to fit all items.
 * All text panels inside each resource type must have the same size.
 * Panels of the same type are concatenated in ascending name order.
 *
 * The raw panel displays raw inventory information for other blocks to use.
 *
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Skeleton;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Profiler;
using VRageMath;
using ContentType = VRage.Game.GUI.TextPanel.ContentType;
using IMyBatteryBlock = Sandbox.ModAPI.Ingame.IMyBatteryBlock;
using IMyBlockGroup = Sandbox.ModAPI.Ingame.IMyBlockGroup;
using IMyCargoContainer = Sandbox.ModAPI.Ingame.IMyCargoContainer;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyGasTank = Sandbox.Game.Entities.Interfaces.IMyGasTank;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyTextPanel = Sandbox.ModAPI.Ingame.IMyTextPanel;

namespace CentralInventory
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        // Config

        private const string PANEL_GROUP = "Inventory Panels";
        private const int PANEL_ROW_COUNT = 17;
        private const int PANEL_COLUMN_COUNT = 24;
        private const double DISPLAY_PRECISION = 1;
        private const int BATCH_SIZE = 5;
        private const UpdateFrequency UPDATE_FREQUENCY = UpdateFrequency.Update100;

        // Debugging

        enum LogSeverity
        {
            Ok,
            Warning,
            Error,
        }

        private bool DEBUG = false;
        private LogSeverity highestLogLogSeverity = LogSeverity.Ok;
        private readonly StringBuilder log = new StringBuilder();

        private void Log(string formatString, params object[] args)
        {
            log.AppendFormat(formatString + "\n", args);
        }

        private void Debug(string formatString, params object[] args)
        {
            if (DEBUG)
            {
                Log("D: " + formatString, args);
            }
        }

        private void Warning(string formatString, params object[] args)
        {
            Log("W: " + formatString, args);
            IncreaseSeverity(LogSeverity.Warning);
        }

        private void Error(string formatString, params object[] args)
        {
            Log("E: " + formatString, args);
            IncreaseSeverity(LogSeverity.Error);
        }

        private void ClearLog()
        {
            highestLogLogSeverity = LogSeverity.Ok;
            log.Clear();
        }

        private void ShowLog()
        {
            Echo(log.ToString());

            if (highestLogLogSeverity == LogSeverity.Ok)
            {
                Surface.WriteText("Running");
            }
            else
            {
                Surface.WriteText(highestLogLogSeverity.ToString());
            }
        }

        private void IncreaseSeverity(LogSeverity severity)
        {
            if (highestLogLogSeverity < severity)
            {
                highestLogLogSeverity = severity;
            }
        }

        private IMyTextSurface Surface
        {
            get
            {
                return Me.GetSurface(0);
            }
        }

        // Blocks

        private List<IMyCargoContainer> cargoBlocks = new List<IMyCargoContainer>();
        private List<IMyBatteryBlock> batteryBlocks = new List<IMyBatteryBlock>();
        private List<IMyTextPanel> textPanels = new List<IMyTextPanel>();

        // State

        private enum State
        {
            Cargo,
            Battery,
            Report,
        }

        private State state = State.Cargo;

        private int cargoIndex;
        private double cargoCapacity;
        private double cargoVolume;
        private double cargoMass;

        private int batteryIndex;
        private double batteryCharge;
        private double batteryCapacity;

        private StringBuilder rawData = new StringBuilder();

        Dictionary<string, double> ore = new Dictionary<string, double>();
        Dictionary<string, double> ingot = new Dictionary<string, double>();
        Dictionary<string, double> component = new Dictionary<string, double>();
        Dictionary<string, double> ammo = new Dictionary<string, double>();
        Dictionary<string, double> other = new Dictionary<string, double>();

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
            Initialize();
            Load();
        }

        private void Initialize()
        {
            Surface.ContentType = ContentType.TEXT_AND_IMAGE;
            Surface.FontSize = 4f;

            Reset();

            Runtime.UpdateFrequency = UPDATE_FREQUENCY;
        }

        private void Reset()
        {
            if (!DEBUG)
            {
                ClearLog();
            }

            state = State.Cargo;

            cargoIndex = 0;
            cargoCapacity = 0f;
            cargoVolume = 0f;
            cargoMass = 0f;

            batteryIndex = 0;
            batteryCapacity = 0f;
            batteryCharge = 0f;

            rawData.Clear();

            ore.Clear();
            ingot.Clear();
            component.Clear();
            ammo.Clear();
            other.Clear();

            cargoBlocks.Clear();
            textPanels.Clear();

            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargoBlocks);
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteryBlocks);
            GridTerminalSystem.GetBlockGroupWithName(PANEL_GROUP).GetBlocksOfType<IMyTextPanel>(textPanels);

            Log("Cargo blocks: {0}", cargoBlocks.Count);
            Log("Battery blocks: {0}", batteryBlocks.Count);
            Log("Text panels: {0}", textPanels.Count);
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
            Debug("Main {0} {1}", updateSource, argument);

            switch (updateSource)
            {
                case UpdateType.None:
                case UpdateType.Terminal:
                case UpdateType.Trigger:
                case UpdateType.Antenna:
                case UpdateType.Mod:
                case UpdateType.Script:
                case UpdateType.Once:
                case UpdateType.IGC:
                    ClearLog();

                    try {
                        ProcessCommand(argument);
                    }
                    catch (Exception e)
                    {
                        Error(e.ToString());
                    }

                    Log(highestLogLogSeverity.ToString());
                    break;

                case UpdateType.Update1:
                case UpdateType.Update10:
                case UpdateType.Update100:
                    try {
                        PeriodicProcessing();
                    }
                    catch (Exception e)
                    {
                        Error(e.ToString());
                    }

                    if (highestLogLogSeverity >= LogSeverity.Error)
                    {
                        StopPeriodicProcessing();
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
            // TODO: Add command processing below

            var command = ParseCommand(argument);
            switch (command)
            {
                case Command.Default:
                    Reset();
                    break;

                default:
                    Error("Unknown command");
                    break;
            }
        }

        private void PeriodicProcessing()
        {
            for (int batch = 0; batch < BATCH_SIZE; batch++)
            {
                switch (state)
                {
                    case State.Cargo:
                        if (cargoIndex >= cargoBlocks.Count)
                        {
                            state = State.Battery;
                            continue;
                        }
                        ScanCargo();
                        break;

                    case State.Battery:
                        if (batteryIndex >= batteryBlocks.Count)
                        {
                            state = State.Report;
                            continue;
                        }
                        ScanBattery();
                        break;

                    case State.Report:
                        Report();
                        Reset();
                        break;

                    default:
                        Reset();
                        break;
                }
            }
        }

        private void ScanCargo()
        {
            var cargo = cargoBlocks[cargoIndex++];
            if (cargo == null)
            {
                Debug("Cargo container is missing");
                return;
            }

            if (!cargo.IsFunctional)
            {
                Warning("Broken container: " + cargo.CustomName);
                return;
            }

            if (!cargo.IsWorking)
            {
                Warning("Disabled container: " + cargo.CustomName);
                return;
            }

            Debug("[{0}]: {1}", cargoIndex, cargo.CustomName);

            SummarizeCapacity(cargo);
            SummarizeCargoContainer(cargo);
        }

        private void SummarizeCapacity(IMyCargoContainer cargo)
        {
            IMyInventory blockInventory = cargo.GetInventory(0);
            cargoCapacity += blockInventory.MaxVolume.RawValue;
            cargoVolume += blockInventory.CurrentVolume.RawValue;
            cargoMass += blockInventory.CurrentMass.RawValue;
        }

        private void SummarizeCargoContainer(IMyCargoContainer cargo)
        {
            for (int i = 0; i < cargo.InventoryCount; i++)
            {
                SummarizeCargoInventory(cargo.GetInventory(i));
            }
        }

        private void SummarizeCargoInventory(IMyInventory blockInventory)
        {
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            blockInventory.GetItems(items);

            foreach (var item in items)
            {
                if (item.Amount <= 0)
                {
                    continue;
                }

                Dictionary<string, double> summary;
                switch (item.Type.TypeId)
                {
                    case "MyObjectBuilder_Ore":
                        summary = ore;
                        break;
                    case "MyObjectBuilder_Ingot":
                        summary = ingot;
                        break;
                    case "MyObjectBuilder_Component":
                        summary = component;
                        break;
                    case "MyObjectBuilder_AmmoMagazine":
                        summary = ammo;
                        break;
                    case "MyObjectBuilder_PhysicalGunObject":
                        summary = other;
                        break;
                    case "MyObjectBuilder_Datapad":
                        summary = other;
                        break;
                    case "MyObjectBuilder_GasContainerObject":
                        summary = other;
                        break;
                    case "MyObjectBuilder_OxygenContainerObject":
                        summary = other;
                        break;
                    case "MyObjectBuilder_PhysicalObject":
                        summary = other;
                        break;
                    default:
                        Warning("Skipping item with unknown item.Type.TypeID: {0}", item.Type.TypeId);
                        continue;
                }

                Accumulate(summary, item.Type.SubtypeId, (double) item.Amount);
            }
        }

        private void ScanBattery()
        {
            var battery = batteryBlocks[batteryIndex++];
            if (battery == null)
            {
                Debug("Battery is missing");
                return;
            }

            if (!battery.IsFunctional)
            {
                Warning("Broken battery: " + battery.CustomName);
                return;
            }

            if (!battery.IsWorking)
            {
                Warning("Disabled battery: " + battery.CustomName);
                return;
            }

            Debug("[{0}]: {1}", batteryIndex, battery.CustomName);

            batteryCapacity += battery.MaxStoredPower;
            batteryCharge += battery.CurrentStoredPower;
        }

        private void Report()
        {
            AppendRawData("now", FormatDateTime(DateTime.UtcNow));
            AppendRawData("status", highestLogLogSeverity.ToString());

            GridTerminalSystem.GetBlockGroupWithName(PANEL_GROUP).GetBlocksOfType<IMyTextPanel>(textPanels);

            foreach (var panel in textPanels)
            {
                Debug("Panel {0}", panel.CustomName);
            }

            DisplaySummary("ore", ore);
            DisplaySummary("ingot", ingot);
            DisplaySummary("component", component);
            DisplaySummary("ammo", ammo);
            DisplaySummary("other", other);

            DisplayStatus();
            DisplayRawData();
        }

        private void DisplaySummary(string kind, Dictionary<string, double> summary)
        {
            var panels = FindPanels(kind).ToList();
            if (panels.Count == 0)
            {
                Warning("No text panel for {0}", kind);
                return;
            }

            var panelRowCount = (int)Math.Floor(PANEL_ROW_COUNT / panels[0].FontSize);
            var panelIndex = 0;

            if (summary.Count > 0)
            {
                foreach (var page in FormatSummary(kind, summary, panelRowCount))
                {
                    if (panelIndex >= panels.Count)
                    {
                        Warning("Not enough panels to display full {0} information", kind);
                        break;
                    }

                    panels[panelIndex++].WriteText(page);
                }
            }

            while (panelIndex < panels.Count)
            {
                panels[panelIndex++].WriteText("");
            }
        }

        private IOrderedEnumerable<IMyTextPanel> FindPanels(string kind)
        {
            return textPanels
                .Where(panel => panel.CustomName.ToLower().Contains(kind))
                .OrderBy(panel => panel.CustomName);
        }

        private IEnumerable<StringBuilder> FormatSummary(string kind, Dictionary<string, double> summary, int panelRowCount)
        {
            var page = new StringBuilder();

            page.AppendLine(Capitalize(kind));
            page.AppendLine(new String('-', kind.Length));
            var lineCount = 2;

            var maxValue = summary.Values.Max();
            var maxWidth = maxValue >= 10
                ? string.Format("{0:n0}", Math.Round(maxValue / DISPLAY_PRECISION) * DISPLAY_PRECISION).Length
                : 1;

            var sortedSummary = summary.ToImmutableSortedDictionary();
            foreach (KeyValuePair<string, double> item in sortedSummary)
            {
                AppendRawData(kind + item.Key, item.Value);

                var formattedAmount = string.Format("{0:n0}", Math.Round(item.Value / DISPLAY_PRECISION) * DISPLAY_PRECISION);
                var line = formattedAmount.PadLeft(maxWidth) + " " + item.Key;

                page.AppendLine(line);
                lineCount++;

                if (lineCount >= panelRowCount)
                {
                    yield return page;
                    page = new StringBuilder();
                    lineCount = 0;
                }
            }

            if (page.Length > 0)
            {
                yield return page;
            }
        }

        private void DisplayStatus()
        {
            var panels = FindPanels("status").ToList();
            if (panels.Count == 0)
            {
                Warning("No status panel");
                return;
            }

            var text = new StringBuilder();

            text.AppendLine(FormatDateTime(DateTime.UtcNow));
            text.AppendLine("");

            AppendRawData("batteryCapacity", batteryCapacity);
            AppendRawData("batteryCharge", batteryCharge);
            text.AppendLine(string.Format("Battery: {0:p0}", batteryCharge / Math.Max(1, batteryCapacity)));
            text.AppendLine(string.Format("Energy: {0:n2} MWh", Math.Round(batteryCharge)));
            text.AppendLine("");

            AppendRawData("cargoCapacity", cargoCapacity);
            AppendRawData("cargoVolume", cargoVolume);
            AppendRawData("cargoMass", cargoMass * 1e-6);
            text.AppendLine(string.Format("Cargo: {0:p0}", cargoVolume / Math.Max(1, cargoCapacity)));
            text.AppendLine(string.Format(" Capacity: {0:n0} ML", Math.Round(cargoCapacity * 1e-6)));
            text.AppendLine(string.Format(" Volume: {0:n0} ML", Math.Round(cargoVolume * 1e-6)));
            text.AppendLine(string.Format(" Mass: {0:n0} kg", Math.Round(cargoMass * 1e-6)));
            text.AppendLine("");

            text.Append(Wrap(log.ToString(), PANEL_COLUMN_COUNT));

            var panel = panels.First();
            panel.WriteText(text);
        }

        private void DisplayRawData()
        {
            var panels = FindPanels("raw").ToList();
            if (panels.Count == 0)
            {
                return;
            }

            panels[0].WriteText(rawData);
        }

        private static string FormatDateTime(DateTime dt)
        {
            return string.Format("{0:yyyy-MM-dd HH:mm:ss} UTC", dt);
        }

        // Utility functions

        private void AppendRawData<T>(string name, T value)
        {
            rawData.AppendLine(string.Format("{0}={1}", name, value));
        }

        private void Accumulate(Dictionary<string, double> summary, string key, double amount)
        {
            if (summary.ContainsKey(key))
            {
                summary[key] += amount;
            }
            else
            {
                summary[key] = amount;
            }
        }

        private static string Capitalize(string text)
        {
            return text.Substring(0, 1).ToUpper() + text.Substring(1);
        }

        private static string Wrap(string text, int width)
        {
            var output = new StringBuilder();
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.TrimEnd();
                int position = 0;
                while (trimmed.Length > position + width)
                {
                    output.AppendLine(trimmed.Substring(position, width));
                    position += width;
                }
                if (position < trimmed.Length)
                {
                    output.AppendLine(trimmed.Substring(position));
                }
            }
            return output.ToString();
        }

        #endregion
    }
}