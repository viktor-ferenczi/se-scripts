using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace SpaceEngineersScripts.FabricatorArm
{
    // ReSharper disable once UnusedType.Global
    // ReSharper disable once ArrangeTypeModifiers
    class Program : MyGridProgram
    {
        private static IMyTextPanel lcdTimer;
        private static IMyTextPanel lcdDetails;
        private static IMyTextPanel lcdStatus;
        private static IMyTextPanel lcdLog;
        private readonly Shipyard shipyard;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            var debug = new DebugAPI(this);
            debug.RemoveDraw();

            PrepareDisplay();
            FindTextPanels();

            Util.ClearLog();
            try
            {
                var mgp = new MultigridProjectorProgrammableBlockAgent(Me);
                var projector = GridTerminalSystem.GetBlockWithName(Cfg.ProjectorName) as IMyProjector;
                shipyard = new Shipyard(GridTerminalSystem, projector, mgp, lcdDetails, lcdStatus, lcdTimer, debug);
            }
            catch (Exception e)
            {
                Util.Log(e.ToString());
                throw;
            }
            finally
            {
                Util.ShowLog(lcdLog);
            }
        }

        private void PrepareDisplay()
        {
            var pbSurface = Me.GetSurface(0);
            pbSurface.ContentType = ContentType.TEXT_AND_IMAGE;
            pbSurface.Alignment = TextAlignment.CENTER;
            pbSurface.FontColor = Color.DarkGreen;
            pbSurface.Font = "DEBUG";
            pbSurface.FontSize = 3f;
            pbSurface.WriteText("Fabricator Arm\r\nController");
        }

        private void FindTextPanels()
        {
            var lcdGroup = GridTerminalSystem.GetBlockGroupWithName(Cfg.TextPanelsGroupName);
            var textPanels = new List<IMyTextPanel>();
            lcdGroup?.GetBlocksOfType(textPanels);

            foreach (var textPanel in textPanels)
            {
                textPanel.ContentType = ContentType.TEXT_AND_IMAGE;
                textPanel.Alignment = TextAlignment.LEFT;
                textPanel.FontColor = Color.Cyan;
                textPanel.Font = "DEBUG";
                textPanel.FontSize = 1.2f;
                textPanel.WriteText("");
            }

            lcdTimer = textPanels.FirstOrDefault(p => p.CustomName.Contains("Timer"));
            lcdDetails = textPanels.FirstOrDefault(p => p.CustomName.Contains("Details"));
            lcdStatus = textPanels.FirstOrDefault(p => p.CustomName.Contains("Status"));
            lcdLog = textPanels.FirstOrDefault(p => p.CustomName.Contains("Log"));

            if (lcdTimer != null)
            {
                lcdTimer.Font = "Monospace";
                lcdTimer.FontSize = 4f;
                lcdTimer.Alignment = TextAlignment.CENTER;
                lcdTimer.TextPadding = 10;
            }

            if (lcdStatus != null)
            {
                lcdStatus.Font = "Monospace";
                lcdStatus.FontSize = 0.8f;
                lcdStatus.TextPadding = 0;
            }
        }

        // ReSharper disable once UnusedMember.Global
        public void Main(string argument, UpdateType updateSource)
        {
            Util.ClearLog();
            try
            {
                try
                {
                    if (((int) updateSource & (int) UpdateType.Update10) > 0)
                    {
                        shipyard.Update();
                    }
                }
                catch (Exception e)
                {
                    Util.Log(e.ToString());
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    throw;
                }
            }
            finally
            {
                Util.ShowLog(lcdLog);
            }
        }

        // ReSharper disable once UnusedMember.Global
        public void Save()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }
    }
}