using System.Text;

namespace SpaceEngineersScripts.Inventory
{
    public class Log
    {
        private readonly Cfg cfg;
        private LogSeverity highestSeverity = LogSeverity.Ok;
        private readonly StringBuilder text = new StringBuilder();

        private void IncreaseSeverity(LogSeverity severity)
        {
            if (highestSeverity < severity)
            {
                highestSeverity = severity;
            }
        }

        public Log(Cfg cfg)
        {
            this.cfg = cfg;
        }

        public LogSeverity HighestSeverity => highestSeverity;

        public string Text => text.ToString();

        public void Clear()
        {
            highestSeverity = LogSeverity.Ok;
            text.Clear();
        }

        public void Info(string formatString, params object[] args)
        {
            text.AppendFormat(formatString + "\n", args);
        }

        public void Debug(string formatString, params object[] args)
        {
            if (cfg.Debug)
            {
                Info("D: " + formatString, args);
            }
        }

        public void Warning(string formatString, params object[] args)
        {
            IncreaseSeverity(LogSeverity.Warning);
            Info("W: " + formatString, args);
        }

        public void Error(string formatString, params object[] args)
        {
            IncreaseSeverity(LogSeverity.Error);
            Info("E: " + formatString, args);
        }
    }
}