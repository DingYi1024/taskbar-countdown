using System;
using System.Collections.Generic;
using System.Linq;
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
        Check(def.hide_fullscreen && def.show_progress_bar && def.click_action == "flyout", "2.0 config defaults");
        var legacy = new JavaScriptSerializer().Deserialize<Cfg>("{\"theme\":\"dark\"}");
        Check(legacy.motion && legacy.theme == "dark" && legacy.hide_fullscreen && legacy.click_action == "flyout", "legacy config preserves 2.0 defaults");

        Check(Program.FestivalForName("圣诞") == "christmas", "festival christmas");
        Check(Program.FestivalForName("2027元旦") == "newyear", "festival newyear");
        Check(Program.FestivalForName("春节") == "spring", "festival spring");
        Check(Program.FestivalForName("中秋节") == "midautumn", "festival midautumn");
        Check(Program.FestivalForName("国庆") == "national", "festival national");
        Check(Program.FestivalForName("寒假") == "default", "festival default");

        // Verify 2.0 Native Lunar Engine (zero external dependency)
        Check(LunarHelper.LunarToSolar(2027, 1, 1, false) == new DateTime(2027, 2, 6), "2027 Spring Festival solar date");
        Check(LunarHelper.LunarToSolar(2028, 1, 1, false) == new DateTime(2028, 1, 26), "2028 Spring Festival solar date");
        Check(LunarHelper.LunarToSolar(2027, 8, 15, false) == new DateTime(2027, 9, 15), "2027 Mid-Autumn solar date");
        Check(LunarHelper.SolarToShortLunarCell(new DateTime(2027, 2, 6)) == "春节", "lunar short cell春节");
        Check(LunarHelper.FormatLunarMonthDay(8, 15, false) == "八月十五", "lunar format 八月十五");

        // Verify 2.0 TimeEngine: Lunar Yearly Recurrence, Solar Monthly Recurrence, Count-up Anniversary, Auto Icons
        var lunarSpring = new CfgItem { name = "春节", date = "2026-01-01", calendar = "lunar", lunar_month = 1, lunar_day = 1, repeat = "yearly", enabled = true };
        var resSpring = Program.ResolveItem(lunarSpring, new DateTime(2026, 11, 21));
        Check(resSpring != null && resSpring.TargetDate.Date == new DateTime(2027, 2, 6) && resSpring.IsLunar && resSpring.IsRecurring, "resolve lunar yearly spring");
        Check(resSpring.Progress > 0.5 && resSpring.Progress < 0.95, "real cycle progress for yearly lunar");

        var payday = new CfgItem { name = "发薪日", date = "2026-01-10", repeat = "monthly", enabled = true };
        var resPay = Program.ResolveItem(payday, new DateTime(2026, 11, 21));
        Check(resPay != null && resPay.TargetDate.Date == new DateTime(2026, 12, 10) && resPay.ResolvedIcon == "coin", "monthly recurrence & coin icon");

        var anniv = new CfgItem { name = "恋爱纪念日", date = "2026-01-01", kind = "countup", enabled = true };
        var resAnniv = Program.ResolveItem(anniv, new DateTime(2026, 1, 10));
        Check(resAnniv != null && resAnniv.IsCountup && resAnniv.DaysValue == 10 && resAnniv.ResolvedIcon == "heart", "countup days calculation & heart icon");

        var todayItem = new CfgItem { name = "生日", date = "2026-11-21 00:00:00", enabled = true };
        var resToday = Program.ResolveItem(todayItem, new DateTime(2026, 11, 21, 15, 0, 0));
        Check(resToday != null && resToday.IsToday && !resToday.IsExpired && resToday.ResolvedIcon == "cake", "Day 0 arrival preserved all day & cake icon");

        // Verify 13-skin catalog including new 2.0 luxury skins
        Check(Program.SkinCatalog.Length == 13, "13 artisanal skins in catalog");
        Check(Program.FindSkin("xuanpaper").Id == "xuanpaper", "skin xuanpaper");
        Check(Program.FindSkin("vinyl").Id == "vinyl", "skin vinyl");
        Check(Program.FindSkin("monet").Id == "monet", "skin monet");

        // Verify share card rendering across ALL 5 templates (Editorial, Atmosphere, Ticket, SwissGrid, Polaroid) & ALL 4 aspect ratios (3:4, 1:1, 16:9, 9:19.5 Lockscreen)
        var sampleEntries = new List<Program.ShareEntry> {
            new Program.ShareEntry { Name = "2027元旦", Date = new DateTime(2027, 1, 1), Enabled = true },
            new Program.ShareEntry { Name = "春节", Date = new DateTime(2027, 2, 6), Enabled = true, LunarLabel = "农历 正月初一" },
            new Program.ShareEntry { Name = "寒假", Date = new DateTime(2027, 1, 16), Enabled = true }
        };
        int[,] expectedDims = { { 1080, 1440 }, { 1080, 1080 }, { 1600, 900 }, { 1080, 2340 } };
        for (int t = 0; t < 6; t++) {
            for (int r = 0; r < 4; r++) {
                var bmpSingle = Program.RenderShareBitmap(sampleEntries, new Program.CardOpts { IsList = false, EntryIndex = 1, TemplateIndex = t, RatioIndex = r, Quote = "安静地记录时间，认真地期待下一次相逢。", ShowDate = true, ShowWatermark = true }, "christmas");
                Check(bmpSingle.PixelWidth == expectedDims[r, 0] && bmpSingle.PixelHeight == expectedDims[r, 1], "single share card t=" + t + " r=" + r);
                var bmpList = Program.RenderShareBitmap(sampleEntries, new Program.CardOpts { IsList = true, EntryIndex = 0, TemplateIndex = t, RatioIndex = r, Quote = "所有值得等待的事物，都配得上慢慢走来。", ShowDate = true, ShowWatermark = true }, "spring");
                Check(bmpList.PixelWidth == expectedDims[r, 0] && bmpList.PixelHeight == expectedDims[r, 1], "list share card t=" + t + " r=" + r);
            }
        }

        // Verify DWM anti-flicker & Win11 Backdrop attributes
        Check(Program.DWMWA_TRANSITIONS_FORCEDISABLED == 3, "dwm transitions forcedisabled attr");
        Check(Program.DWMWA_DISALLOW_PEEK == 11, "dwm disallow peek attr");
        Check(Program.DWMWA_EXCLUDED_FROM_PEEK == 12, "dwm excluded from peek attr");
        Check(Program.DWMWA_USE_IMMERSIVE_DARK_MODE == 20, "dwm immersive dark mode attr");
        Check(Program.DWMWA_WINDOW_CORNER_PREFERENCE == 33, "dwm window corner attr");
        Check(Program.DWMWA_SYSTEMBACKDROP_TYPE == 38, "dwm system backdrop attr");

        // Verify 50+ unique literary quotes, tray icon default, and manual vs smart sort
        Check(Program.LiteraryQuotes.Length >= 50, "50+ literary quotes library");
        Check(Program.LiteraryQuotes.Distinct().Count() == Program.LiteraryQuotes.Length, "all literary quotes unique");
        Check(def.show_tray_icon && def.sort_mode == "smart", "tray icon and sort_mode defaults");
        Check(legacy.show_tray_icon && legacy.sort_mode == "smart", "legacy config preserves tray & sort defaults");

        var testCfg = new Cfg {
            sort_mode = "manual",
            items = new List<CfgItem> {
                new CfgItem { name = "远期日程", date = "2027-10-01 00:00:00", enabled = true },
                new CfgItem { name = "近期日程", date = "2026-12-01 00:00:00", enabled = true }
            }
        };
        var cfgField = typeof(Program).GetField("Config", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        cfgField.SetValue(null, testCfg);
        var manualOrder = Program.GetResolvedItems(now);
        Check(manualOrder.Count == 2 && manualOrder[0].Name == "远期日程", "manual sort preserves drag order");
        testCfg.sort_mode = "smart";
        var smartOrder = Program.GetResolvedItems(now);
        Check(smartOrder.Count == 2 && smartOrder[0].Name == "近期日程", "smart sort orders by soonest");

        Console.WriteLine(count + " checks passed.");
    }
}
