using System;
using System.Collections.Generic;
using System.Text;

namespace SpaceEngineersScripts.Inventory
{
    public class BaseConfig : Dictionary<string, object>
    {
        private static StringBuilder sb = new StringBuilder();

        public override string ToString()
        {
            sb.Clear();

            foreach (var p in this)
            {
                sb.Append($"{p.Key}={p.Value}\r\n");
            }

            return sb.ToString();
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
                var s = line.Split(new[] { '=' }, 2);
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