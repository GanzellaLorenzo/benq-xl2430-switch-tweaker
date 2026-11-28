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
    const byte VCP_DISPMODE = 0x86;
    const byte VCP_GAMMA = 0x72;

    const uint MODE_STANDARD = 0;
    const uint AUTO_OFF = 4;
    const uint INSTANT_ON = 1;
    const uint BLUR_ON = 1;
    const uint AMA_HIGH = 1;
    const uint DISPMODE_FULL = 2;
    const uint GAMMA_5 = 160;

    const int POLL_MS = 700;
    const int AUTO_EVERY = 5;
    const int SETTLE_MS = 500;
    const int SECOND_RELOCK_MS = 25000;   // o driver termina de aplicar o modo depois do logon
    const int FAILS_TO_ARM = 21;          // ~15 s de DDC caido antes de armar relock

    // Perfil por botao: indice 0 = Gamer 1 (24), 1 = Gamer 2 (25), 2 = Gamer 3 (26)
    // Area + (26 - Intensity) must stay <= 6000 / refresh (54 at 110 Hz).
    // Area also sets where the crosstalk band sits: higher area pushes it to the
    // bottom edge, lower area drags it into view. So each profile keeps Area as
    // high as its duty allows.
    static readonly uint[] PROFILE_AREA = { 48, 46, 44 };
    static readonly uint[] PROFILE_INTENSITY = { 20, 18, 16 };
    static readonly string[] PROFILE_NAME = { "FPS nitido 1.00ms", "Dia 1.33ms", "Claro 1.67ms" };

    static readonly string logPath = Path.Combine(Path.GetTempPath(), "xlkeeper.log");
    static readonly string pausePath = Path.Combine(Path.GetTempPath(), "xlkeeper.pause");

    static int secondRelockAt = 0;   // 0 = nenhuma confirmacao pendente

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

    // The monitor's DDC read can return a stale value from its cache, so a single
    // read is not trustworthy. Two reads that agree, 150 ms apart, are.
    static bool ReadStable(IntPtr h, byte code, out uint cur)
    {
        uint a, b;
        cur = 0;
        if (!Read(h, code, out a)) return false;
        Thread.Sleep(150);
        if (!Read(h, code, out b)) return false;
        if (a != b) return false;
        cur = a;
        return true;
    }

    // Reads never flicker; writes do. So only write when the value is wrong.
    static void SetIfDiffers(IntPtr h, byte code, uint want)
    {
        uint cur;
        if (Read(h, code, out cur) && cur == want) return;
        SetVCPFeature(h, code, want);
    }

    // The scaler drops writes that arrive while it is still applying the previous
    // change. This bit us on Area/Intensity: pressing 3 logged "applied 44/16" and
    // left 48/20 on screen, so all three profiles looked identical. Verify and retry.
    static void SetVerified(IntPtr h, byte code, uint want)
    {
        for (int i = 0; i < 5; i++)
        {
            uint cur;
            if (ReadStable(h, code, out cur) && cur == want) return;
            SetVCPFeature(h, code, want);
            Thread.Sleep(320);
        }
        Log("AVISO: VCP 0x" + code.ToString("X2") + " nao confirmou " + want);
    }

    // The whole reason this program exists. Writing 1 on a register that already
    // reads 1 is a no-op for the firmware, so the edge 0 -> 1 is mandatory.
    //
    // This monitor commits a VCP write asynchronously: right after writing 1 the
    // register can still read 0 for up to ~2 s. Firing the second edge too early
    // loses it and leaves Instant Mode OFF (and the vertical total ignored), so
    // the pulse is verified and retried.
    // Measured on hardware: a 700 ms gap is not enough — the scaler needs about
    // two seconds to commit the 0 before it will register the edge. With a short
    // gap the write is lost, Instant Mode stays OFF and the vertical total is
    // silently ignored. 2500 ms is the value that was verified to actually
    // clear the double image.
    static void PulseInstant(IntPtr h)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            SetVCPFeature(h, VCP_INSTANT, 0);
            Thread.Sleep(2500);
            SetVCPFeature(h, VCP_INSTANT, INSTANT_ON);
            Thread.Sleep(2000);

            uint cur;
            if (Read(h, VCP_INSTANT, out cur) && cur == INSTANT_ON) return;
            Log("instant mode nao confirmou, tentativa " + (attempt + 1));
        }

        Log("AVISO: Instant Mode nao confirmou ON");
    }

    static void RestoreState(IntPtr h, int idx, bool pulse)
    {
        uint mode;
        if (Read(h, VCP_MODE, out mode) && mode != MODE_STANDARD)
        {
            SetVCPFeature(h, VCP_MODE, MODE_STANDARD);
            // Enquanto o scaler aplica o modo ele descarta writes. 1.2 s e o
            // menor valor que se mostrou estavel em uso real.
            Thread.Sleep(1200);
        }

        // A picture mode change already re-applies the timing, so the slow pulse
        // is only worth its four seconds after a wake, where the scaler really
        // is latched. Pressing a button does not need it.
        if (pulse)
        {
            PulseInstant(h);
            Thread.Sleep(120);
        }

        SetIfDiffers(h, VCP_BLUR, BLUR_ON);
        // Se um pulso anterior perdeu a escrita do 1, o Instant Mode fica em 0 e
        // o VT deixa de ser usado - que e exatamente a imagem dupla. So repara
        // quando duas leituras concordam, para nao pulsar por leitura velha.
        uint inst;
        if (ReadStable(h, VCP_INSTANT, out inst) && inst != INSTANT_ON)
        {
            Log("instant mode estava em 0, religando");
            PulseInstant(h);
        }

        SetVerified(h, VCP_AREA, PROFILE_AREA[idx]);
        SetVerified(h, VCP_INTENSITY, PROFILE_INTENSITY[idx]);
        SetIfDiffers(h, VCP_AMA, AMA_HIGH);
        SetIfDiffers(h, VCP_DISPMODE, DISPMODE_FULL);
        SetIfDiffers(h, VCP_GAMMA, GAMMA_5);
    }

    static void ApplyPreset(IntPtr h, uint mode)
    {
        int idx = (int)mode - 24;
        if (idx < 0 || idx > 2) { Log("preset desconhecido " + mode + " -> perfil 1"); idx = 0; }

        Log("botao " + (idx + 1) + " -> " + PROFILE_NAME[idx]);
        // Se voce esta usando o monitor, o boot ja passou: a confirmacao atrasada
        // so atrapalharia as escritas do perfil.
        if (secondRelockAt != 0) { secondRelockAt = 0; Log("confirmacao de boot cancelada"); }
        RestoreState(h, idx, false);
        Log("aplicado Area=" + PROFILE_AREA[idx] + " Intensity=" + PROFILE_INTENSITY[idx] + " AMA=High Full Gamma5");
    }

    [STAThread]
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
        Log("iniciado v10  poll=" + POLL_MS + "ms");
        IntPtr h = IntPtr.Zero;
        int fails = 0, cycle = 0, stuck = 0;
        int retryAt = 0;
        bool needRelock = true;      // primeira conexao tambem relocka
        bool startupRelock = true;   // so o primeiro relock do processo pede confirmacao
        var sw = new Stopwatch();

        while (true)
        {
            sw.Restart();

            if (h == IntPtr.Zero)
            {
                if (Environment.TickCount < retryAt) { Thread.Sleep(POLL_MS); continue; }
                h = Open();
                if (h == IntPtr.Zero)
                {
                    retryAt = Environment.TickCount + 5000;
                    Thread.Sleep(POLL_MS);
                    continue;
                }
            }

            if (File.Exists(pausePath)) { Thread.Sleep(POLL_MS); continue; }

            uint cur;
            if (Read(h, VCP_MODE, out cur))
            {
                fails = 0;

                if (needRelock)
                {
                    Log("monitor voltou -> relock do scanout");
                    Thread.Sleep(SETTLE_MS);
                    RestoreState(h, 0, true);
                    needRelock = false;

                    // Confirmacao apenas no boot. Depois de um wake o driver nao
                    // reaplica nada, e um segundo pulso cairia no meio de uma
                    // partida sem motivo.
                    if (startupRelock)
                    {
                        startupRelock = false;
                        secondRelockAt = Environment.TickCount + SECOND_RELOCK_MS;
                    }

                    Log("relock concluido");
                }
                else if (secondRelockAt != 0 && Environment.TickCount >= secondRelockAt)
                {
                    secondRelockAt = 0;
                    Log("relock de confirmacao");
                    RestoreState(h, 0, true);
                    Log("relock de confirmacao concluido");
                }
                else if (cur != MODE_STANDARD)
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
                if (++fails > FAILS_TO_ARM)
                {
                    Close(h);
                    h = IntPtr.Zero;
                    fails = 0;
                    retryAt = Environment.TickCount + 3000;
                    needRelock = true;
                    Log("DDC caido por ~15s (standby?), armando relock");
                }
            }

            cycle++;

            int remaining = POLL_MS - (int)sw.ElapsedMilliseconds;
            if (remaining < 50) remaining = 50;
            Thread.Sleep(remaining);
        }
    }
}
