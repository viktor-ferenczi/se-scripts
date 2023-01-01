// Improved version of Morphik's Ice Calculator

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersScripts.IceCalc
{
    // ReSharper disable once UnusedType.Global
    public class Program : MyGridProgram
    {
        // Update frequency [100 frames]
        private const long UpdatePeriodInSeconds = 60;
        
        // LCD Name  
        private const string LcdName = "LCD Ice Calc";

        // Bar Graph Starting Character                    
        private const string Start = "[";

        // Bar Graph Ending Character                   
        private const string End = "]";

        // Bar Graph Delimiter Character                    
        private const char Bar = 'I';

        // Bar Graph Empty Space Character                    
        private const char Fill = '.';

        private readonly List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        private readonly List<IMyTerminalBlock> lcds = new List<IMyTerminalBlock>();
        private readonly List<IMyGasTank> gasTanks = new List<IMyGasTank>();
        private readonly StringBuilder sb = new StringBuilder();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        private const long UpdateAfterTicks = UpdatePeriodInSeconds * 60L; 
        private long ticks = UpdateAfterTicks;
        
        private int statusIndex;
        private readonly string[] runStatus = {
            "Program Running [|---]",
            "Program Running [-|--]",
            "Program Running [--|-]",
            "Program Running [---|]",
            "Program Running [--|-]",
            "Program Running [-|--]"
        };

        // ReSharper disable once UnusedMember.Local
        private void Main()
        {
            switch (Runtime.UpdateFrequency)
            {
                case UpdateFrequency.Update1:
                    ticks++;
                    break;
                
                case UpdateFrequency.Update10:
                    ticks += 10;
                    break;
                
                case UpdateFrequency.Update100:
                    ticks += 100;
                    break;
                
                case UpdateFrequency.Once:
                    ticks = UpdateAfterTicks;
                    break;
                
                default:
                    return;
            }

            if (ticks < UpdateAfterTicks)
            {
                return;
            }

            ticks = 0L;
            
            // Display All Information   
            IMyTextSurface pb = Me.GetSurface(0);
            pb.ContentType = ContentType.TEXT_AND_IMAGE;
            //pb.FontSize = 2;
            //pb.TextPadding = 5;
            //pb.Alignment = TextAlignment.CENTER;
            pb.WriteText("Ice to Hydrogen\n Calculator\n\n" + runStatus[statusIndex]);
            statusIndex = (statusIndex + 1) % runStatus.Length;

            /************ Performance Debug ************  
            int counter = 1;            
            int maxSeconds = 30;             
            if (counter <= maxSeconds * 60)            
            {            
                double timeToRunCode = Runtime.LastRunTimeMs;            
                    
                sb.Append(timeToRunCode.ToString("0.00")).Append("ms\n");            
                counter++;            
            }           
            Echo(sb.ToString()); 
            sb.Clear();  
            *************************************************/

            if (!blocks.Any())
            {
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, b => b.CubeGrid == Me.CubeGrid);
                GridTerminalSystem.SearchBlocksOfName(LcdName, lcds, b => b.CubeGrid == Me.CubeGrid);
                GridTerminalSystem.GetBlocksOfType(gasTanks, b => b.CubeGrid == Me.CubeGrid);
            }

            var gridS = Me.CubeGrid.GridSizeEnum.ToString();

            // A large tank has 15ML hydrogen
            double icePerLiter;
            switch (gridS)
            {
                case "Large":
                    icePerLiter = 277844.19 / 15e6;
                    break;
                
                case "Small":
                    icePerLiter = 19995.74 / 15e6;
                    break;
                
                default:
                    return;
            }

            var iceInInventory = 0.0;
            foreach (var block in blocks)
            {
                if (!block.HasInventory)
                {
                    continue;
                }

                var inventory = new List<MyInventoryItem>();
                block.GetInventory(0).GetItems(inventory);

                foreach (var item in inventory)
                {
                    var typeName = item.Type.ToString();
                    if (typeName.Contains("MyObjectBuilder_Ore/Ice"))
                    {
                        //Echo(iceList + tempInv.Amount); 
                        iceInInventory += (double)item.Amount;
                    }
                }
            }

            var tankCount = 0;
            var totalCapacity = 0.0;
            var totalHydrogen = 0.0;
            foreach (var tank in gasTanks)
            {
                MyResourceSinkComponent sink;
                tank.Components.TryGet(out sink);
                var list = sink.AcceptedResources;
                var hasH2 = false;
                foreach (var gas in list)
                {
                    hasH2 = gas.SubtypeId.ToString() == "Hydrogen";
                    if (hasH2)
                    {
                        break;
                    }
                }

                if (hasH2)
                {
                    ++tankCount;
                    totalCapacity += tank.Capacity;
                    totalHydrogen += tank.Capacity * tank.FilledRatio;
                }
            }

            var missingHydrogen = totalCapacity - totalHydrogen;
            var iceToFillTanks = missingHydrogen * icePerLiter;
            var missingIce = Math.Max(0.0, iceToFillTanks - iceInInventory);

            var fillRatio = totalCapacity > 0.0 ? totalHydrogen / totalCapacity : 0.0;
            var fillBar = BarBuilder(fillRatio);
            
            sb.Append($"Hydrogen status updated: {FormatDateTime(DateTime.UtcNow)}\n\n");
            sb.Append($"Fill level: {fillBar}\n\n");
            sb.Append($"Hydrogen tanks: {tankCount}\n");
            sb.Append($"Ice to fill tanks: {HumanFormat(iceToFillTanks)}\n");
            sb.Append($"Ice in inventory: {HumanFormat(iceInInventory)}\n");
            sb.Append($"Missing ice: {HumanFormat(missingIce)}\n\n");
            sb.Append($"Ice for a complete refill: {HumanFormat(totalCapacity * icePerLiter)}\n\n");

            var text = sb.ToString();
            sb.Clear();
            
            Echo(text);

            foreach (var myTerminalBlock in lcds)
            {
                var lcd = (IMyTextPanel)myTerminalBlock;
                lcd.ContentType = ContentType.TEXT_AND_IMAGE;
                //lcd.TextPadding = 0;
                //lcd.Alignment = TextAlignment.LEFT;
                //lcd.FontSize = 1;      
                lcd.WriteText(text);
            }

            blocks.Clear();
            lcds.Clear();
            gasTanks.Clear();
        }

        private static string HumanFormat(double d)
        {
            var v = (int)d;
            if (v >= 30000000)
            {
                return $"{(v + 500000) / 1000000:n0}M";
            }

            if (v >= 30000)
            {
                return $"{(v + 500) / 1000:n0}k";
            }

            return $"{v:n0}";
        }

        private string BarBuilder(double ratio)
        {
            var pct = ratio * 100;
            var cnt = (int)Math.Round(pct) / 2;
            
            sb.Append(Start);
            sb.Append(new string(Bar, cnt));
            sb.Append(new string(Fill, 50 - cnt));
            sb.Append(End);
            
            sb.Append($" {pct:0.00}%");
            var text = sb.ToString();
            
            sb.Clear();
            
            return text;
        }
        
        private static string FormatDateTime(DateTime dt)
        {
            return $"{dt:yyyy-MM-dd HH:mm:ss} UTC";
        }
    }
}