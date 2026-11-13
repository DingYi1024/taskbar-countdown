using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

class Watchdog {
    static bool IsWidgetRunning() {
        using (var widget = new Mutex(false, "Local\\DshCountdownWpf2027")) {
            bool acquired;
            try { acquired = widget.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (acquired) widget.ReleaseMutex();
            return !acquired;
        }
    }

    static bool PromotePending(string exe, string pending) {
        if (!File.Exists(pending)) return false;
        for (int i = 0; i < 8; i++) {
            try {
                if (File.Exists(exe)) File.Replace(pending, exe, null);
                else File.Move(pending, exe);
                return true;
            } catch (IOException) {
                Thread.Sleep(150);
            } catch (UnauthorizedAccessException) {
                Thread.Sleep(150);
            }
        }
        return false;
    }

    static void Main() {
        bool created;
        using (var guard = new Mutex(true, "Local\\DshCountdownWatchdog2027", out created)) {
            if (!created) return;
            var exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Countdown.exe");
            var pending = exe + ".pending";
            while (File.Exists(exe) || File.Exists(pending)) {
                try {
                    bool running = IsWidgetRunning();
                    if (!running) {
                        PromotePending(exe, pending);
                        if (File.Exists(exe)) {
                            using (var p = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false })) { }
                        }
                    }
                } catch (Exception ex) {
                    try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(exe), "watchdog_error.log"), DateTime.Now + " " + ex.Message + Environment.NewLine); } catch { }
                }
                Thread.Sleep(1500);
            }
        }
    }
}
