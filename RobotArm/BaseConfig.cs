using System;
using System.Collections.Generic;
using System.Text;

namespace RobotArm
{
    public class BaseConfig : Dictionary<string, object>
    {
        private static readonly StringBuilder Sb = new StringBuilder();
        
        protected readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>();
        protected readonly Dictionary<string, object> Defaults = new Dictionary<string, object>();

        protected BaseConfig()
        {
            AddOptions();

            foreach (var p in Defaults)
            {
                Add(p.Key, p.Value);
            }
        }

        protected virtual void AddOptions()
        {
        }

        public override string ToString()
        {
            Sb.Clear();

            foreach (var p in this)
            {
                var description = Descriptions[p.Key];
                foreach (var line in description.Split('\n'))
                    Sb.AppendLine($"#| {line}");
                
                var isDefault = p.Value == Defaults[p.Key];
                var prefix = isDefault ? "#" : "";
                if (p.Value is float || p.Value is double)
                {
                    Sb.AppendLine($"{prefix}{p.Key}={p.Value:F3}");
                }
                else
                {
                    Sb.AppendLine($"{prefix}{p.Key}={p.Value}");
                }
                
                Sb.AppendLine();
            }

            return Sb.ToString();
        }

        public bool TryParse(string text, Dictionary<string, object> defaults, List<string> errors = null)
        {
            Clear();

            foreach (var p in defaults)
            {
                this[p.Key] = p.Value;
            }

            var success = true;

            foreach (var line in text.Replace('\r', '\n').Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#"))
                {
                    continue;
                }

                var s = trimmed.Split(new[] { '=' }, 2);
                if (s.Length != 2)
                {
                    errors?.Add($"Invalid: {line}");
                    success = false;
                    continue;
                }

                var name = s[0];

                object @default;
                if (!defaults.TryGetValue(name, out @default))
                {
                    errors?.Add($"Unknown: {line}");
                    success = false;
                    continue;
                }

                object value;
                var ok = false;

                if (@default is bool)
                {
                    bool v;
                    ok = bool.TryParse(s[1], out v);
                    value = v;
                }
                else if (@default is int)
                {
                    int v;
                    ok = int.TryParse(s[1], out v);
                    value = v;
                }
                else if (@default is float)
                {
                    float v;
                    ok = float.TryParse(s[1], out v);
                    value = v;
                }
                else if (@default is double)
                {
                    double v;
                    ok = double.TryParse(s[1], out v);
                    value = v;
                }
                else if (@default is string)
                {
                    ok = true;
                    value = s[1].Trim();
                }
                else
                {
                    value = null;
                }

                if (!ok)
                {
                    errors?.Add($"Cannot parse: {line}");
                    success = false;
                    continue;
                }

                this[name] = value;
            }

            return success;
        }
    }
}