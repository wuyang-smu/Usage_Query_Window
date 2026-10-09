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
        string[] values = { "#438A70", "#C3A356", "#C17D4D", "#B95F6A", "#438A70", "#C3A356", "#C17D4D", "#B95F6A", "#F2F4F7", "#F2F4F7", "#F2F4F7", "#F2F4F7", "#F2F4F7", "#AAB3C2", "#C17D4D", "#15181E", "#77859B", "#444C59", "#414B5B", "#20242C", "#AAB3C2", "#86DFFF", "#B95F6A" };
        var result = new Dictionary<string,string>();
        for (int i=0;i<Keys.Length;i++) result[Keys[i]]=values[i];
        return result;
    }
    public static Dictionary<string,string> Preset(int index) {
        var result=Defaults();
        string[][] fills={
            new[]{"#A8CAE5","#EDD887","#ECB788","#E1A4B1"},
            new[]{"#EDBD9F","#EDD887","#E5A059","#DC96A1"},
            new[]{"#68502F","#726016","#7D4623","#813443"},
            new[]{"#B7BDC5","#EDD887","#E5AD78","#DDA0AD"}
        };
        string[][] surfaces={
            new[]{"#EDF5FC","#D5E2EF","#708EA8","#9AB3C8","#213345","#455E73","#304D63","#9C5200","#0070C9","#A52239"},
            new[]{"#FFF1E6","#E8D6C7","#A0836C","#C4A68D","#493027","#735347","#63483A","#9A450C","#006485","#A52239"},
            new[]{"#796D62","#4B4238","#C4B3A0","#ADA08F","#FFF8EE","#F8EFE4","#201A14","#FFE08C","#98E7FF","#FFBBC4"},
            new[]{"#FFFFFF","#E4E7EB","#747E8A","#A0A8B3","#18212B","#4B5968","#35414F","#9C5200","#0070C9","#A52239"}
        };
        string[] tones={"Green","Yellow","Orange","Red"};
        for(int i=0;i<4;i++) {result["quota"+tones[i]]=fills[index][i];result["time"+tones[i]]=fills[index][i];}
        string[] keys={"background","track","barBorder","windowBorder","title","icons","ticks","alert","weekMarker","failure"};
        for(int i=0;i<keys.Length;i++) result[keys[i]]=surfaces[index][i];
        foreach(string key in new[]{"label","valueText","timeText","caption"}) result[key]=result["title"];
        result["status"]=result["icons"];
        return result;
    }
    public static string PresetName(int index,bool english) {
        return (english ? new[]{"Glacier blue","Apricot cream","Warm sand","White graphite"} : new[]{"冰川蓝","杏桃奶油","暖砂灰","纯白石墨"})[index];
    }
    public static Brush Brush(string key) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(Colors[key])); }
    public static bool Valid(string value) {
        if (value == null || (value.Length != 7 && value.Length != 9) || value[0] != '#') return false;
        for (int i=1;i<value.Length;i++) if (!Uri.IsHexDigit(value[i])) return false;
        return true;
    }
}
