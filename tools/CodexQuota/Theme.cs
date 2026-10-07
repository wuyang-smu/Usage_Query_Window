using System;
using System.Collections.Generic;
using System.Windows.Media;

public static class Theme
{
    public static readonly string[] Keys = { "quotaGreen", "quotaYellow", "quotaOrange", "quotaRed", "timeGreen", "timeYellow", "timeOrange", "timeRed", "title", "label", "valueText", "timeText", "caption", "status", "alert", "ticks", "barBorder", "windowBorder", "track", "background", "icons" };
    public static readonly string[] Chinese = { "额度 · 绿色", "额度 · 黄色", "额度 · 橙色", "额度 · 红色", "时间 · 绿色", "时间 · 黄色", "时间 · 橙色", "时间 · 红色", "窗口标题", "行标题 / 分隔符", "百分比文字", "倒计时文字", "条内文字", "状态说明", "提醒文字", "刻度", "条形外框", "窗口外框", "空条底色", "窗口背景", "顶部图标" };
    public static readonly string[] English = { "Quota · green", "Quota · yellow", "Quota · orange", "Quota · red", "Time · green", "Time · yellow", "Time · orange", "Time · red", "Window title", "Row labels / separators", "Percentage text", "Countdown text", "Bar caption", "Status text", "Alert text", "Ticks", "Bar outline", "Window outline", "Empty track", "Window background", "Toolbar icons" };
    public static Dictionary<string,string> Colors = Defaults();
    public static Dictionary<string,string> Defaults() {
        string[] values = { "#237A57", "#AD8522", "#B65B20", "#B93649", "#237A57", "#AD8522", "#B65B20", "#B93649", "#F0F3FA", "#D3DAE8", "#F0F3FA", "#F0F3FA", "#FFFFFF", "#96A3BC", "#FF9D66", "#111722", "#718096", "#47536B", "#354057", "#F21B2130", "#AAB5CA" };
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
