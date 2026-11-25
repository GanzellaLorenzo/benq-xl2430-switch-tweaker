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
    const uint MODE_STANDARD = 0;
    const int POLL_MS = 700;

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

    static void Main()
    {
        Log("iniciado");
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
                if (cur != MODE_STANDARD)
                {
                    Log("preset " + cur + " detectado -> Standard");
                    SetVCPFeature(h, VCP_MODE, MODE_STANDARD);
                }
            }
            else
            {
                if (++fails > 5) { h = IntPtr.Zero; fails = 0; Log("handle perdido, reabrindo"); }
            }

            Thread.Sleep(POLL_MS);
        }
    }
}
