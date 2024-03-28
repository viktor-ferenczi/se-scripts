using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace Inventory
{
    public class TextPanels: ProgramModule
    {
        private List<IMyTextPanel> textPanels = new List<IMyTextPanel>();
        
        public TextPanels(Config config, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts) : base(config, log, me, gts)
        {
        }

        public int TextPanelCount => textPanels.Count; 
        
        public void Reset()
        {
            textPanels.Clear();
            Gts.GetBlockGroupWithName(Config.TextPanelsGroup)?.GetBlocksOfType(textPanels, block => block.IsSameConstructAs(Me));
            Util.SortBlocksByName(textPanels);

            if (textPanels == null || textPanels.Count == 0)
            {
                return;
            }

            foreach (var panel in textPanels)
            {
                panel.ContentType = ContentType.TEXT_AND_IMAGE;

                if (panel.CustomName.ToLower().Contains("status"))
                {
                    panel.Font = "InfoMessageBoxText";
                    panel.FontSize = Config.StatusFontSize;
                    panel.TextPadding = 4.0f;
                }
                else if (panel.CustomName.ToLower().Contains("log"))
                {
                    panel.Font = "InfoMessageBoxText";
                    panel.FontSize = Config.LogFontSize;
                    panel.TextPadding = 2.0f;
                }
                else
                {
                    panel.Font = "Monospace";
                    panel.FontSize = Config.DefaultFontSize;
                    panel.TextPadding = 2.0f;
                }
            }

            if (Config.Debug)
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