using System.Text;

namespace SpaceEngineersScripts.Inventory
{
    public class RawData
    {
        private StringBuilder text = new StringBuilder();
        
        public string Text => text.ToString();

        public void Clear()
        {
            text.Clear();
        }

        public void Append(string name, string value)
        {
            text.AppendLine(string.Format("{0}: \"{1}\"", name, value));
        }

        public void Append(string name, double value)
        {
            text.AppendLine(string.Format("{0}: {1}", name, value));
        }
    }
}