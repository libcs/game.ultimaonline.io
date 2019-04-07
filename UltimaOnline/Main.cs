using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UltimaOnline.Network;

namespace UltimaOnline
{
    public delegate void Slice();

    public static class Core
    {
        static bool _Crashed;
        static Thread _timerThread;
        static string _BaseDirectory;
        static string _ExePath;
        static bool _Cache = true;
        static bool _Profiling;
        static DateTime _ProfileStart;
        static TimeSpan _ProfileTime;

        public static MessagePump MessagePump { get; set; }
        public static Slice Slice;
        public static bool Profiling
        {
            get => _Profiling;
            set
            {
                if (_Profiling == value)
                    return;
                _Profiling = value;
                if (_ProfileStart > DateTime.MinValue)
                    _ProfileTime += DateTime.UtcNow - _ProfileStart;
                _ProfileStart = _Profiling ? DateTime.UtcNow : DateTime.MinValue;
            }
        }
        public static TimeSpan ProfileTime => _ProfileStart > DateTime.MinValue ? _ProfileTime + (DateTime.UtcNow - _ProfileStart) : _ProfileTime;
        public static bool Service { get; private set; }
        public static bool Debug { get; private set; }
        internal static bool HaltOnWarning { get; private set; }
        public static List<string> DataDirectories { get; } = new List<string>();
        public static Assembly Assembly { get; set; }
        public static Version Version { get { return Assembly.GetName().Version; } }
        public static Process Process { get; private set; }
        public static Thread Thread { get; private set; }
        public static MultiTextWriter MultiConsoleOut { get; private set; }
        /* 
		 * DateTime.Now and DateTime.UtcNow are based on actual system clock time.
		 * The resolution is acceptable but large clock jumps are possible and cause issues.
		 * GetTickCount and GetTickCount64 have poor resolution.
		 * GetTickCount64 is unavailable on Windows XP and Windows Server 2003.
		 * Stopwatch.GetTimestamp() (QueryPerformanceCounter) is high resolution, but
		 * somewhat expensive to call because of its defference to DateTime.Now,
		 * which is why Stopwatch has been used to verify HRT before calling GetTimestamp(),
		 * enabling the usage of DateTime.UtcNow instead.
		 */
        static readonly bool _HighRes = Stopwatch.IsHighResolution;
        static readonly double _HighFrequency = 1000.0 / Stopwatch.Frequency;
        static readonly double _LowFrequency = 1000.0 / TimeSpan.TicksPerSecond;
        static bool _UseHRT;
        public static bool UsingHighResolutionTiming { get { return _UseHRT && _HighRes && !Unix; } }
        public static long TickCount { get { return (long)Ticks; } }
        public static double Ticks => _UseHRT && _HighRes && !Unix ? Stopwatch.GetTimestamp() * _HighFrequency : DateTime.UtcNow.Ticks * _LowFrequency;
        public static readonly bool Is64Bit = Environment.Is64BitProcess;
        public static bool MultiProcessor { get; private set; }
        public static int ProcessorCount { get; private set; }
        public static bool Unix { get; private set; }

        public static string FindDataFile(string path)
        {
            if (DataDirectories.Count == 0)
                throw new InvalidOperationException("Attempted to FindDataFile before DataDirectories list has been filled.");
            string fullPath = null;
            foreach (var p in DataDirectories)
            {
                fullPath = Path.Combine(p, path);
                if (File.Exists(fullPath))
                    break;
                fullPath = null;
            }
            return fullPath;
        }

        public static string FindDataFile(string format, params object[] args) => FindDataFile(string.Format(format, args));

        #region Expansions

        public static Expansion Expansion { get; set; }
        public static bool T2A => Expansion >= Expansion.T2A;
        public static bool UOR => Expansion >= Expansion.UOR;
        public static bool UOTD => Expansion >= Expansion.UOTD;
        public static bool LBR => Expansion >= Expansion.LBR;
        public static bool AOS => Expansion >= Expansion.AOS;
        public static bool SE => Expansion >= Expansion.SE;
        public static bool ML => Expansion >= Expansion.ML;
        public static bool SA => Expansion >= Expansion.SA;
        public static bool HS => Expansion >= Expansion.HS;
        public static bool TOL => Expansion >= Expansion.TOL;

