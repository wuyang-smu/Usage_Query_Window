using System;
using System.Collections.Generic;
using System.Windows.Media;

public static class Theme
{
    public static readonly string[] Keys = { "quotaGreen", "quotaYellow", "quotaOrange", "quotaRed", "timeGreen", "timeYellow", "timeOrange", "timeRed", "title", "label", "valueText", "timeText", "caption", "status", "alert", "ticks", "barBorder", "windowBorder", "track", "background", "icons" };
    public static readonly string[] Chinese = { "额度 · ≥30%", "额度 · 20%–<30%", "额度 · 10%–<20%", "额度 · <10%", "时间 · ≤20%", "时间 · >20%–40%", "时间 · >40%–60%", "时间 · >60%", "窗口标题", "标签 / 分隔符（含 5h/Wk）", "展开 · 百分比文字", "展开 · 倒计时文字", "收起 · 条内数值文字", "状态说明", "提醒文字", "刻度", "条形外框", "窗口外框", "空条底色", "窗口背景", "顶部图标" };
    public static readonly string[] English = { "Quota · ≥30%", "Quota · 20%–<30%", "Quota · 10%–<20%", "Quota · <10%", "Time · ≤20%", "Time · >20%–40%", "Time · >40%–60%", "Time · >60%", "Window title", "Labels / separators (5h/Wk)", "Expanded · percentage", "Expanded · countdown", "Compact · value caption", "Status text", "Alert text", "Ticks", "Bar outline", "Window outline", "Empty track", "Window background", "Toolbar icons" };
    public static Dictionary<string,string> Colors = Defaults();
    public static Dictionary<string,string> Defaults() {
        string[] values = { "#237A57", "#AD8522", "#B65B20", "#B93649", "#237A57", "#AD8522", "#B65B20", "#B93649", "#F0F3FA", "#F0F3FA", "#F0F3FA", "#F0F3FA", "#F0F3FA", "#96A3BC", "#FF9D66", "#111722", "#718096", "#47536B", "#354057", "#F21B2130", "#AAB5CA" };
        var result = new Dictionary<string,string>();
        for (int i=0;i<Keys.Length;i++) result[Keys[i]]=values[i];
        return result;
    }
    public static Brush Brush(string key) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(Colors[key])); }
    public static bool Valid(string value) {
        if (value == null || (value.Length != 7 && value.Length != 9) || value[0] != '#') return false;
        for (int i=1;i<value.Length;i++) if (!Uri.IsHexDigit(value[i])) return false;
        return true;
    }
}
