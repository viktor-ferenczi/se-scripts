using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace SpaceEngineersScripts.Inventory
{
    public class TextPanels: ProgramModule
    {
        private List<IMyTextPanel> textPanels = new List<IMyTextPanel>();
        
        public TextPanels(Cfg cfg, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts) : base(cfg, log, me, gts)
        {
        }

        public int Count => textPanels.Count; 
        
        public void Reset()
        {
            textPanels.Clear();
            Gts.GetBlockGroupWithName(Cfg.PanelsGroup)?.GetBlocksOfType(textPanels, block => block.IsSameConstructAs(Me));

            if (textPanels == null || textPanels.Count == 0)
            {
                Log.Error("No text panels in group {0}", Cfg.PanelsGroup);
                return;
            }

            foreach (var panel in textPanels)
            {
                panel.ContentType = ContentType.TEXT_AND_IMAGE;

                if (panel.CustomName.ToLower().Contains("status"))
                {
                    panel.Font = "InfoMessageBoxText";
                    panel.FontSize = Cfg.StatusFontSize;
                }
                else if (panel.CustomName.ToLower().Contains("log"))
                {
                    panel.Font = "InfoMessageBoxText";
                    panel.FontSize = Cfg.LogFontSize;
                }
                else
                {
                    panel.Font = "Monospace";
                    panel.FontSize = Cfg.DefaultFontSize;
                }

                panel.TextPadding = panel.FontSize;
            }

            if (Cfg.Debug)
            {
                foreach (var panel in textPanels)
                {
                    Log.Debug("Panel {0}", panel.CustomName);
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