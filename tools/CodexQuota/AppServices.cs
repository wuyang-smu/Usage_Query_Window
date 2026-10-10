using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

[assembly: AssemblyVersion("0.1.9.0")]
// Stable repository ID keeps update checks working after account/repository renames.
internal static class AppServices {
    public static readonly Version CurrentVersion=new Version(0,1,9);
    const string Api="https://api.github.com/repositories/1408153798/releases/latest";
    const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName="CodexUsageWidget";
    static string optionsFile;
    public static bool AutoCheck, Checking;
    public static string LatestUrl="", LatestVersion="", Error="";
    public static event Action Changed;
    class Options { public bool AutoCheck; }
    public static void Load(string folder) {
        optionsFile=Path.Combine(folder,"startup-update.json");
        try {if(File.Exists(optionsFile)) AutoCheck=new JavaScriptSerializer().Deserialize<Options>(File.ReadAllText(optionsFile)).AutoCheck;} catch(Exception) {AutoCheck=false;}
    }
    public static void SetAutoCheck(bool value) {
        Directory.CreateDirectory(Path.GetDirectoryName(optionsFile));
        File.WriteAllText(optionsFile,new JavaScriptSerializer().Serialize(new Options {AutoCheck=value}));AutoCheck=value;
    }
    public static bool IsStartupEnabled() {
        using(var key=Registry.CurrentUser.OpenSubKey(RunKey)) return key!=null && (string)key.GetValue(RunName)==StartupCommand();
    }
    static string StartupCommand() {return "\""+Assembly.GetExecutingAssembly().Location+"\"";}
    public static void SetStartup(bool enabled) {
        using(var key=Registry.CurrentUser.CreateSubKey(RunKey)) {
            if(enabled) key.SetValue(RunName,StartupCommand());else key.DeleteValue(RunName,false);
        }
    }
    public static bool IsNewer(string tag) {
        Version version;return Version.TryParse((tag ?? "").TrimStart('v','V'),out version) && version>CurrentVersion;
    }
    public static bool ValidReleaseUrl(string value) {
        Uri uri;return Uri.TryCreate(value,UriKind.Absolute,out uri) && uri.Scheme=="https" && uri.Host=="github.com" && uri.AbsolutePath.Contains("/releases/tag/");
    }
    public static async void CheckUpdates() {
        if(Checking) return;Checking=true;Error="";Notify();
        try {
            var result=await Task.Run(()=>{
                var request=(HttpWebRequest)WebRequest.Create(Api);request.UserAgent="CodexUsageWidget/0.1.9";request.Accept="application/vnd.github+json";request.Timeout=15000;request.ReadWriteTimeout=15000;
                using(var response=request.GetResponse()) using(var reader=new StreamReader(response.GetResponseStream())) return new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(reader.ReadToEnd());
            });
            string tag=Convert.ToString(result["tag_name"]),url=Convert.ToString(result["html_url"]);
            if(!ValidReleaseUrl(url)) throw new InvalidDataException("Invalid release URL");
            LatestVersion=tag;LatestUrl=IsNewer(tag) ? url : "";
        } catch(Exception ex) {Error=ex is WebException ? "Network request failed" : "Could not read release information";}
        finally {Checking=false;Notify();}
    }
    static void Notify() {if(Changed!=null) Changed();}
    public static void OpenRelease() {if(ValidReleaseUrl(LatestUrl)) Process.Start(new ProcessStartInfo(LatestUrl) {UseShellExecute=true});}
}