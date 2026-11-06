using System;
using CountdownWidget;
class Tests {
    static int count;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); count++; }
    static void Main() {
        var now = new DateTime(2026, 11, 5, 23, 30, 0);
        Check(Program.ParseDate("2027-1-7 00:00") == new DateTime(2027, 1, 7), "legacy single-digit month");
        Check(Program.ParseDate("2027-02-30 00:00") == null, "invalid date");
        Check(Program.ParseDate("") == null, "empty date");
        Check(Program.DayText(now.AddMinutes(45), now, false) == "1", "tomorrow calendar day");
        Check(Program.DayText(now.AddMinutes(45), now, true) == "0", "partial 24-hour day");
        Check(Program.DayText(now.AddMinutes(10), now, false) == "今天", "same day");
        Check(Program.DayText(now, now, false) == "已到期", "exact expiry");
        Check(Program.DayText(now.AddDays(-1), now, true) == "已到期", "past expiry");
        Console.WriteLine(count + " checks passed.");
    }
}
