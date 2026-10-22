using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Frankenstein
{
    /// <summary>A small JSON reader and writer, so the mod needs nothing beyond S1API and MelonLoader
    /// (the same code then works on the Mono and IL2CPP builds of the game).</summary>
    internal static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object v = Value(text, ref i);
            return v;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++;
                Skip(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Skip(s, ref i);
                    string k = Str(s, ref i);
                    Skip(s, ref i);
                    i++; // :
                    d[k] = Value(s, ref i);
                    Skip(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; // }
                    return d;
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++;
                Skip(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; // ]
                    return l;
                }
            }
            if (c == '"') return Str(s, ref i);
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            i++; // closing quote
            return sb.ToString();
        }

        public static string Write(object v)
        {
            var sb = new StringBuilder();
            Write(sb, v);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string str)
            {
                sb.Append('"');
                foreach (char c in str)
                {
                    if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                    else if (c == '\n') sb.Append("\\n");
                    else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                }
                sb.Append('"');
                return;
            }
            if (v is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (v is int || v is long || v is float || v is double)
            {
                sb.Append(Convert.ToDouble(v).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (v is IDictionary<string, object> d)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in d)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, kv.Key);
                    sb.Append(':');
                    Write(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            if (v is System.Collections.IEnumerable l)
            {
                sb.Append('[');
                bool first = true;
                foreach (var x in l)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, x);
                }
                sb.Append(']');
                return;
            }
            Write(sb, v.ToString());
        }
    }

    /// <summary>Friendly getters over the parsed JSON.</summary>
    internal static class J
    {
        public static string S(object o, string key, string fallback = "")
        {
            var d = o as Dictionary<string, object>;
            return d != null && d.TryGetValue(key, out var v) && v != null ? v.ToString() : fallback;
        }

        public static double N(object o, string key, double fallback = 0)
        {
            var d = o as Dictionary<string, object>;
            if (d != null && d.TryGetValue(key, out var v) && v is double n) return n;
            return fallback;
        }

        public static List<object> L(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            return d != null && d.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();
        }

        public static object O(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            return d != null && d.TryGetValue(key, out var v) ? v : null;
        }
    }
}
