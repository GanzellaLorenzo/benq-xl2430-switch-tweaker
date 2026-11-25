using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

class Program
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szPhysicalMonitorDescription;
    }

    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("dxva2.dll")] static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr h, out uint n);
    [DllImport("dxva2.dll")] static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr h, uint n, [Out] PHYSICAL_MONITOR[] a);
    [DllImport("dxva2.dll")] static extern bool DestroyPhysicalMonitor(IntPtr h);
    [DllImport("dxva2.dll")] static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr h, byte code, out uint type, out uint cur, out uint max);
    [DllImport("dxva2.dll")] static extern bool SetVCPFeature(IntPtr h, byte code, uint val);

    const byte VCP_MODE = 0xDC;
    const byte VCP_AUTO = 0xD8;
    const byte VCP_INSTANT = 0xD5;
    const byte VCP_BLUR = 0x4D;
    const byte VCP_AREA = 0x4E;
    const byte VCP_INTENSITY = 0x4F;
    const byte VCP_AMA = 0xF0;
    const byte VCP_GAMMA = 0x72;
    const byte VCP_DISPMODE = 0x86;

    const uint MODE_STANDARD = 0;
    const uint AUTO_OFF = 4;
    const uint INSTANT_ON = 1;
    const uint BLUR_ON = 1;
    const uint AMA_HIGH = 1;
    const uint DISPMODE_FULL = 2;
    const uint GAMMA_5 = 160;

    const int POLL_MS = 700;
    const int AUTO_EVERY = 5;

    // Perfil por botao: indice 0 = Gamer 1 (24), 1 = Gamer 2 (25), 2 = Gamer 3 (26)
    static readonly uint[] PROFILE_AREA = { 48, 44, 40 };
    static readonly uint[] PROFILE_INTENSITY = { 20, 18, 16 };
    static readonly string[] PROFILE_NAME = { "FPS nitido 1.00ms", "Dia 1.33ms", "Claro 1.67ms" };

    static readonly string logPath = Path.Combine(Path.GetTempPath(), "xlkeeper.log");
    static readonly string pausePath = Path.Combine(Path.GetTempPath(), "xlkeeper.pause");

    static void Log(string msg)
    {
        try
        {
            var fi = new FileInfo(logPath);
            if (fi.Exists && fi.Length > 300000) fi.Delete();
            File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "\r\n");
        }
        catch { }
    }

    static IntPtr Open()
    {
        IntPtr hMon = MonitorFromWindow(GetDesktopWindow(), 1);
        uint n;
        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out n) || n == 0) return IntPtr.Zero;
        var arr = new PHYSICAL_MONITOR[n];
        if (!GetPhysicalMonitorsFromHMONITOR(hMon, n, arr)) return IntPtr.Zero;
        for (uint i = 1; i < n; i++) DestroyPhysicalMonitor(arr[i].hPhysicalMonitor);
        return arr[0].hPhysicalMonitor;
    }

    static void Close(IntPtr h)
    {
        if (h != IntPtr.Zero) DestroyPhysicalMonitor(h);
    }

    static bool Read(IntPtr h, byte code, out uint cur)
    {
        uint t, m;
        return GetVCPFeatureAndVCPFeatureReply(h, code, out t, out cur, out m);
    }

    static void ApplyPreset(IntPtr h, uint mode)
    {
        int idx = (int)mode - 24;
        if (idx < 0 || idx > 2) { Log("preset desconhecido " + mode + " -> perfil 1"); idx = 0; }

        Log("botao " + (idx + 1) + " -> " + PROFILE_NAME[idx]);

        SetVCPFeature(h, VCP_MODE, MODE_STANDARD);
        Thread.Sleep(180);
        SetVCPFeature(h, VCP_INSTANT, 0);
        Thread.Sleep(260);
        SetVCPFeature(h, VCP_INSTANT, INSTANT_ON);
        Thread.Sleep(500);

        SetVCPFeature(h, VCP_BLUR, BLUR_ON);
        Thread.Sleep(100);

        SetVCPFeature(h, VCP_AREA, PROFILE_AREA[idx]);
        SetVCPFeature(h, VCP_INTENSITY, PROFILE_INTENSITY[idx]);
        Thread.Sleep(200);
        SetVCPFeature(h, VCP_AREA, PROFILE_AREA[idx]);
        SetVCPFeature(h, VCP_INTENSITY, PROFILE_INTENSITY[idx]);

        SetVCPFeature(h, VCP_AMA, AMA_HIGH);
        SetVCPFeature(h, VCP_DISPMODE, DISPMODE_FULL);
        SetVCPFeature(h, VCP_GAMMA, GAMMA_5);

        Log("aplicado Area=" + PROFILE_AREA[idx] + " Intensity=" + PROFILE_INTENSITY[idx] + " AMA=High Full Gamma5");
    }

    static void Main()
    {
        bool created;
        using (var mutex = new Mutex(true, "Global\\XLStandardKeeper", out created))
        {
            if (!created) { Log("ja existe uma instancia"); return; }
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }

            Run();
        }
    }

    static void Run()
    {
        Log("iniciado  poll=" + POLL_MS + "ms");
        IntPtr h = IntPtr.Zero;
        int fails = 0, cycle = 0, stuck = 0;
        int retryAt = 0;
        var sw = new Stopwatch();

        while (true)
        {
            sw.Restart();

            if (h == IntPtr.Zero)
            {
                // Non-blocking retry: the loop keeps ticking at POLL_MS so a
                // button press is noticed as soon as the panel answers again.
                if (Environment.TickCount < retryAt) { Thread.Sleep(POLL_MS); continue; }
                h = Open();
                if (h == IntPtr.Zero)
                {
                    retryAt = Environment.TickCount + 5000;
                    Thread.Sleep(POLL_MS);
                    continue;
                }
                Log("conexao DDC restabelecida");
            }

            if (File.Exists(pausePath)) { Thread.Sleep(POLL_MS); continue; }

            uint cur;
            if (Read(h, VCP_MODE, out cur))
            {
                fails = 0;

                if (cur != MODE_STANDARD)
                {
                    ApplyPreset(h, cur);

                    uint again;
                    if (Read(h, VCP_MODE, out again) && again != MODE_STANDARD)
                    {
                        if (++stuck > 3) { Log("nao consegui restaurar (preso em " + again + ")"); Thread.Sleep(3000); stuck = 0; }
                    }
                    else stuck = 0;
                }

                if (cycle % AUTO_EVERY == 0)
                {
                    uint a;
                    if (Read(h, VCP_AUTO, out a) && a != AUTO_OFF)
                    {
                        Log("auto mode " + a + " -> off");
                        SetVCPFeature(h, VCP_AUTO, 0);
                    }
                }
            }
            else
            {
                if (++fails > 5)
                {
                    Close(h);
                    h = IntPtr.Zero;
                    fails = 0;
                    retryAt = Environment.TickCount + 3000;
                    Log("monitor nao responde DDC (standby?), tentando de novo em 3s");
                }
            }

            cycle++;

            int remaining = POLL_MS - (int)sw.ElapsedMilliseconds;
            if (remaining < 50) remaining = 50;
            Thread.Sleep(remaining);
        }
    }
}
