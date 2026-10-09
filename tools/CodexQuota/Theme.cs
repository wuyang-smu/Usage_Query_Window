using System;
using System.Collections.Generic;
using System.Windows.Media;

public static class Theme
{
    public static readonly string[] Keys = { "quotaGreen", "quotaYellow", "quotaOrange", "quotaRed", "timeGreen", "timeYellow", "timeOrange", "timeRed", "title", "label", "valueText", "timeText", "caption", "status", "alert", "ticks", "barBorder", "windowBorder", "track", "background", "icons", "weekMarker", "failure" };
    public static readonly string[] Chinese = { "额度 · ≥30%", "额度 · 20%–<30%", "额度 · 10%–<20%", "额度 · <10%", "时间 · ≤20%", "时间 · >20%–40%", "时间 · >40%–60%", "时间 · >60%", "窗口标题", "标签 / 分隔符（含 5h/Wk）", "展开 · 百分比文字", "展开 · 倒计时文字", "收起 · 条内数值文字", "状态说明", "提醒文字", "刻度", "条形外框", "窗口外框", "空条底色", "窗口背景", "工具栏图标", "周参考箭头", "刷新失败提示" };
    public static readonly string[] English = { "Quota · ≥30%", "Quota · 20%–<30%", "Quota · 10%–<20%", "Quota · <10%", "Time · ≤20%", "Time · >20%–40%", "Time · >40%–60%", "Time · >60%", "Window title", "Labels / separators (5h/Wk)", "Expanded · percentage", "Expanded · countdown", "Compact · value caption", "Status text", "Alert text", "Ticks", "Bar outline", "Window outline", "Empty track", "Window background", "Toolbar icons", "Weekly reference arrow", "Refresh failure" };
    public static Dictionary<string,string> Colors = Defaults();
    public static Dictionary<string,string> Defaults() {
        string[] values = { "#438A70", "#C3A356", "#C17D4D", "#B95F6A", "#438A70", "#C3A356", "#C17D4D", "#B95F6A", "#F2F4F7", "#F2F4F7", "#F2F4F7", "#F2F4F7", "#F2F4F7", "#AAB3C2", "#C17D4D", "#15181E", "#77859B", "#444C59", "#414B5B", "#20242C", "#AAB3C2", "#70B8FF", "#B95F6A" };
        var result = new Dictionary<string,string>();
        for (int i=0;i<Keys.Length;i++) result[Keys[i]]=values[i];
        return result;
    }
    public static Dictionary<string,string> Preset(int index) {
        var result=Defaults();
        bool light=index==3;
        string[] fills=light ? new[]{"#A8DCC0","#EAD595","#EDBD98","#E4ADB5"}
            : index==1 ? new[]{"#285AA6","#755A18","#84421B","#862E40"}
            : index==2 ? new[]{"#805B2E","#7C6A04","#8A4824","#824958"}
            : new[]{"#356952","#71603A","#805335","#824958"};
        string[] tones={"Green","Yellow","Orange","Red"};
        for(int i=0;i<4;i++) {result["quota"+tones[i]]=fills[i];result["time"+tones[i]]=fills[i];}
        string text=light ? "#18212B" : index==2 ? "#F7F2E8" : "#F2F4F7";
        foreach(string key in new[]{"title","label","valueText","timeText","caption"}) result[key]=text;
        result["background"]=light ? "#F3F5F7" : index==1 ? "#111D32" : index==2 ? "#2B241D" : "#20242C";
        result["track"]=light ? "#D4DCE5" : index==2 ? "#48443E" : index==1 ? "#344559" : "#414B5B";
        result["barBorder"]=light ? "#64758A" : index==2 ? "#998F80" : "#8795A8";
        result["windowBorder"]=light ? "#A2ADBA" : index==2 ? "#655E54" : "#525C69";
        result["icons"]=result["status"]=light ? "#4B596A" : index==2 ? "#CEC5B7" : "#C6CFDB";
        result["ticks"]=light ? "#324153" : "#0B1017";
        result["alert"]=light ? "#8B3D00" : "#FFB580";
        result["weekMarker"]=light ? "#005FB8" : index==2 ? "#9CBFDF" : index==1 ? "#8BD3FF" : "#70B8FF";
        result["failure"]=light ? "#A52239" : "#FF8794";
        return result;
    }
    public static string PresetName(int index,bool english) {
        return (english ? new[]{"Graphite mint","Midnight blue","Warm amber","Light teal"} : new[]{"石墨薄荷","午夜蓝","暖琥珀","浅雾青绿"})[index];
    }
    public static Brush Brush(string key) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(Colors[key])); }
    public static bool Valid(string value) {
        if (value == null || (value.Length != 7 && value.Length != 9) || value[0] != '#') return false;
        for (int i=1;i<value.Length;i++) if (!Uri.IsHexDigit(value[i])) return false;
        return true;
    }
}
