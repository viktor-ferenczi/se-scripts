using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace OmniBeam
{
    // ReSharper disable once UnusedType.Global
    // ReSharper disable once ArrangeTypeModifiers
    class Program : MyGridProgram
    {
        private readonly Shipyard shipyard;
        private readonly DebugAPI debug;

        private IMyTextPanel lcdTimer;
        private IMyTextPanel lcdDetails;
        private IMyTextPanel lcdStatus;
        private IMyTextPanel lcdLog;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

#if DEBUG
            debug = new DebugAPI(this);
            debug?.RemoveDraw();
#endif

            FindTextPanels();

            Util.ClearLog();
            try
            {
                var projector = GridTerminalSystem.GetBlockWithName(Cfg.ProjectorName) as IMyProjector;
                shipyard = new Shipyard(Me, GridTerminalSystem, projector, lcdDetails, lcdStatus, lcdTimer, debug);
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
                textPanel.TextPadding = 2;
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
                lcdTimer.FontColor = Color.Yellow;
                lcdTimer.Alignment = TextAlignment.CENTER;
            }

            if (lcdStatus != null)
            {
                lcdStatus.Font = "Monospace";
                lcdStatus.FontSize = 0.8f;
                lcdStatus.FontColor = Color.LimeGreen;
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
                    debug?.RemoveDraw();
                    Util.Log(shipyard.State.ToString());
                    Util.Log(shipyard.Message ?? "");

                    if (((int) updateSource & (int) UpdateType.Update10) != 0)
                    {
                        shipyard.Update();
                    }
                    else
                    {
                        Command(argument);
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

        private void Command(string argument)
        {
            switch (argument.ToLower().Trim())
            {
                case "start":
                    shipyard.Start();
                    break;

                case "stop":
                    shipyard.Stop();
                    break;
            }
        }
    }
}