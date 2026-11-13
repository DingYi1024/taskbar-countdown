using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using CountdownWidget;
class Tests {
    static int count;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); count++; }
    [STAThread] static void Main() {
        var now = new DateTime(2026, 11, 5, 23, 30, 0);
        Check(Program.ParseDate("2027-1-16 00:00") == new DateTime(2027, 1, 16), "legacy single-digit month");
        Check(Program.ParseDate("2027/1/16") == new DateTime(2027, 1, 16), "slash date");
        Check(Program.ParseDate("2027-02-30 00:00") == null, "invalid date");
        Check(Program.ParseDate("") == null, "empty date");
        Check(Program.ParseDate(null) == null, "null date");
        Check(Program.DayText(now.AddMinutes(45), now, false) == "1", "tomorrow calendar day");
        Check(Program.DayText(now.AddMinutes(45), now, true) == "0", "partial 24-hour day");
        Check(Program.DayText(now.AddMinutes(10), now, false) == "今天", "same day");
        Check(Program.DayText(now, now, false) == "已到期", "exact expiry");
        Check(Program.DayText(now.AddDays(-1), now, true) == "已到期", "past expiry");
        var def = new Cfg();
        Check(def.motion && def.glass && def.display_mode == "expand" && def.rotate_seconds == 4, "config defaults");
        var legacy = new JavaScriptSerializer().Deserialize<Cfg>("{\"theme\":\"dark\"}");
        Check(legacy.motion && legacy.theme == "dark", "legacy config preserves motion default");
        Check(Program.FestivalForName("圣诞") == "christmas", "festival christmas");
        Check(Program.FestivalForName("2027元旦") == "newyear", "festival newyear");
        Check(Program.FestivalForName("春节") == "spring", "festival spring");
        Check(Program.FestivalForName("中秋节") == "midautumn", "festival midautumn");
        Check(Program.FestivalForName("国庆") == "national", "festival national");
        Check(Program.FestivalForName("寒假") == "default", "festival default");

        // Verify share card rendering across templates, single/list modes, and all 3 aspect ratios
        var sampleEntries = new List<Program.ShareEntry> {
            new Program.ShareEntry { Name = "2027元旦", Date = new DateTime(2027, 1, 1), Enabled = true },
            new Program.ShareEntry { Name = "春节", Date = new DateTime(2027, 2, 6), Enabled = true },
            new Program.ShareEntry { Name = "寒假", Date = new DateTime(2027, 1, 16), Enabled = true }
        };
        int[,] expectedDims = { { 1080, 1440 }, { 1080, 1080 }, { 1600, 900 } };
        for (int t = 0; t < 3; t++) {
            for (int r = 0; r < 3; r++) {
                var bmpSingle = Program.RenderShareBitmap(sampleEntries, new Program.CardOpts { IsList = false, EntryIndex = 1, TemplateIndex = t, RatioIndex = r, SkinIndex = 3, Quote = "安静地记录时间，认真地期待下一次相逢。", ShowDate = true, ShowWatermark = true }, "christmas");
                Check(bmpSingle.PixelWidth == expectedDims[r, 0] && bmpSingle.PixelHeight == expectedDims[r, 1], "single share card t=" + t + " r=" + r);
                var bmpList = Program.RenderShareBitmap(sampleEntries, new Program.CardOpts { IsList = true, EntryIndex = 0, TemplateIndex = t, RatioIndex = r, SkinIndex = 5, Quote = "静候相逢", ShowDate = true, ShowWatermark = false }, "spring");
                Check(bmpList.PixelWidth == expectedDims[r, 0] && bmpList.PixelHeight == expectedDims[r, 1], "list share card t=" + t + " r=" + r);
            }
        }
        foreach (int newSkin in new[] { 8, 9, 10 }) {
            var bmpNew = Program.RenderShareBitmap(sampleEntries, new Program.CardOpts { IsList = true, EntryIndex = 0, TemplateIndex = 0, RatioIndex = 0, SkinIndex = newSkin, Quote = "静候相逢", ShowDate = true, ShowWatermark = true }, "default");
            Check(bmpNew.PixelWidth == 1080 && bmpNew.PixelHeight == 1440, "new skin share card s=" + newSkin);
        }

        // Verify DWM anti-flicker attributes
        Check(Program.DWMWA_TRANSITIONS_FORCEDISABLED == 3, "dwm transitions forcedisabled attr");
        Check(Program.DWMWA_DISALLOW_PEEK == 11, "dwm disallow peek attr");
        Check(Program.DWMWA_EXCLUDED_FROM_PEEK == 12, "dwm excluded from peek attr");

        Console.WriteLine(count + " checks passed.");
    }
}
