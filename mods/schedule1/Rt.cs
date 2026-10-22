using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Calls that work on both builds of Schedule I: the normal one (IL2CPP, where Unity's arrays and the
    /// game's own types are wrapped) and the Mono beta. Anything that returns an array, or names a type from the
    /// game, TextMeshPro or Unity's UI, goes through here by reflection instead of being compiled in.</summary>
    internal static class Rt
    {
        static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();
        static MethodInfo _findAll, _getComp, _getInChildren, _getComps;

        /// <summary>A type by its full name, with or without the "Il2Cpp" prefix IL2CPP games give game types.</summary>
        public static Type Find(string fullName)
        {
            Type t;
            if (_types.TryGetValue(fullName, out t)) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(fullName, false) ?? asm.GetType("Il2Cpp" + fullName, false);
                    if (t != null) break;
                }
                catch (Exception) { }
            }
            if (t != null) _types[fullName] = t;
            return t;
        }

        /// <summary>A game type by its class name alone, in whatever namespace this build put it.</summary>
        public static Type FindByName(string name)
        {
            Type t;
            if (_types.TryGetValue("#" + name, out t)) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var an = asm.GetName().Name;
                if (an != "Assembly-CSharp" && an != "Il2CppAssembly-CSharp") continue;
                try { t = asm.GetTypes().FirstOrDefault(x => x.Name == name); }
                catch (ReflectionTypeLoadException e) { t = e.Types.FirstOrDefault(x => x != null && x.Name == name); }
                if (t != null) break;
            }
            if (t != null) _types["#" + name] = t;
            return t;
        }

        static MethodInfo Generic(Type on, string name, int args)
        {
            return on.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                     .FirstOrDefault(m => m.Name == name && m.IsGenericMethodDefinition && m.GetParameters().Length == args);
        }

        static IEnumerable<object> Each(object list)
        {
            var e = list as IEnumerable;
            if (e == null) yield break;
            foreach (var x in e) if (x != null) yield return x;
        }

        /// <summary>Every live object of a type (FindObjectsOfType), as a plain list on either build.</summary>
        public static List<object> All(Type t)
        {
            if (t == null) return new List<object>();
            if (_findAll == null) _findAll = Generic(typeof(UnityEngine.Object), "FindObjectsOfType", 0);
            try { return Each(_findAll.MakeGenericMethod(t).Invoke(null, null)).ToList(); }
            catch (Exception) { return new List<object>(); }
        }

        public static List<T> All<T>() where T : UnityEngine.Object => All(typeof(T)).OfType<T>().ToList();

        public static object Comp(GameObject go, Type t)
        {
            if (go == null || t == null) return null;
            if (_getComp == null) _getComp = Generic(typeof(GameObject), "GetComponent", 0);
            try { return _getComp.MakeGenericMethod(t).Invoke(go, null); } catch (Exception) { return null; }
        }

        public static object CompInChildren(GameObject go, Type t)
        {
            if (go == null || t == null) return null;
            if (_getInChildren == null) _getInChildren = Generic(typeof(GameObject), "GetComponentInChildren", 0);
            try { return _getInChildren.MakeGenericMethod(t).Invoke(go, null); } catch (Exception) { return null; }
        }

        public static List<object> Comps(GameObject go, Type t, bool children)
        {
            if (go == null || t == null) return new List<object>();
            if (_getComps == null) _getComps = Generic(typeof(GameObject), "GetComponentsInChildren", 0);
            try
            {
                if (children) return Each(_getComps.MakeGenericMethod(t).Invoke(go, null)).ToList();
                var m = Generic(typeof(GameObject), "GetComponents", 0);
                return Each(m.MakeGenericMethod(t).Invoke(go, null)).ToList();
            }
            catch (Exception) { return new List<object>(); }
        }

        static MemberInfo Member(Type t, string name)
        {
            const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
                                   BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy;
            for (var x = t; x != null; x = x.BaseType)
            {
                var p = x.GetProperty(name, f);
                if (p != null && p.GetIndexParameters().Length == 0) return p;
                var fi = x.GetField(name, f);
                if (fi != null) return fi;
            }
            return null;
        }

        /// <summary>A field or property (IL2CPP turns the game's fields into properties), by any of a few names.</summary>
        public static object Get(object o, params string[] names)
        {
            if (o == null) return null;
            foreach (var n in names)
            {
                var m = Member(o.GetType(), n);
                try
                {
                    if (m is PropertyInfo p && p.CanRead) return p.GetValue(o, null);
                    if (m is FieldInfo fi) return fi.GetValue(o);
                }
                catch (Exception) { }
            }
            return null;
        }

        /// <summary>Sets the first of these names the object has. Returns the name it set, or null.</summary>
        public static string Set(object o, object value, params string[] names)
        {
            if (o == null) return null;
            foreach (var n in names)
            {
                var m = Member(o.GetType(), n);
                try
                {
                    var type = m is PropertyInfo pp ? pp.PropertyType : m is FieldInfo ff ? ff.FieldType : null;
                    if (type == null) continue;
                    var v = value == null || type.IsInstanceOfType(value) ? value : Convert.ChangeType(value, type);
                    if (m is PropertyInfo p && p.CanWrite) { p.SetValue(o, v, null); return n; }
                    if (m is FieldInfo fi && !fi.IsInitOnly) { fi.SetValue(o, v); return n; }
                }
                catch (Exception) { }
            }
            return null;
        }

        /// <summary>The real type name of a Unity object, even on IL2CPP where it's wrapped ("Name (Type)").</summary>
        public static string TypeName(object o)
        {
            if (o == null) return "";
            var s = o.ToString() ?? "";
            int a = s.LastIndexOf('('), b = s.LastIndexOf(')');
            return a >= 0 && b > a ? s.Substring(a + 1, b - a - 1) : o.GetType().FullName;
        }

        public static GameObject GameObjectOf(object component) => Get(component, "gameObject") as GameObject;

        /// <summary>The text shown by a TextMeshPro or Unity UI text, or null if it isn't one.</summary>
        public static string Text(object t) => Get(t, "text") as string;

        public static IEnumerable<object> Texts()
        {
            foreach (var n in new[] { "TMPro.TMP_Text", "UnityEngine.UI.Text" })
                foreach (var x in All(Find(n))) yield return x;
        }

        /// <summary>A picture file as a texture (ImageConversion.LoadImage on either build).</summary>
        public static Texture2D LoadImage(byte[] bytes)
        {
            try
            {
                var tex = new Texture2D(2, 2);
                var ic = Find("UnityEngine.ImageConversion");
                var m = ic?.GetMethods().FirstOrDefault(x => x.Name == "LoadImage" && x.GetParameters().Length == 2);
                if (m == null) return null;
                var want = m.GetParameters()[1].ParameterType;
                object data = bytes;
                if (!want.IsInstanceOfType(bytes))
                {
                    var conv = want.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                   .FirstOrDefault(x => x.Name == "op_Implicit" && x.GetParameters().Length == 1 &&
                                                        x.GetParameters()[0].ParameterType == typeof(byte[]));
                    data = conv != null ? conv.Invoke(null, new object[] { bytes }) : Activator.CreateInstance(want, new object[] { bytes });
                }
                return (bool)m.Invoke(null, new[] { tex, data }) ? tex : null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Calls S1API's cross-build ButtonUtils (works on both builds) by name.</summary>
        public static bool ButtonClick(object button, Action click)
        {
            var bu = Find("S1API.Utils.ButtonUtils");
            if (bu == null || button == null) return false;
            try
            {
                var ev = Get(button, "onClick");
                if (ev != null)
                {
                    // the copied button keeps the original's clicks (New game): give it a fresh, empty click event
                    try { Set(button, Activator.CreateInstance(ev.GetType()), "onClick"); } catch (Exception) { }
                }
                bu.GetMethod("ClearListeners")?.Invoke(null, new[] { button });
                bu.GetMethod("AddListener")?.Invoke(null, new object[] { button, click });
                return true;
            }
            catch (Exception) { return false; }
        }
    }

    /// <summary>Drawing that works on both builds. IL2CPP strips Unity methods the game itself never calls
    /// (GUI.DrawTexture and friends can be missing), so pictures and solid colours are drawn as a GUI box whose
    /// style has the picture as its background: that path is always there.</summary>
    internal static class Paint
    {
        static GUIStyle _plain;
        static bool _texOk = true;

        static GUIStyle Plain(Texture t)
        {
            if (_plain == null) _plain = new GUIStyle();
            _plain.normal.background = t as Texture2D;
            return _plain;
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.Box(r, "", Plain(Texture2D.whiteTexture));
            GUI.color = old;
        }

        /// <summary>A sprite (an item's icon). Single-picture sprites go through the always-there path; sprites
        /// cut from a sheet need DrawTextureWithTexCoords, tried once and dropped if this build lacks it.</summary>
        public static bool Sprite(Rect at, Sprite s)
        {
            if (s == null) return false;
            var t = s.texture;
            if (t == null) return false;
            var r = s.textureRect;
            bool whole = Math.Abs(r.width - t.width) < 1 && Math.Abs(r.height - t.height) < 1;
            if (!whole && _texOk)
            {
                try
                {
                    GUI.DrawTextureWithTexCoords(at, t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
                    return true;
                }
                catch (Exception e)
                {
                    _texOk = false;
                    MelonLoader.Melon<Plugin>.Logger.Warning("Icons: this build can't cut icons from a sheet (" + e.Message + "); showing whole pictures.");
                }
            }
            GUI.Box(at, "", Plain(t));
            return true;
        }

        public static void Picture(Rect at, Texture t)
        {
            if (t != null) GUI.Box(at, "", Plain(t));
        }
    }
}
