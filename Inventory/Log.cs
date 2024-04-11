using System.Text;

namespace Inventory
{
    public class Log
    {
        private readonly Config config;
        private LogSeverity highestSeverity = LogSeverity.Ok;
        private readonly StringBuilder text = new StringBuilder();

        private void IncreaseSeverity(LogSeverity severity)
        {
            if (highestSeverity < severity)
            {
                highestSeverity = severity;
            }
        }

        public Log(Config config)
        {
            this.config = config;
        }

        public LogSeverity HighestSeverity => highestSeverity;

        public override string ToString()
        {
            return text.ToString();
        }

        public void Clear()
        {
            highestSeverity = LogSeverity.Ok;
            text.Clear();
        }

        public void Info(string formatString, params object[] args)
        {
            text.AppendFormat($"{formatString}\n", args);
        }

        public void Debug(string formatString, params object[] args)
        {
            if (config.Debug)
            {
                Info($"D: {formatString}", args);
            }
        }

        public void Warning(string formatString, params object[] args)
        {
            IncreaseSeverity(LogSeverity.Warning);
            Info($"W: {formatString}", args);
        }

        public void Error(string formatString, params object[] args)
        {
            IncreaseSeverity(LogSeverity.Error);
            Info($"E: {formatString}", args);
        }
    }
}