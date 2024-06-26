using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;

namespace Inventory
{
    public static class Util
    {
        private static StringBuilder output = new StringBuilder();
        
        public static string Capitalize(string text)
        {
            return text.Substring(0, 1).ToUpper() + text.Substring(1);
        }

        public static string Wrap(string text, int width)
        {
            output.Clear();
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.TrimEnd();

                var position = 0;
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

        public static void SortBlocksByName<T>(List<T> blocks) where T: IMyTerminalBlock
        {
            blocks.Sort((a, b) => string.Compare((a.CustomName ?? a.Name), b.CustomName ?? b.Name, StringComparison.Ordinal));
        }
    }
}