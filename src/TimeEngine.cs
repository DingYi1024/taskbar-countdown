using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace CountdownWidget
{
    public class CfgItem
    {
        public string name { get; set; }
        public string date { get; set; }
        public bool enabled { get; set; }
        public string kind { get; set; }       // "countdown" | "countup"
        public string repeat { get; set; }     // "none" | "yearly" | "monthly" | "weekly"
        public string calendar { get; set; }   // "solar" | "lunar"
        public int lunar_month { get; set; }   // 1..12
        public int lunar_day { get; set; }     // 1..30
        public bool lunar_leap { get; set; }
        public string start_date { get; set; } // optional yyyy-MM-dd for real progress bar
        public string icon { get; set; }       // "auto" or explicit icon id
        public string note { get; set; }       // optional per-item quote/note
        public bool archived { get; set; }

        public CfgItem()
        {
            enabled = true;
            kind = "countdown";
            repeat = "none";
            calendar = "solar";
            icon = "auto";
        }
    }

    public class Cfg
    {
        public string display_mode { get; set; }   // "single" | "dual" | "expand" | "rotate"
        public int rotate_seconds { get; set; }
        public bool glass { get; set; }
        public bool show_seconds { get; set; }
        public string theme { get; set; }          // "system" | "light" | "dark"
        public string skin { get; set; }
        public bool motion { get; set; }
        public string anchor { get; set; }         // "free" | "left" | "right"
        public bool hide_fullscreen { get; set; }
        public string click_action { get; set; }   // "flyout" | "seconds"
        public bool show_progress_bar { get; set; }
        public string font_style { get; set; }     // "modern" | "editorial" | "mono"
        public bool show_tray_icon { get; set; }
        public string sort_mode { get; set; }      // "smart" | "manual"
        public string pinned_name { get; set; }
        public List<CfgItem> items { get; set; }

        public Cfg()
        {
            display_mode = "expand";
            rotate_seconds = 4;
            glass = true;
            theme = "system";
            skin = "default";
            motion = true;
            anchor = "free";
            hide_fullscreen = true;
            click_action = "flyout";
            show_progress_bar = true;
            font_style = "modern";
            show_tray_icon = true;
            sort_mode = "smart";
            items = new List<CfgItem>();
        }
    }

    public sealed class ResolvedItem
    {
        public CfgItem Source;
        public string Name;
        public DateTime TargetDate;
        public DateTime CycleStartDate;
        public bool IsCountup;
        public bool IsLunar;
        public bool IsRecurring;
        public bool IsToday;
        public bool IsExpired;
        public int DaysValue;
        public double Progress;
        public string LunarDisplay;
        public string RepeatLabel;
        public string ResolvedIcon;
    }

    public static class LunarHelper
    {
        static readonly ChineseLunisolarCalendar Cal = new ChineseLunisolarCalendar();
        static readonly string[] MonthNames = { "", "正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "冬月", "腊月" };
        static readonly string[] DayNames = {
            "", "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
            "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
            "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十"
        };
        static readonly string[] Stems = { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };
        static readonly string[] Branches = { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };

        public static DateTime? LunarToSolar(int lunarYear, int lunarMonth, int lunarDay, bool isLeap)
        {
            try
            {
                if (lunarYear < Cal.MinSupportedDateTime.Year + 1 || lunarYear > Cal.MaxSupportedDateTime.Year - 1) return null;
                lunarMonth = Math.Max(1, Math.Min(12, lunarMonth));
                int leapMonth = Cal.GetLeapMonth(lunarYear);
                int rawMonth = lunarMonth;
                if (leapMonth > 0)
                {
                    if (isLeap && lunarMonth + 1 == leapMonth) rawMonth = leapMonth;
                    else if (lunarMonth >= leapMonth) rawMonth = lunarMonth + 1;
                }
                int maxDay = Cal.GetDaysInMonth(lunarYear, rawMonth);
                int safeDay = Math.Max(1, Math.Min(maxDay, lunarDay));
                return Cal.ToDateTime(lunarYear, rawMonth, safeDay, 0, 0, 0, 0);
            }
            catch
            {
                return null;
            }
        }

        public static void SolarToLunar(DateTime solar, out int lunarYear, out int lunarMonth, out int lunarDay, out bool isLeap)
        {
            lunarYear = solar.Year;
            lunarMonth = solar.Month;
            lunarDay = solar.Day;
            isLeap = false;
            try
            {
                if (solar < Cal.MinSupportedDateTime || solar > Cal.MaxSupportedDateTime) return;
                lunarYear = Cal.GetYear(solar);
                int rawMonth = Cal.GetMonth(solar);
                lunarDay = Cal.GetDayOfMonth(solar);
                int leapMonth = Cal.GetLeapMonth(lunarYear);
                if (leapMonth > 0)
                {
                    if (rawMonth == leapMonth)
                    {
                        isLeap = true;
                        lunarMonth = rawMonth - 1;
                    }
                    else if (rawMonth > leapMonth)
                    {
                        lunarMonth = rawMonth - 1;
                    }
                    else
                    {
                        lunarMonth = rawMonth;
                    }
                }
                else
                {
                    lunarMonth = rawMonth;
                }
            }
            catch { }
        }

        public static string FormatLunarMonthDay(int lunarMonth, int lunarDay, bool isLeap)
        {
            int m = Math.Max(1, Math.Min(12, lunarMonth));
            int d = Math.Max(1, Math.Min(30, lunarDay));
            return (isLeap ? "闰" : "") + MonthNames[m] + DayNames[d];
        }

        public static string SolarToShortLunarCell(DateTime solar)
        {
            int ly, lm, ld;
            bool leap;
            SolarToLunar(solar, out ly, out lm, out ld, out leap);
            if (!leap)
            {
                if (lm == 1 && ld == 1) return "春节";
                if (lm == 1 && ld == 15) return "元宵";
                if (lm == 5 && ld == 5) return "端午";
                if (lm == 7 && ld == 7) return "七夕";
                if (lm == 8 && ld == 15) return "中秋";
                if (lm == 9 && ld == 9) return "重阳";
            }
            if (ld == 1) return (leap ? "闰" : "") + MonthNames[Math.Max(1, Math.Min(12, lm))];
            return DayNames[Math.Max(1, Math.Min(30, ld))];
        }

        public static string SolarToFullLunar(DateTime solar)
        {
            int ly, lm, ld;
            bool leap;
            SolarToLunar(solar, out ly, out lm, out ld, out leap);
            int idx = (ly - 4) % 60;
            if (idx < 0) idx += 60;
            string gz = Stems[idx % 10] + Branches[idx % 12];
            return gz + "年 " + FormatLunarMonthDay(lm, ld, leap);
        }

        public static DateTime NextLunarOccurrence(int lunarMonth, int lunarDay, bool isLeap, DateTime now, TimeSpan timeOfDay, out DateTime prevOccurrence)
        {
            int baseYear = now.Year;
            DateTime? prev = null;
            for (int y = baseYear - 1; y <= baseYear + 2; y++)
            {
                var d = LunarToSolar(y, lunarMonth, lunarDay, isLeap);
                if (!d.HasValue && isLeap) d = LunarToSolar(y, lunarMonth, lunarDay, false);
                if (!d.HasValue) continue;
                DateTime candidate = d.Value.Date.Add(timeOfDay);
                if (candidate.Date >= now.Date)
                {
                    prevOccurrence = prev.HasValue ? prev.Value : candidate.AddDays(-354);
                    return candidate;
                }
                prev = candidate;
            }
            prevOccurrence = now.Date.AddDays(-354);
            return now.Date.AddDays(30).Add(timeOfDay);
        }

        public static DateTime NextChineseNewYearsEve(DateTime now)
        {
            DateTime prev;
            DateTime cny = NextLunarOccurrence(1, 1, false, now.AddDays(1), TimeSpan.Zero, out prev);
            DateTime eve = cny.AddDays(-1);
            if (eve.Date < now.Date)
            {
                cny = NextLunarOccurrence(1, 1, false, cny.AddDays(2), TimeSpan.Zero, out prev);
                eve = cny.AddDays(-1);
            }
            return eve;
        }
    }

    public static partial class Program
    {
        public static string AutoIconForName(string name, string kind)
        {
            var n = name ?? "";
            if (n.Contains("圣诞")) return "tree";
            if (n.Contains("春节") || n.Contains("过年") || n.Contains("除夕") || n.Contains("元宵")) return "lantern";
            if (n.Contains("中秋") || n.Contains("月")) return "moon";
            if (n.Contains("国庆")) return "flag";
            if (n.Contains("元旦") || n.Contains("新年") || n.Contains("跨年")) return "sparkle";
            if (n.Contains("生日") || n.Contains("诞辰")) return "cake";
            if (n.Contains("旅行") || n.Contains("旅游") || n.Contains("出发") || n.Contains("机票") || n.Contains("航班") || n.Contains("度假") || n.Contains("寒假") || n.Contains("暑假") || n.Contains("放假")) return "plane";
            if (n.Contains("恋爱") || n.Contains("结婚") || n.Contains("周年") || n.Contains("相爱") || n.Contains("纪念") || n.Contains("情人") || n.Contains("七夕")) return "heart";
            if (n.Contains("发薪") || n.Contains("工资") || n.Contains("奖金") || n.Contains("年终")) return "coin";
            if (n.Contains("下班") || n.Contains("周末") || n.Contains("入职") || n.Contains("工作") || n.Contains("项目") || n.Contains("发布")) return "briefcase";
            if (n.Contains("考试") || n.Contains("考研") || n.Contains("高考") || n.Contains("毕业") || n.Contains("答辩") || n.Contains("开学")) return "book";
            if (n.Contains("演唱会") || n.Contains("音乐") || n.Contains("演出") || n.Contains("电影")) return "music";
            if (n.Contains("回家") || n.Contains("搬家") || n.Contains("交房")) return "home";
            if (string.Equals(kind, "countup", StringComparison.OrdinalIgnoreCase)) return "heart";
            return "star";
        }

        static DateTime SafeDate(int year, int month, int day, TimeSpan tod)
        {
            int maxDay = DateTime.DaysInMonth(year, month);
            return new DateTime(year, month, Math.Min(day, maxDay)).Add(tod);
        }

        public static ResolvedItem ResolveItem(CfgItem item, DateTime now)
        {
            if (item == null) return null;
            DateTime? parsed = ParseDate(item.date);
            bool isLunar = string.Equals(item.calendar, "lunar", StringComparison.OrdinalIgnoreCase) && item.lunar_month >= 1 && item.lunar_month <= 12 && item.lunar_day >= 1 && item.lunar_day <= 30;
            bool isCountup = string.Equals(item.kind, "countup", StringComparison.OrdinalIgnoreCase);
            string rep = (item.repeat ?? "none").ToLowerInvariant();
            if (isCountup) rep = "none";

            TimeSpan tod = parsed.HasValue ? parsed.Value.TimeOfDay : TimeSpan.Zero;
            DateTime target;
            DateTime cycleStart;
            string lunarDisp = null;

            if (isLunar)
            {
                lunarDisp = "农历 " + LunarHelper.FormatLunarMonthDay(item.lunar_month, item.lunar_day, item.lunar_leap);
                if (rep == "yearly")
                {
                    target = LunarHelper.NextLunarOccurrence(item.lunar_month, item.lunar_day, item.lunar_leap, now, tod, out cycleStart);
                }
                else
                {
                    int targetYear = parsed.HasValue ? parsed.Value.Year : now.Year;
                    var solar = LunarHelper.LunarToSolar(targetYear, item.lunar_month, item.lunar_day, item.lunar_leap);
                    if (solar.HasValue)
                    {
                        target = solar.Value.Date.Add(tod);
                        cycleStart = target.AddDays(-354);
                    }
                    else
                    {
                        target = LunarHelper.NextLunarOccurrence(item.lunar_month, item.lunar_day, item.lunar_leap, now, tod, out cycleStart);
                    }
                }
            }
            else
            {
                if (!parsed.HasValue) return null;
                target = parsed.Value;
                cycleStart = new DateTime(now.Year, 1, 1);

                if (rep == "yearly")
                {
                    DateTime cand = SafeDate(now.Year, target.Month, target.Day, tod);
                    if (cand.Date < now.Date) cand = SafeDate(now.Year + 1, target.Month, target.Day, tod);
                    target = cand;
                    cycleStart = SafeDate(target.Year - 1, target.Month, target.Day, tod);
                }
                else if (rep == "monthly")
                {
                    DateTime cand = SafeDate(now.Year, now.Month, target.Day, tod);
                    if (cand.Date < now.Date)
                    {
                        DateTime nextMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1);
                        cand = SafeDate(nextMonth.Year, nextMonth.Month, target.Day, tod);
                    }
                    target = cand;
                    DateTime prevMonth = new DateTime(target.Year, target.Month, 1).AddMonths(-1);
                    cycleStart = SafeDate(prevMonth.Year, prevMonth.Month, parsed.Value.Day, tod);
                }
                else if (rep == "weekly")
                {
                    int diff = ((int)target.DayOfWeek - (int)now.DayOfWeek + 7) % 7;
                    DateTime cand = now.Date.AddDays(diff).Add(tod);
                    if (diff == 0 && cand <= now) cand = cand.AddDays(7);
                    target = cand;
                    cycleStart = target.AddDays(-7);
                }
                else
                {
                    DateTime? customStart = ParseDate(item.start_date);
                    if (customStart.HasValue && customStart.Value < target)
                    {
                        cycleStart = customStart.Value;
                    }
                    else
                    {
                        DateTime jan1 = new DateTime(now.Year, 1, 1);
                        cycleStart = jan1 < target ? jan1 : target.AddDays(-90);
                    }
                }
            }

            if (rep != "none")
            {
                DateTime? customStart = ParseDate(item.start_date);
                if (customStart.HasValue && customStart.Value < target && customStart.Value > cycleStart)
                    cycleStart = customStart.Value;
            }

            bool isToday = !isCountup && target.Date == now.Date && (target > now || tod == TimeSpan.Zero);
            bool isExpired = !isCountup && !isToday && target <= now;
            int daysVal;
            double progress;

            if (isCountup)
            {
                daysVal = Math.Max(1, (now.Date - target.Date).Days + 1);
                double modYear = ((now.Date - target.Date).Days % 365 + 365) % 365;
                progress = Math.Max(0.05, Math.Min(1.0, modYear / 365.0));
            }
            else if (isToday || isExpired)
            {
                daysVal = 0;
                progress = 1.0;
            }
            else
            {
                daysVal = Math.Max(0, (target.Date - now.Date).Days);
                double totalSpan = Math.Max(1.0, (target - cycleStart).TotalDays);
                double elapsed = Math.Max(0.0, (now - cycleStart).TotalDays);
                progress = Math.Max(0.04, Math.Min(0.98, elapsed / totalSpan));
            }

            string repeatLbl = rep == "yearly" ? "每年重复"
                : rep == "monthly" ? "每月重复"
                : rep == "weekly" ? "每周重复"
                : isCountup ? "纪念正数" : "";

            string iconId = string.IsNullOrEmpty(item.icon) || item.icon == "auto"
                ? AutoIconForName(item.name, item.kind)
                : item.icon;

            return new ResolvedItem
            {
                Source = item,
                Name = string.IsNullOrWhiteSpace(item.name) ? "倒计时" : item.name.Trim(),
                TargetDate = target,
                CycleStartDate = cycleStart,
                IsCountup = isCountup,
                IsLunar = isLunar,
                IsRecurring = rep != "none",
                IsToday = isToday,
                IsExpired = isExpired,
                DaysValue = daysVal,
                Progress = progress,
                LunarDisplay = lunarDisp,
                RepeatLabel = repeatLbl,
                ResolvedIcon = iconId
            };
        }

        public static string FormatResolvedShortValue(ResolvedItem r, DateTime now, bool detailed, out string suffix, out bool isNumeric)
        {
            if (r.IsCountup)
            {
                isNumeric = true;
                if (detailed)
                {
                    TimeSpan span = now > r.TargetDate ? (now - r.TargetDate) : TimeSpan.Zero;
                    suffix = "天  " + span.ToString(@"hh\:mm\:ss");
                    return ((int)span.TotalDays).ToString(CultureInfo.InvariantCulture);
                }
                suffix = "天";
                return r.DaysValue.ToString(CultureInfo.InvariantCulture);
            }
            if (r.IsToday)
            {
                if (detailed && r.TargetDate > now)
                {
                    isNumeric = true;
                    suffix = "今天  " + (r.TargetDate - now).ToString(@"hh\:mm\:ss");
                    return "0";
                }
                isNumeric = false;
                suffix = "";
                return "就是今天";
            }
            if (r.IsExpired)
            {
                isNumeric = false;
                suffix = "";
                return "已抵达";
            }
            isNumeric = true;
            if (detailed)
            {
                TimeSpan rem = r.TargetDate - now;
                suffix = "天  " + rem.ToString(@"hh\:mm\:ss");
                return ((int)rem.TotalDays).ToString(CultureInfo.InvariantCulture);
            }
            suffix = "天";
            return r.DaysValue.ToString(CultureInfo.InvariantCulture);
        }

        public static readonly string[] LiteraryQuotes = {
            "安静地记录时间，认真地期待下一次相逢。",
            "所有值得等待的事物，都配得上慢慢走来。",
            "把琐碎的日子过成节拍，让远方有光可循。",
            "念念不忘的远期，终将在某个清晨如约抵达。",
            "时间从不喧哗，只是悄悄把期待酿成重逢。",
            "愿你奔赴的下一场山海，皆是心之所向。",
            "每一页翻过的日历，都是向好消息靠近的脚步。",
            "在寻常的烟火里，藏一份闪闪发光的盼头。",
            "日子很长，幸好总有几个值得圈起来的日子。",
            "慢一点也没关系，好事情正踩着星光赶来。",
            "把心动的日期写进时光，余下交给风和晴天。",
            "岁岁年年，总有新的欢喜在前方候场。",
            "山川异域，风月同天，静候花开与归期。",
            "凡是过往，皆为序章；凡是期盼，终将回响。",
            "生活需要一点仪式感，提醒我们正走在热爱里。",
            "不必催促季节，属于你的那一天自有它的时辰。",
            "心怀微光，细数朝夕，万事胜意。",
            "此刻的奔波，都在为下一次盛大的欢喜铺路。",
            "每一天都算数，每一次等待都自带回甘。",
            "把日子的棱角磨柔，把远方的期待擦亮。",
            "愿此去经年，所盼皆所得，所遇皆温柔。",
            "冬有冬的来意，雪有雪的秘密，重逢自有归期。",
            "灯火可亲，岁月可期，敬每一个奔赴的明天。",
            "世界喧嚣，幸好这里替你安静数着归期。",
            "把好日子提前挂在眼前，平凡的今天便有了温度。",
            "风会替你翻过旧页，把更好的章节递到手里。",
            "穿过绵长的日常，总有一束光在尽头等你。",
            "认真过好眼下的每一刻，就是对未来最好的赴约。",
            "日出有盼，日落有念，平平安安，所愿皆安。",
            "有些日子还没到来，光是想到它就已经很甜。",
            "我们在时间里赶路，也把最珍贵的日期留作路标。",
            "且听风吟，静待花信，一切美好都在路上。",
            "人间朝暮，叶落惊秋，唯有心底的期待常青。",
            "愿下一次出发时，行李箱装满轻盈与晴朗。",
            "把想见的人写进日程，距离便开始一天天变短。",
            "时间是最温柔的证人，记录每一次坚持与抵达。",
            "别慌，月亮也正在大海某处迷茫，但潮汐总会准时。",
            "所有的伏笔，都会在某个明朗的早晨如期兑现。",
            "以欢喜之心，慢度日常；以从容之态，静候佳期。",
            "日子一天天往前翻，我们在期待里慢慢变好。",
            "那些认真准备的时光，终将在终点闪闪发光。",
            "春风有信，花开有期，所有久别重逢都值得奔赴。",
            "愿你在忙碌的缝隙里，抬头就能看见盼望的光。",
            "把重要的人和事留在视线里，便不怕岁月匆忙。",
            "每一段倒数，都是给平淡生活写下的一封情书。",
            "走过的路都作数，即将抵达的风景更动人。",
            "山河远阔，人间烟火，愿你如约奔向心安处。",
            "时光不动声色，却把最温柔的礼物留在了前方。",
            "今天也在稳步靠近那个令你嘴角上扬的日子。",
            "长风破浪会有时，直挂云帆，静候佳音。",
            "把心事折成纸船，顺着日历漂向闪亮的彼岸。",
            "星河滚烫，人间理想，每一步都离梦想更近一点。",
            "不必焦虑终点多远，至少今天比昨天近了一整天。",
            "愿每一个被圈出的日子，最终都不负满怀热望。",
            "在日复一日的微光里，收藏属于自己的节日。",
            "岁月悠长，值得期待的事情，一件件都会发生。"
        };

        static int _quoteIdx = 0;
        public static string PickNextQuote(string current)
        {
            for (int i = 0; i < LiteraryQuotes.Length; i++)
            {
                _quoteIdx = (_quoteIdx + 1) % LiteraryQuotes.Length;
                if (!string.Equals(LiteraryQuotes[_quoteIdx], current, StringComparison.Ordinal))
                    return LiteraryQuotes[_quoteIdx];
            }
            return LiteraryQuotes[0];
        }

        // --- System Integration: Startup, Fullscreen Detection, Win11 DWM Materials ---

        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "TaskbarCountdown";

        static string[] LegacyStartupShortcutPaths()
        {
            string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return new[] {
                Path.Combine(startupDir, "TaskbarCountdown.lnk"),
                Path.Combine(startupDir, "Taskbar Countdown.lnk"),
                Path.Combine(startupDir, "2027 Countdown.lnk")
            };
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key != null && key.GetValue(RunValueName) != null) return true;
                }
                foreach (var p in LegacyStartupShortcutPaths())
                {
                    if (File.Exists(p)) return true;
                }
            }
            catch { }
            return false;
        }

        public static bool SetStartupEnabled(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            // Unified single-EXE self-hosted watchdog mode
                            string selfExe = Path.Combine(Dir, "Countdown.exe");
                            key.SetValue(RunValueName, "\"" + selfExe + "\" --watchdog");
                        }
                        else
                        {
                            key.DeleteValue(RunValueName, false);
                        }
                    }
                }
                foreach (var legacy in LegacyStartupShortcutPaths())
                {
                    if (File.Exists(legacy)) { try { File.Delete(legacy); } catch { } }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int pquns);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        [DllImport("user32.dll")] static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        static bool IsFullscreenAppActive(IntPtr selfHwnd, IntPtr trayHwnd)
        {
            try
            {
                int state;
                if (SHQueryUserNotificationState(out state) == 0)
                {
                    // 3 = QUNS_RUNNING_D3D_FULL_SCREEN, 4 = QUNS_PRESENTATION_MODE
                    // Note: Do NOT blindly hide on QUNS_BUSY (2), which can fire for secondary-monitor
                    // fullscreen or Focus/DND states; verify foreground rect on the taskbar's monitor below.
                    if (state == 3 || state == 4) return true;
                }
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero || fg == selfHwnd || fg == trayHwnd) return false;
                var cls = new System.Text.StringBuilder(64);
                GetClassName(fg, cls, 64);
                string c = cls.ToString();
                if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd" || c == "Windows.UI.Core.CoreWindow")
                    return false;
                RECT fr;
                if (!GetWindowRect(fg, out fr)) return false;
                IntPtr mon = MonitorFromWindow(fg, 2 /* MONITOR_DEFAULTTONEAREST */);
                if (mon == IntPtr.Zero) return false;
                if (trayHwnd != IntPtr.Zero)
                {
                    IntPtr trayMon = MonitorFromWindow(trayHwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
                    if (trayMon != IntPtr.Zero && mon != trayMon) return false;
                }
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (!GetMonitorInfo(mon, ref mi)) return false;
                return fr.Left <= mi.rcMonitor.Left && fr.Top <= mi.rcMonitor.Top
                    && fr.Right >= mi.rcMonitor.Right && fr.Bottom >= mi.rcMonitor.Bottom;
            }
            catch
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DWM_MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref DWM_MARGINS margins);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        // backdropType: 2 = Mica, 3 = Transient Acrylic, 4 = Tabbed Mica Alt
        public static bool ApplyWin11Backdrop(Window win, IntPtr hwnd, bool light, int backdropType)
        {
            if (hwnd == IntPtr.Zero) return false;
            try
            {
                var src = HwndSource.FromHwnd(hwnd);
                if (src != null && src.CompositionTarget != null)
                    src.CompositionTarget.BackgroundColor = Colors.Transparent;
                var m = new DWM_MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                DwmExtendFrameIntoClientArea(hwnd, ref m);

                int dark = light ? 0 : 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                int corner = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                int bd = backdropType;
                int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref bd, sizeof(int));
                return hr == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