        #endregion

        public static string ExePath => _ExePath ?? (_ExePath = Assembly.Location);

        public static string BaseDirectory
        {
            get
            {
                if (_BaseDirectory == null)
                {
                    try
                    {
                        _BaseDirectory = ExePath;
                        if (_BaseDirectory.Length > 0)
                            _BaseDirectory = Path.GetDirectoryName(_BaseDirectory);
                    }
                    catch { _BaseDirectory = string.Empty; }
                }
                return _BaseDirectory;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.IsTerminating ? "Error:" : "Warning:");
            Console.WriteLine(e.ExceptionObject);
            if (e.IsTerminating)
            {
                _Crashed = true;
                var close = false;
                try
                {
                    var args = new CrashedEventArgs(e.ExceptionObject as Exception);
                    EventSink.InvokeCrashed(args);
                    close = args.Close;
                }
                catch { }
                if (!close && !Service)
                {
                    try
                    {
                        foreach (var l in MessagePump.Listeners)
                            l.Dispose();
                    }
                    catch { }
                    Console.WriteLine("This exception is fatal, press return to exit");
                    Console.ReadLine();
                }
                Kill();
            }
        }

        internal enum ConsoleEventType
        {
            CTRL_C_EVENT,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        internal delegate bool ConsoleEventHandler(ConsoleEventType type);
        internal static ConsoleEventHandler _ConsoleEventHandler;

        internal class UnsafeNativeMethods
        {
            [DllImport("Kernel32")]
            internal static extern bool SetConsoleCtrlHandler(ConsoleEventHandler callback, bool add);
        }

        private static bool OnConsoleEvent(ConsoleEventType type)
        {
            if (World.Saving || (Service && type == ConsoleEventType.CTRL_LOGOFF_EVENT))
                return true;
            Kill(); //Kill -> HandleClosed will handle waiting for the completion of flushing to disk
            return true;
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e) => HandleClosed();
        public static bool Closing { get; private set; }

        static int _CycleIndex = 1;
        static readonly float[] _CyclesPerSecond = new float[100];

        public static float CyclesPerSecond => _CyclesPerSecond[(_CycleIndex - 1) % _CyclesPerSecond.Length];

        public static float AverageCPS => _CyclesPerSecond.Take(_CycleIndex).Average();


        public static void Kill(bool restart = false)
        {
            HandleClosed();
            if (restart)
                Process.Start(ExePath, Arguments);
            Process.Kill();
        }

        static void HandleClosed()
        {
            if (Closing)
                return;
            Closing = true;
            Console.Write("Exiting...");
            World.WaitForWriteCompletion();
            if (!_Crashed)
                EventSink.InvokeShutdown(new ShutdownEventArgs());
            Timer.TimerThread.Set();
            Console.WriteLine("done");
        }

        static readonly AutoResetEvent _Signal = new AutoResetEvent(true);

        public static void Set() => _Signal.Set();

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            foreach (string a in args)
            {
                if (Insensitive.Equals(a, "-debug")) Debug = true;
                else if (Insensitive.Equals(a, "-service")) Service = true;
                else if (Insensitive.Equals(a, "-profile")) Profiling = true;
                else if (Insensitive.Equals(a, "-nocache")) _Cache = false;
                else if (Insensitive.Equals(a, "-haltonwarning")) HaltOnWarning = true;
                else if (Insensitive.Equals(a, "-usehrt")) _UseHRT = true;
            }
            try
            {
                if (Service)
                {
                    if (!Directory.Exists("Logs"))
                        Directory.CreateDirectory("Logs");
                    Console.SetOut(MultiConsoleOut = new MultiTextWriter(new FileLogger("Logs/Console.log")));
                }
                else Console.SetOut(MultiConsoleOut = new MultiTextWriter(Console.Out));
            }
            catch { }
            Thread = Thread.CurrentThread;
            Process = Process.GetCurrentProcess();
            Assembly = Assembly.GetEntryAssembly();
            if (Thread != null)
                Thread.Name = "Core Thread";
            if (BaseDirectory.Length > 0)
                Directory.SetCurrentDirectory(BaseDirectory);
            var ttObj = new Timer.TimerThread();
            _timerThread = new Thread(ttObj.TimerMain) { Name = "Timer Thread" };
            var ver = Assembly.GetName().Version;

