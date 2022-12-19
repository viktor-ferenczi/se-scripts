using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace SpaceEngineersScripts.Inventory
{
    public class TextPanels
    {
        private readonly Cfg cfg;
        private readonly Log log;
        private readonly IMyProgrammableBlock me;
        private readonly IMyGridTerminalSystem gts;
        
        private List<IMyTextPanel> textPanels = new List<IMyTextPanel>();

        public TextPanels(Cfg cfg, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts)
        {
            this.cfg = cfg;
            this.log = log;
            this.me = me;
            this.gts = gts;
        }

        public int Count => textPanels.Count; 
        
        public void Reset()
        {
            textPanels.Clear();
            gts.GetBlockGroupWithName(cfg.PanelsGroup)?.GetBlocksOfType(textPanels, block => block.IsSameConstructAs(me));

            if (textPanels == null || textPanels.Count == 0)
            {
                log.Error("No text panels in group {0}", cfg.PanelsGroup);
                return;
            }

            foreach (var panel in textPanels)
            {
                panel.ContentType = ContentType.TEXT_AND_IMAGE;

                if (panel.CustomName.ToLower().Contains("status"))
                {
                    panel.Font = "InfoMessageBoxText";
                    panel.FontSize = cfg.StatusFontSize;
                }
                else if (panel.CustomName.ToLower().Contains("log"))
                {
                    panel.Font = "InfoMessageBoxText";
                    panel.FontSize = cfg.LogFontSize;
                }
                else
                {
                    panel.Font = "Monospace";
                    panel.FontSize = cfg.DefaultFontSize;
                }

                panel.TextPadding = panel.FontSize;
            }

            if (cfg.Debug)
            {
                foreach (var panel in textPanels)
                {
                    log.Debug("Panel {0}", panel.CustomName);
                }
            }
        }

        public IOrderedEnumerable<IMyTextPanel> Find(Category category)
        {
            var substring = category.ToString().ToLower(); 
            return textPanels
                .Where(panel => panel.CustomName.ToLower().Contains(substring))
                .OrderBy(panel => panel.CustomName);
        }

        public void ClearScreen()
        {
            foreach (var panel in textPanels)
            {
                panel.WriteText("");
            }
        }
    }
}