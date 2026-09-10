using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CyberRider.Core
{
    /// <summary>
    /// Tiny JSON reader/writer over plain objects: Dictionary&lt;string, object&gt;, List&lt;object&gt;,
    /// double, string, bool and null. Unity's JsonUtility cannot handle the nested, optional shapes
    /// used by tracks and save data, so the game ships its own.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            var p = new Parser(text);
            p.SkipWs();
            object v = p.ReadValue();
            p.SkipWs();
            if (p.Pos != text.Length) throw new FormatException("Trailing characters in JSON");
            return v;
        }

        public static string Stringify(object value, bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(sb, value, pretty, 0);
            return sb.ToString();
        }

        // ------------------------------------------------------------ accessors

        public static Dictionary<string, object> Obj(object v) => v as Dictionary<string, object>;

        public static List<object> List(object v) => v as List<object>;

        public static object Get(Dictionary<string, object> o, string key)
        {
            if (o == null) return null;
            return o.TryGetValue(key, out var v) ? v : null;
        }

        public static double Num(object v, double fallback = 0)
        {
            if (v is double d) return d;
            if (v is int i) return i;
            if (v is long l) return l;
            if (v is float f) return f;
            if (v is bool b) return b ? 1 : 0;
            if (v is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
            return fallback;
        }

        public static double Num(Dictionary<string, object> o, string key, double fallback = 0) => Num(Get(o, key), fallback);

        public static int Int(Dictionary<string, object> o, string key, int fallback = 0) => (int)Math.Round(Num(Get(o, key), fallback));

        public static string Str(object v, string fallback = null) => v is string s ? s : v == null ? fallback : Convert.ToString(v, CultureInfo.InvariantCulture);

        public static string Str(Dictionary<string, object> o, string key, string fallback = null) => Str(Get(o, key), fallback);

        public static bool Bool(object v, bool fallback = false)
        {
            if (v is bool b) return b;
            if (v is double d) return d != 0;
            return fallback;
        }

        public static bool Bool(Dictionary<string, object> o, string key, bool fallback = false) => Bool(Get(o, key), fallback);

        public static bool Has(Dictionary<string, object> o, string key) => o != null && o.ContainsKey(key) && o[key] != null;

        // ------------------------------------------------------------ writer

        private static void Write(StringBuilder sb, object v, bool pretty, int depth)
        {
            switch (v)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case double d:
                    WriteNumber(sb, d);
                    break;
                case float f:
                    WriteNumber(sb, f);
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case Dictionary<string, object> o:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in o)
                    {
                        if (kv.Value == null) continue;
                        if (!first) sb.Append(',');
                        first = false;
                        if (pretty) Indent(sb, depth + 1);
                        WriteString(sb, kv.Key);
                        sb.Append(':');
                        if (pretty) sb.Append(' ');
                        Write(sb, kv.Value, pretty, depth + 1);
                    }
                    if (pretty && !first) Indent(sb, depth);
                    sb.Append('}');
                    break;
                }
                case List<object> list:
                {
                    sb.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        if (pretty) Indent(sb, depth + 1);
                        Write(sb, list[i], pretty, depth + 1);
                    }
                    if (pretty && list.Count > 0) Indent(sb, depth);
                    sb.Append(']');
                    break;
                }
                default:
                    WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static void Indent(StringBuilder sb, int depth)
        {
            sb.Append('\n');
            for (int i = 0; i < depth; i++) sb.Append(' ');
        }

        private static void WriteNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                sb.Append("null");
                return;
            }
            if (d == Math.Floor(d) && Math.Abs(d) < 1e15) sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
            else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------ parser

        private sealed class Parser
        {
            private readonly string _s;
            public int Pos;

            public Parser(string s)
            {
                _s = s;
            }

            public void SkipWs()
            {
                while (Pos < _s.Length && char.IsWhiteSpace(_s[Pos])) Pos++;
            }

            public object ReadValue()
            {
                SkipWs();
                if (Pos >= _s.Length) throw new FormatException("Unexpected end of JSON");
                char c = _s[Pos];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ReadNumber();
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, Pos, word, 0, word.Length) != 0) throw new FormatException("Bad JSON literal at " + Pos);
                Pos += word.Length;
            }

            private Dictionary<string, object> ReadObject()
            {
                var o = new Dictionary<string, object>();
                Pos++; // {
                SkipWs();
                if (Pos < _s.Length && _s[Pos] == '}')
                {
                    Pos++;
                    return o;
                }
                while (true)
                {
                    SkipWs();
                    string key = ReadString();
                    SkipWs();
                    if (_s[Pos] != ':') throw new FormatException("Expected ':' at " + Pos);
                    Pos++;
                    o[key] = ReadValue();
                    SkipWs();
                    if (_s[Pos] == ',')
                    {
                        Pos++;
                        continue;
                    }
                    if (_s[Pos] == '}')
                    {
                        Pos++;
                        return o;
                    }
                    throw new FormatException("Expected ',' or '}' at " + Pos);
                }
            }

            private List<object> ReadArray()
            {
                var list = new List<object>();
                Pos++; // [
                SkipWs();
                if (Pos < _s.Length && _s[Pos] == ']')
                {
                    Pos++;
                    return list;
                }
                while (true)
                {
                    list.Add(ReadValue());
                    SkipWs();
                    if (_s[Pos] == ',')
                    {
                        Pos++;
                        continue;
                    }
                    if (_s[Pos] == ']')
                    {
                        Pos++;
                        return list;
                    }
                    throw new FormatException("Expected ',' or ']' at " + Pos);
                }
            }

            private string ReadString()
            {
                if (_s[Pos] != '"') throw new FormatException("Expected string at " + Pos);
                Pos++;
                var sb = new StringBuilder();
                while (Pos < _s.Length)
                {
                    char c = _s[Pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    char e = _s[Pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            sb.Append((char)Convert.ToInt32(_s.Substring(Pos, 4), 16));
                            Pos += 4;
                            break;
                        default: throw new FormatException("Bad escape at " + Pos);
                    }
                }
                throw new FormatException("Unterminated string");
            }

            private double ReadNumber()
            {
                int start = Pos;
                while (Pos < _s.Length)
                {
                    char c = _s[Pos];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') Pos++;
                    else break;
                }
                if (start == Pos) throw new FormatException("Unexpected character at " + Pos);
                return double.Parse(_s.Substring(start, Pos - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
