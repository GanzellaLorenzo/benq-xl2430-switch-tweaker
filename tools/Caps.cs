using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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

    [DllImport("dxva2.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    static extern bool GetCapabilitiesStringLength(IntPtr h, out uint len);

    [DllImport("dxva2.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    static extern bool CapabilitiesRequestAndCapabilitiesReply(IntPtr h, StringBuilder sb, uint len);

    static void Main()
    {
        var outp = Path.Combine(Path.GetTempPath(), "caps.txt");
        var sb = new StringBuilder();
        sb.AppendLine("=== CAPABILITIES " + DateTime.Now.ToString("HH:mm:ss") + " ===");

        IntPtr hMon = MonitorFromWindow(GetDesktopWindow(), 1);
        uint n;
        GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out n);
        var arr = new PHYSICAL_MONITOR[n];
        GetPhysicalMonitorsFromHMONITOR(hMon, n, arr);
        var h = arr[0].hPhysicalMonitor;

        uint len;
        if (!GetCapabilitiesStringLength(h, out len))
        {
            sb.AppendLine("GetCapabilitiesStringLength falhou, err=" + Marshal.GetLastWin32Error());
        }
        else
        {
            sb.AppendLine("tamanho declarado: " + len);
            var buf = new StringBuilder((int)len + 2);
            if (CapabilitiesRequestAndCapabilitiesReply(h, buf, len))
            {
                sb.AppendLine("--- STRING ---");
                sb.AppendLine(buf.ToString());
            }
            else
            {
                sb.AppendLine("CapabilitiesRequestAndCapabilitiesReply falhou, err=" + Marshal.GetLastWin32Error());
            }
        }

        File.WriteAllText(outp, sb.ToString());
    }
}
