using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

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
    [DllImport("dxva2.dll")] static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr h, byte code, out uint type, out uint cur, out uint max);
    [DllImport("dxva2.dll")] static extern bool SetVCPFeature(IntPtr h, byte code, uint val);

    const byte VCP_MODE = 0xDC;
    const byte VCP_INSTANT = 0xD5;
    const byte VCP_BLUR = 0x4D;
    const byte VCP_AREA = 0x4E;
    const byte VCP_INTENSITY = 0x4F;
    const byte VCP_AMA = 0xF0;
    const byte VCP_DISPMODE = 0x86;

    const uint MODE_STANDARD = 0;
    const uint INSTANT_ON = 1;
    const uint BLUR_ON = 1;
    const uint AMA_HIGH = 1;
    const uint DISPMODE_FULL = 2;
    const int POLL_MS = 700;

    // Perfil por botao: indice 0 = Gamer 1 (24), 1 = Gamer 2 (25), 2 = Gamer 3 (26)
    static readonly uint[] PROFILE_AREA = { 48, 44, 40 };
    static readonly uint[] PROFILE_INTENSITY = { 20, 18, 16 };
    static readonly string[] PROFILE_NAME = { "FPS nitido 1.00ms", "Dia 1.33ms", "Claro 1.67ms" };

    static readonly string logPath = Path.Combine(Path.GetTempPath(), "xlkeeper.log");

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
        return arr[0].hPhysicalMonitor;
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

        Log("aplicado Area=" + PROFILE_AREA[idx] + " Intensity=" + PROFILE_INTENSITY[idx] + " AMA=High Full");
    }

    static void Main()
    {
        Log("iniciado  poll=" + POLL_MS + "ms");
        IntPtr h = IntPtr.Zero;
        int fails = 0;

        while (true)
        {
            if (h == IntPtr.Zero)
            {
                h = Open();
                if (h == IntPtr.Zero) { Thread.Sleep(5000); continue; }
            }

            uint cur;
            if (Read(h, VCP_MODE, out cur))
            {
                fails = 0;
                if (cur != MODE_STANDARD) ApplyPreset(h, cur);
            }
            else
            {
                if (++fails > 5) { h = IntPtr.Zero; fails = 0; Log("handle perdido, reabrindo"); }
            }

            Thread.Sleep(POLL_MS);
        }
    }
}