            // Added to help future code support on forums, as a 'check' people can ask for to it see if they recompiled core or not
            Console.WriteLine("Serv: Version {0}.{1}.{2}.{3}", ver.Major, ver.Minor, ver.Build, ver.Revision);
            Console.WriteLine("Core: Running on .NET Framework Version {0}.{1}.{2}", Environment.Version.Major, Environment.Version.Minor, Environment.Version.Build);
            var s = Arguments;
            if (s.Length > 0)
                Console.WriteLine("Core: Running with arguments: {0}", s);
            ProcessorCount = Environment.ProcessorCount;
            if (ProcessorCount > 1)
                MultiProcessor = true;
            if (MultiProcessor || Is64Bit)
                Console.WriteLine("Core: Optimizing for {0} {2}processor{1}", ProcessorCount, ProcessorCount == 1 ? "" : "s", Is64Bit ? "64-bit " : "");
            var platform = (int)Environment.OSVersion.Platform;
            if (platform == 4 || platform == 128)
            { // MS 4, MONO 128
                Unix = true;
                Console.WriteLine("Core: Unix environment detected");
            }
            else
            {
                _ConsoleEventHandler = OnConsoleEvent;
                UnsafeNativeMethods.SetConsoleCtrlHandler(_ConsoleEventHandler, true);
            }
            if (GCSettings.IsServerGC)
                Console.WriteLine("Core: Server garbage collection mode enabled");
            if (_UseHRT)
                Console.WriteLine("Core: Requested high resolution timing ({0})", UsingHighResolutionTiming ? "Supported" : "Unsupported");
            Console.WriteLine("RandomImpl: {0} ({1})", RandomImpl.Type.Name, RandomImpl.IsHardwareRNG ? "Hardware" : "Software");
            while (!ScriptCompiler.Compile(Debug, _Cache))
            {
                Console.WriteLine("Scripts: One or more scripts failed to compile or no script files were found.");
                if (Service)
                    return;
                Console.WriteLine(" - Press return to exit, or R to try again.");
                if (Console.ReadKey(true).Key != ConsoleKey.R)
                    return;
            }
            ScriptCompiler.Invoke("Configure");
            Region.Load();
            World.Load();
            ScriptCompiler.Invoke("Initialize");
            var messagePump = MessagePump = new MessagePump();
            _timerThread.Start();
            foreach (var m in Map.AllMaps)
                m.Tiles.Force();
            NetState.Initialize();
            EventSink.InvokeServerStarted();
            try
            {
                long now, last = TickCount;
                const int sampleInterval = 100;
                const float ticksPerSecond = 1000.0f * sampleInterval;
                var sample = 0L;
                while (!Closing)
                {
                    _Signal.WaitOne();
                    Mobile.ProcessDeltaQueue();
                    Item.ProcessDeltaQueue();
                    Timer.Slice();
                    messagePump.Slice();
                    NetState.FlushAll();
                    NetState.ProcessDisposedQueue();
                    Slice?.Invoke();
                    if (sample++ % sampleInterval != 0)
                        continue;
                    now = TickCount;
                    _CyclesPerSecond[_CycleIndex++ % _CyclesPerSecond.Length] = ticksPerSecond / (now - last);
                    last = now;
                }
            }
            catch (Exception e) { CurrentDomain_UnhandledException(null, new UnhandledExceptionEventArgs(e, true)); }
        }

        public static string Arguments
        {
            get
            {
                var sb = new StringBuilder();
                if (Debug) Utility.Separate(sb, "-debug", " ");
                if (Service) Utility.Separate(sb, "-service", " ");
                if (_Profiling) Utility.Separate(sb, "-profile", " ");
                if (!_Cache) Utility.Separate(sb, "-nocache", " ");
                if (HaltOnWarning) Utility.Separate(sb, "-haltonwarning", " ");
                if (_UseHRT) Utility.Separate(sb, "-usehrt", " ");
                return sb.ToString();
            }
        }

