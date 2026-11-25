using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
    [DllImport("dwmapi.dll")] static extern int DwmFlush();

    static readonly string outPath = Path.Combine(Path.GetTempPath(), "perf.txt");

    static void Main(string[] args)
    {
        var sb = new StringBuilder();
        string label = args.Length > 0 ? args[0] : "run";
        sb.AppendLine("=== " + label + " " + DateTime.Now.ToString("HH:mm:ss") + " ===");

        // ---------- DDC latency ----------
        IntPtr hMon = MonitorFromWindow(GetDesktopWindow(), 1);
        uint n;
        GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out n);
        var arr = new PHYSICAL_MONITOR[n];
        GetPhysicalMonitorsFromHMONITOR(hMon, n, arr);
        var h = arr[0].hPhysicalMonitor;

        var lat = new List<double>();
        int ddcFails = 0;
        for (int i = 0; i < 200; i++)
        {
            var sw = Stopwatch.StartNew();
            uint t, c, m;
            bool ok = GetVCPFeatureAndVCPFeatureReply(h, 0xDC, out t, out c, out m);
            sw.Stop();
            if (!ok) ddcFails++;
            lat.Add(sw.Elapsed.TotalMilliseconds);
            Thread.Sleep(5);
        }
        lat.Sort();
        sb.AppendLine(string.Format("DDC 0xDC  n=200  fails={0}  min={1:F1}ms  p50={2:F1}  p95={3:F1}  max={4:F1}",
            ddcFails, lat[0], lat[100], lat[190], lat[199]));

        // ---------- frame timing ----------
        DwmFlush();
        var deltas = new List<double>();
        var total = Stopwatch.StartNew();
        double last = 0;
        while (total.ElapsedMilliseconds < 20000)
        {
            DwmFlush();
            double now = total.Elapsed.TotalMilliseconds;
            if (last > 0) deltas.Add(now - last);
            last = now;
        }

        deltas.Sort();
        double sum = 0;
        foreach (var d in deltas) sum += d;
        double avg = sum / deltas.Count;
        int over15 = 0, over2 = 0;
        foreach (var d in deltas)
        {
            if (d > avg * 1.5) over15++;
            if (d > avg * 2.0) over2++;
        }

        sb.AppendLine(string.Format("FRAMES  n={0}  avg={1:F3}ms ({2:F2} Hz)  p50={3:F2}  p99={4:F3}  p99.9={5:F3}  max={6:F3}",
            deltas.Count, avg, 1000.0 / avg, deltas[deltas.Count / 2],
            deltas[(int)(deltas.Count * 0.99)], deltas[(int)(deltas.Count * 0.999)], deltas[deltas.Count - 1]));
        sb.AppendLine(string.Format("STUTTER >1.5x: {0} ({1:F3}%)   >2.0x: {2} ({3:F3}%)",
            over15, 100.0 * over15 / deltas.Count, over2, 100.0 * over2 / deltas.Count));

        // histograma grosseiro
        int[] bins = new int[8];
        foreach (var d in deltas)
        {
            double r = d / avg;
            int b = r < 1.05 ? 0 : r < 1.2 ? 1 : r < 1.5 ? 2 : r < 2.0 ? 3 : r < 3.0 ? 4 : r < 5.0 ? 5 : 6;
            bins[b]++;
        }
        sb.AppendLine(string.Format("DIST  <1.05x={0}  1.05-1.2={1}  1.2-1.5={2}  1.5-2={3}  2-3={4}  3-5={5}  >5={6}",
            bins[0], bins[1], bins[2], bins[3], bins[4], bins[5], bins[6]));

        File.AppendAllText(outPath, sb.ToString());
    }
}
