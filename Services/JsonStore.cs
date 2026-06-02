using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace UltraLightApiTester.Services
{
    /// <summary>
    /// Minimal JSON serializer/deserializer for our simple data models.
    /// Avoids any external dependency.
    /// </summary>
    public static class JsonStore
    {
        public static string Serialize<T>(List<T> items)
        {
            var sb = new StringBuilder();
            sb.Append("[\n");
            for (int i = 0; i < items.Count; i++)
            {
                sb.Append("  ");
                SerializeObject(items[i], sb);
                if (i < items.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append("]");
            return sb.ToString();
        }

        public static List<T> Deserialize<T>(string json) where T : new()
        {
            var result = new List<T>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            int pos = 0;
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length || json[pos] != '[') return result;
            pos++; // skip '['

            while (pos < json.Length)
            {
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) break;
                if (json[pos] == ']') { pos++; break; }
                if (json[pos] == ',') { pos++; continue; }

                var obj = DeserializeObject<T>(json, ref pos);
                if (obj != null) result.Add(obj);
            }
            return result;
        }

        private static void SerializeObject<T>(T obj, StringBuilder sb)
        {
            sb.Append("{");
            var type = typeof(T);
            var props = type.GetProperties();
            bool first = true;
            foreach (var prop in props)
            {
                if (!prop.CanRead) continue;
                var value = prop.GetValue(obj);
                if (!first) sb.Append(", ");
                first = false;
                sb.Append("\"");
                sb.Append(prop.Name);
                sb.Append("\": ");
                SerializeValue(value, sb);
            }
            sb.Append("}");
        }

        private static void SerializeValue(object value, StringBuilder sb)
        {
            if (value == null)
            {
                sb.Append("null");
            }
            else if (value is string s)
            {
                sb.Append("\"");
                sb.Append(EscapeJson(s));
                sb.Append("\"");
            }
            else if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
            }
            else if (value is DateTime dt)
            {
                sb.Append("\"");
                sb.Append(dt.ToString("yyyy-MM-ddTHH:mm:ss"));
                sb.Append("\"");
            }
            else if (value is int || value is long || value is float || value is double || value is decimal)
            {
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0}", value));
            }
            else if (value is Dictionary<string, string> dict)
            {
                sb.Append("{");
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append("\"");
                    sb.Append(EscapeJson(kv.Key));
                    sb.Append("\": \"");
                    sb.Append(EscapeJson(kv.Value));
                    sb.Append("\"");
                }
                sb.Append("}");
            }
            else
            {
                sb.Append("\"");
                sb.Append(EscapeJson(value.ToString()));
                sb.Append("\"");
            }
        }

        private static T DeserializeObject<T>(string json, ref int pos) where T : new()
        {
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length || json[pos] != '{') return default;
            pos++; // skip '{'

            var obj = new T();
            var type = typeof(T);

            while (pos < json.Length)
            {
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) break;
                if (json[pos] == '}') { pos++; break; }
                if (json[pos] == ',') { pos++; continue; }

                var key = DeserializeString(json, ref pos);
                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == ':') pos++;

                var prop = type.GetProperty(key);
                if (prop != null && prop.CanWrite)
                {
                    var val = DeserializeValue(json, ref pos, prop.PropertyType);
                    prop.SetValue(obj, val);
                }
                else
                {
                    SkipValue(json, ref pos);
                }
            }
            return obj;
        }

        private static object DeserializeValue(string json, ref int pos, Type targetType)
        {
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length) return null;

            if (targetType == typeof(string) || targetType == typeof(DateTime))
            {
                var s = DeserializeString(json, ref pos);
                if (targetType == typeof(DateTime))
                {
                    DateTime.TryParse(s, out var dt);
                    return dt;
                }
                return s;
            }
            else if (targetType == typeof(bool))
            {
                if (json.Substring(pos).StartsWith("true")) { pos += 4; return true; }
                if (json.Substring(pos).StartsWith("false")) { pos += 5; return false; }
                return false;
            }
            else if (targetType == typeof(int))
            {
                return (int)DeserializeNumber(json, ref pos);
            }
            else if (targetType == typeof(long))
            {
                return DeserializeNumber(json, ref pos);
            }
            else if (targetType == typeof(Dictionary<string, string>))
            {
                return DeserializeDictionary(json, ref pos);
            }
            else
            {
                SkipValue(json, ref pos);
                return null;
            }
        }

        private static string DeserializeString(string json, ref int pos)
        {
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length || json[pos] != '"') return "";
            pos++; // skip opening quote
            var sb = new StringBuilder();
            while (pos < json.Length)
            {
                var ch = json[pos];
                if (ch == '"') { pos++; break; }
                if (ch == '\\' && pos + 1 < json.Length)
                {
                    pos++;
                    var next = json[pos];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 < json.Length)
                            {
                                var hex = json.Substring(pos + 1, 4);
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                    sb.Append((char)code);
                                pos += 4;
                            }
                            break;
                        default: sb.Append(next); break;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
                pos++;
            }
            return sb.ToString();
        }

        private static Dictionary<string, string> DeserializeDictionary(string json, ref int pos)
        {
            var dict = new Dictionary<string, string>();
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length || json[pos] != '{') return dict;
            pos++;
            while (pos < json.Length)
            {
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) break;
                if (json[pos] == '}') { pos++; break; }
                if (json[pos] == ',') { pos++; continue; }
                var key = DeserializeString(json, ref pos);
                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == ':') pos++;
                var val = DeserializeString(json, ref pos);
                if (!string.IsNullOrEmpty(key)) dict[key] = val;
            }
            return dict;
        }

        private static long DeserializeNumber(string json, ref int pos)
        {
            SkipWhitespace(json, ref pos);
            var sb = new StringBuilder();
            while (pos < json.Length && (char.IsDigit(json[pos]) || json[pos] == '-' || json[pos] == '.'))
            {
                sb.Append(json[pos]);
                pos++;
            }
            long.TryParse(sb.ToString(), out var n);
            return n;
        }

        private static void SkipValue(string json, ref int pos)
        {
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length) return;
            var ch = json[pos];
            if (ch == '"') { DeserializeString(json, ref pos); return; }
            if (ch == '{') { int depth = 1; pos++; while (pos < json.Length && depth > 0) { if (json[pos] == '{') depth++; if (json[pos] == '}') depth--; pos++; } return; }
            if (ch == '[') { int depth = 1; pos++; while (pos < json.Length && depth > 0) { if (json[pos] == '[') depth++; if (json[pos] == ']') depth--; pos++; } return; }
            while (pos < json.Length && json[pos] != ',' && json[pos] != '}' && json[pos] != ']') pos++;
        }

        private static void SkipWhitespace(string json, ref int pos)
        {
            while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;
        }

        private static string EscapeJson(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }
    }
}
