using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace UltimaOnline
{
    public static class ScriptCompiler
    {
        public static Assembly[] Assemblies { get; set; }

        private static List<string> _additionalReferences = new List<string>();

        public static string[] GetReferenceAssemblies()
        {
            var list = new List<string>();
            var path = Path.Combine(Core.BaseDirectory, "Data/Assemblies.cfg");
            string line;
            if (File.Exists(path))
                using (var ip = new StreamReader(path))
                    while ((line = ip.ReadLine()) != null)
                        if (line.Length > 0 && !line.StartsWith("#"))
                            list.Add(line);
            list.Add(Core.ExePath);
            list.AddRange(_additionalReferences);
            return list.ToArray();
        }

        public static string GetCompilerOptions(bool debug)
        {
            var b = new StringBuilder();
            b.Append("/unsafe ");
            if (!debug)
                b.Append("/optimize ");
            if (Core.Is64Bit)
                b.Append("/d:x64 ");
#if NEWTIMERS
			b.Append("/d:NEWTIMERS ");
#endif
#if NEWPARENT
			b.Append("/d:NEWPARENT ");
#endif
            b.Length--;
            return b.ToString();
        }

        static byte[] GetHashCode(string compiledFile, string[] scriptFiles, bool debug)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                var fileInfo = new FileInfo(compiledFile);
                bw.Write(fileInfo.LastWriteTimeUtc.Ticks);
                foreach (var scriptFile in scriptFiles)
                    bw.Write(new FileInfo(scriptFile).LastWriteTimeUtc.Ticks);
                bw.Write(debug);
                bw.Write(Core.Version.ToString());
                ms.Position = 0;
                using (var sha1 = SHA1.Create())
                    return sha1.ComputeHash(ms);
            }
        }

        public static bool CompileCSScripts(out Assembly assembly, bool debug = false, bool cache = true)
        {
            Console.Write("Scripts: Compiling C# scripts...");
            var files = GetScripts("*.cs");
            if (files.Length == 0)
            {
                Console.WriteLine("no files found.");
                assembly = null;
                return true;
            }
            if (File.Exists("Scripts/Output/Scripts.CS.dll") && cache && File.Exists("Scripts/Output/Scripts.CS.hash"))
                try
                {
                    var hashCode = GetHashCode("Scripts/Output/Scripts.CS.dll", files, debug);
                    using (var fs = new FileStream("Scripts/Output/Scripts.CS.hash", FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        var bytes = br.ReadBytes(hashCode.Length);
                        if (bytes.Length == hashCode.Length)
                        {
                            var valid = true;
                            for (var i = 0; i < bytes.Length; ++i)
                                if (bytes[i] != hashCode[i])
                                {
                                    valid = false;
                                    break;
                                }
                            if (valid)
                            {
                                assembly = Assembly.LoadFrom("Scripts/Output/Scripts.CS.dll");
                                if (!_additionalReferences.Contains(assembly.Location))
                                    _additionalReferences.Add(assembly.Location);
                                Console.WriteLine("done (cached)");
                                return true;
                            }
                        }
                    }
                }
                catch { }
            DeleteFiles("Scripts.CS*.dll");
            using (var provider = new CSharpCodeProvider())
            {
                var path = GetUnusedPath("Scripts.CS");
                CompilerParameters parms = new CompilerParameters(GetReferenceAssemblies(), path, debug);
                var options = GetCompilerOptions(debug);
                if (options != null)
                    parms.CompilerOptions = options;
                if (Core.HaltOnWarning)
                    parms.WarningLevel = 4;
                var results = provider.CompileAssemblyFromFile(parms, files);
                _additionalReferences.Add(path);
                Display(results);
                if (results.Errors.Count > 0)
                {
                    assembly = null;
                    return false;
                }
                if (cache && Path.GetFileName(path) == "Scripts.CS.dll")
                    try
                    {
                        var hashCode = GetHashCode(path, files, debug);
                        using (var fs = new FileStream("Scripts/Output/Scripts.CS.hash", FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var bin = new BinaryWriter(fs))
                            bin.Write(hashCode, 0, hashCode.Length);
                    }
                    catch { }
                assembly = results.CompiledAssembly;
                return true;
            }
        }

        public static void Display(CompilerResults results)
        {
            if (results.Errors.Count > 0)
            {
                var errors = new Dictionary<string, List<CompilerError>>(results.Errors.Count, StringComparer.OrdinalIgnoreCase);
                var warnings = new Dictionary<string, List<CompilerError>>(results.Errors.Count, StringComparer.OrdinalIgnoreCase);
                foreach (CompilerError e in results.Errors)
                {
                    var file = e.FileName;
                    // Ridiculous. FileName is null if the warning/error is internally generated in csc.
                    if (string.IsNullOrEmpty(file))
                    {
                        Console.WriteLine($"ScriptCompiler: {e.ErrorNumber}: {e.ErrorText}");
                        continue;
                    }
                    var table = e.IsWarning ? warnings : errors;
                    List<CompilerError> list = null;
                    table.TryGetValue(file, out list);
                    if (list == null)
                        table[file] = list = new List<CompilerError>();
                    list.Add(e);
                }
                if (errors.Count > 0)
                    Console.WriteLine($"failed ({errors.Count} errors, {warnings.Count} warnings)");
                else
                    Console.WriteLine($"done ({errors.Count} errors, {warnings.Count} warnings)");
                var scriptRoot = Path.GetFullPath(Path.Combine(Core.BaseDirectory, $"Scripts{Path.DirectorySeparatorChar}"));
                var scriptRootUri = new Uri(scriptRoot);
                Utility.PushColor(ConsoleColor.Yellow);
                if (warnings.Count > 0)
                    Console.WriteLine("Warnings:");
                foreach (KeyValuePair<string, List<CompilerError>> kvp in warnings)
                {
                    var fileName = kvp.Key;
                    var list = kvp.Value;
                    var fullPath = Path.GetFullPath(fileName);
                    var usedPath = Uri.UnescapeDataString(scriptRootUri.MakeRelativeUri(new Uri(fullPath)).OriginalString);
                    Console.WriteLine($" + {usedPath}:");
                    Utility.PushColor(ConsoleColor.DarkYellow);
                    foreach (CompilerError e in list)
                        Console.WriteLine($"    {e.ErrorNumber}: Line {e.Line}: {e.ErrorText}");
                    Utility.PopColor();
                }
                Utility.PopColor();
                Utility.PushColor(ConsoleColor.Red);
                if (errors.Count > 0)
                    Console.WriteLine("Errors:");
                foreach (var kvp in errors)
                {
                    var fileName = kvp.Key;
                    var list = kvp.Value;
                    var fullPath = Path.GetFullPath(fileName);
                    var usedPath = Uri.UnescapeDataString(scriptRootUri.MakeRelativeUri(new Uri(fullPath)).OriginalString);
                    Console.WriteLine($" + {usedPath}:");
                    Utility.PushColor(ConsoleColor.DarkRed);
                    foreach (var e in list)
                        Console.WriteLine($"    {e.ErrorNumber}: Line {e.Line}: {e.ErrorText}");
                    Utility.PopColor();
                }
                Utility.PopColor();
            }
            else Console.WriteLine("done (0 errors, 0 warnings)");
        }

        public static string GetUnusedPath(string name)
        {
            var path = Path.Combine(Core.BaseDirectory, $"Scripts/Output/{name}.dll");
            for (var i = 2; File.Exists(path) && i <= 1000; ++i)
                path = Path.Combine(Core.BaseDirectory, $"Scripts/Output/{name}.{i}.dll");
            return path;
        }

        public static void DeleteFiles(string mask)
        {
            try
            {
                var files = Directory.GetFiles(Path.Combine(Core.BaseDirectory, "Scripts/Output"), mask);
                foreach (string file in files)
                    try { File.Delete(file); }
                    catch { }
            }
            catch { }
        }

        private delegate CompilerResults Compiler(bool debug);

        public static bool Compile(bool debug = false, bool cache = true)
        {
            EnsureDirectory("Scripts/");
            EnsureDirectory("Scripts/Output/");
            if (_additionalReferences.Count > 0)
                _additionalReferences.Clear();
            var assemblies = new List<Assembly>();
            Assembly assembly;
            if (CompileCSScripts(out assembly, debug, cache))
                if (assembly != null)
                    assemblies.Add(assembly);
                else return false;
            if (assemblies.Count == 0)
                return false;
            Assemblies = assemblies.ToArray();
            Console.Write("Scripts: Verifying...");
            var watch = Stopwatch.StartNew();
            Core.VerifySerialization();
            watch.Stop();
            Console.WriteLine($"done ({Core.ScriptItems} items, {Core.ScriptMobiles} mobiles) ({watch.Elapsed.TotalSeconds:F2} seconds)");
            return true;
        }

        public static void Invoke(string method)
        {
            var invoke = new List<MethodInfo>();
            for (var a = 0; a < Assemblies.Length; ++a)
            {
                var types = Assemblies[a].GetTypes();
                for (var i = 0; i < types.Length; ++i)
                {
                    var m = types[i].GetMethod(method, BindingFlags.Static | BindingFlags.Public);
                    if (m != null)
                        invoke.Add(m);
                }
            }
            invoke.Sort(new CallPriorityComparer());
            for (var i = 0; i < invoke.Count; ++i)
                invoke[i].Invoke(null, null);
        }

        static Dictionary<Assembly, TypeCache> _typeCaches = new Dictionary<Assembly, TypeCache>();
        static TypeCache _nullCache;

        public static TypeCache GetTypeCache(Assembly asm)
        {
            if (asm == null)
            {
                if (_nullCache == null)
                    _nullCache = new TypeCache(null);
                return _nullCache;
            }
            _typeCaches.TryGetValue(asm, out var c);
            if (c == null)
                _typeCaches[asm] = c = new TypeCache(asm);
            return c;
        }

        public static Type FindTypeByFullName(string fullName, bool ignoreCase = true)
        {
            Type type = null;
            for (var i = 0; type == null && i < Assemblies.Length; ++i)
                type = GetTypeCache(Assemblies[i]).GetTypeByFullName(fullName, ignoreCase);
            if (type == null)
                type = GetTypeCache(Core.Assembly).GetTypeByFullName(fullName, ignoreCase);
            return type;
        }
        public static Type FindTypeByName(string name, bool ignoreCase = true)
        {
            Type type = null;
            for (var i = 0; type == null && i < Assemblies.Length; ++i)
                type = GetTypeCache(Assemblies[i]).GetTypeByName(name, ignoreCase);
            if (type == null)
                type = GetTypeCache(Core.Assembly).GetTypeByName(name, ignoreCase);
            return type;
        }

        public static void EnsureDirectory(string dir)
        {
            var path = Path.Combine(Core.BaseDirectory, dir);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public static string[] GetScripts(string filter)
        {
            var list = new List<string>();
            GetScripts(list, Path.Combine(Core.BaseDirectory, "Scripts"), filter);
            return list.ToArray();
        }

        public static void GetScripts(List<string> list, string path, string filter)
        {
            foreach (var dir in Directory.GetDirectories(path))
                GetScripts(list, dir, filter);
            list.AddRange(Directory.GetFiles(path, filter));
        }
    }

    public class TypeCache
    {
        public TypeCache(Assembly asm)
        {
            Types = asm == null ? Type.EmptyTypes : asm.GetTypes(); ;
            Names = new TypeTable(Types.Length);
            FullNames = new TypeTable(Types.Length);
            var typeofTypeAliasAttribute = typeof(TypeAliasAttribute);
            for (var i = 0; i < Types.Length; ++i)
            {
                var type = Types[i];
                Names.Add(type.Name, type);
                FullNames.Add(type.FullName, type);
                if (type.IsDefined(typeofTypeAliasAttribute, false))
                {
                    var attrs = type.GetCustomAttributes(typeofTypeAliasAttribute, false);
                    if (attrs != null && attrs.Length > 0)
                        if (attrs[0] is TypeAliasAttribute attr)
                            for (var j = 0; j < attr.Aliases.Length; ++j)
                                FullNames.Add(attr.Aliases[j], type);
                }
            }
        }

        public Type[] Types { get; }
        public TypeTable Names { get; }
        public TypeTable FullNames { get; }
        public Type GetTypeByName(string name, bool ignoreCase) => Names.Get(name, ignoreCase);
        public Type GetTypeByFullName(string fullName, bool ignoreCase) => FullNames.Get(fullName, ignoreCase);
    }

    public class TypeTable
    {
        readonly Dictionary<string, Type> _sensitive;
        readonly Dictionary<string, Type> _insensitive;

        public TypeTable(int capacity)
        {
            _sensitive = new Dictionary<string, Type>(capacity);
            _insensitive = new Dictionary<string, Type>(capacity, StringComparer.OrdinalIgnoreCase);
        }

        public void Add(string key, Type type)
        {
            _sensitive[key] = type;
            _insensitive[key] = type;
        }

        public Type Get(string key, bool ignoreCase)
        {
            Type t = null;
            if (ignoreCase) _insensitive.TryGetValue(key, out t);
            else _sensitive.TryGetValue(key, out t);
            return t;
        }
    }
}