        public static int GlobalUpdateRange { get; set; } = 18;
        public static int GlobalMaxUpdateRange { get; set; } = 24;

        static int _ItemCount, _MobileCount;

        public static int ScriptItems => _ItemCount;
        public static int ScriptMobiles => _MobileCount;

        public static void VerifySerialization()
        {
            _ItemCount = 0;
            _MobileCount = 0;
            var ca = Assembly.GetCallingAssembly();
            VerifySerialization(ca);
            foreach (var a in ScriptCompiler.Assemblies.Where(a => a != ca))
                VerifySerialization(a);
        }

        static readonly Type[] _SerialTypeArray = { typeof(Serial) };

        static void VerifyType(Type t)
        {
            var isItem = t.IsSubclassOf(typeof(Item));
            if (!isItem && !t.IsSubclassOf(typeof(Mobile)))
                return;
            if (isItem) Interlocked.Increment(ref _ItemCount);
            else Interlocked.Increment(ref _MobileCount);
            StringBuilder b = null;
            try
            {
                if (t.GetConstructor(_SerialTypeArray) == null)
                {
                    b = new StringBuilder();
                    b.AppendLine("       - No serialization constructor");
                }
                if (t.GetMethod("Serialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly) == null)
                {
                    if (b == null)
                        b = new StringBuilder();
                    b.AppendLine("       - No Serialize() method");
                }
                if (t.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly) == null)
                {
                    if (b == null)
                        b = new StringBuilder();
                    b.AppendLine("       - No Deserialize() method");
                }
                if (b != null && b.Length > 0)
                    Console.WriteLine("Warning: {0}\n{1}", t, b);
            }
            catch { Console.WriteLine("Warning: Exception in serialization verification of type {0}", t); }
        }

        static void VerifySerialization(Assembly a)
        {
            if (a != null)
                Parallel.ForEach(a.GetTypes(), VerifyType);
        }
    }

    public class FileLogger : TextWriter
    {
        public const string DateFormat = "[MMMM dd hh:mm:ss.f tt]: ";
        bool _NewLine;
        public string FileName { get; private set; }

        public FileLogger(string file, bool append = false)
        {
            FileName = file;
            using (var w =  new StreamWriter(new FileStream(FileName, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                w.WriteLine(">>>Logging started on {0}.", DateTime.UtcNow.ToString("f"));
                //f = Tuesday, April 10, 2001 3:51 PM 
            }
            _NewLine = true;
        }

        public override void Write(char ch)
        {
            using (var w = new StreamWriter(new FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read)))
            {
                if (_NewLine)
                {
                    w.Write(DateTime.UtcNow.ToString(DateFormat));
                    _NewLine = false;
                }
                w.Write(ch);
            }
        }
        public override void Write(string str)
        {
            using (var w = new StreamWriter(new FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read)))
            {
                if (_NewLine)
                {
                    w.Write(DateTime.UtcNow.ToString(DateFormat));
                    _NewLine = false;
                }
                w.Write(str);
            }
        }

        public override void WriteLine(string line)
        {
            using (var w = new StreamWriter(new FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read)))
            {
                if (_NewLine)
                    w.Write(DateTime.UtcNow.ToString(DateFormat));
                w.WriteLine(line);
                _NewLine = true;
            }
        }

        public override Encoding Encoding => Encoding.Default;
    }

    public class MultiTextWriter : TextWriter
    {
        readonly List<TextWriter> _Streams;

        public MultiTextWriter(params TextWriter[] streams)
        {
            _Streams = new List<TextWriter>(streams);
            if (_Streams.Count < 0)
                throw new ArgumentException("You must specify at least one stream.");
        }

        public void Add(TextWriter tw) => _Streams.Add(tw);
        public void Remove(TextWriter tw) => _Streams.Remove(tw);

        public override void Write(char ch)
        {
            foreach (var t in _Streams)
                t.Write(ch);
        }

        public override void WriteLine(string line)
        {
            foreach (var t in _Streams)
                t.WriteLine(line);
        }

        public override void WriteLine(string line, params object[] args) => WriteLine(String.Format(line, args));
        public override Encoding Encoding => Encoding.Default;
    }
}