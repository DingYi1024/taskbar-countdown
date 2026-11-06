using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
class Watchdog {
    static void Main() {
        bool created;
        using (var guard = new Mutex(true, "Local\\DshCountdownWatchdog2027", out created)) {
            if (!created) return;
            var exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Countdown.exe");
            while (File.Exists(exe)) {
                try {
                    bool running;
                    using (var widget = new Mutex(false, "Local\\DshCountdownWpf2027")) {
                        bool acquired;
                        try { acquired = widget.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
                        running = !acquired;
                        if (acquired) widget.ReleaseMutex();
                    }
                    if (!running) using (var p = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false })) { }
                } catch (Exception ex) {
                    try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(exe), "watchdog_error.log"), DateTime.Now + " " + ex.Message + Environment.NewLine); } catch { }
                }
                Thread.Sleep(5000);
            }
        }
    }
}
