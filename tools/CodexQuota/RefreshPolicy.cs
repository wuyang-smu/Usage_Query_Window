using System;
using System.Threading.Tasks;
using System.Windows.Controls;
partial class QuotaWidget {
    static bool DataIsStale() { return DataIsStaleAt(DateTimeOffset.UtcNow); }
    static bool DataIsStaleAt(DateTimeOffset now) {
        if (snapshot == null) return false;
        return (snapshot.Hour != null && (now - (snapshot.HourTime == default(DateTimeOffset) ? snapshot.Time : snapshot.HourTime)).TotalMinutes >= 5)
            || (snapshot.Week != null && (now - (snapshot.WeekTime == default(DateTimeOffset) ? snapshot.Time : snapshot.WeekTime)).TotalMinutes >= 5);
    }
    static bool ShouldAutoQuery(DateTimeOffset now) {
        bool localFailed=localFailureSince!=DateTimeOffset.MinValue;
        bool due=localFailed ? (now-localFailureSince).TotalMinutes>=3 : snapshot==null || DataIsStaleAt(now);
        return !reading && (queryFailed || due) && (now-lastActiveCompleted).TotalMinutes>=5;
    }
    static void RegisterLocalResult(Snapshot candidate, DateTimeOffset now) {
        bool valid=candidate!=null && (candidate.Hour!=null || candidate.Week!=null);
        if(!valid) {if(localFailureSince==DateTimeOffset.MinValue) localFailureSince=now;return;}
        localFailureSince=DateTimeOffset.MinValue;
        bool fresh=(candidate.Hour==null || (now-(candidate.HourTime==default(DateTimeOffset) ? candidate.Time : candidate.HourTime)).TotalMinutes<5)
            && (candidate.Week==null || (now-(candidate.WeekTime==default(DateTimeOffset) ? candidate.Time : candidate.WeekTime)).TotalMinutes<5);
        if(fresh && snapshot!=null && !DataIsStaleAt(now)) {queryFailed=false;queryError="";}
    }
    static async void Refresh(bool active) {
        if (reading) return;
        reading = true;
        activeReading=active;
        Control<Button>("RefreshButton").IsEnabled = false;
        UpdateRefreshStatus();
        if (active) Control<TextBlock>("Updated").Text = Text("正在查询账户额度…", "Querying account usage…");
        try {
            bool fallback = false;
            try {
                var candidate = await Task.Run(() => active ? QuerySnapshot() : ReadSnapshot());
                var selected = NewerSnapshot(snapshot, candidate);
                if (!active && candidate != null && snapshot != null && selected == snapshot && candidate.Time > snapshot.Time
                    && ((candidate.Hour != null && snapshot.Hour != null && candidate.Hour.Remaining < snapshot.Hour.Remaining)
                    || (candidate.Week != null && snapshot.Week != null && candidate.Week.Remaining < snapshot.Week.Remaining)))
                    localReadWarning = "Newer local usage rejected by reset/time protection. Please query the account.";
                snapshot = selected;
                if (active) { queryFailed = false; queryError = ""; localReadWarning = ""; localFailureSince=DateTimeOffset.MinValue; }
                else RegisterLocalResult(candidate,DateTimeOffset.UtcNow);
            } catch (Exception ex) {
                if (active) { queryFailed = true; queryError = ex.Message; fallback = true; }
                else {localReadWarning = "Local read failed: " + ex.GetType().Name; RegisterLocalResult(null,DateTimeOffset.UtcNow);}
            }
            if (fallback) {
                try {var candidate=await Task.Run(() => ReadSnapshot());snapshot=NewerSnapshot(snapshot,candidate);RegisterLocalResult(candidate,DateTimeOffset.UtcNow);} catch (Exception) {RegisterLocalResult(null,DateTimeOffset.UtcNow);}
            }
        }
        finally {
            if(active) lastActiveCompleted=DateTimeOffset.UtcNow;
            activeReading=false;
            reading = false; Control<Button>("RefreshButton").IsEnabled = true; Render();
            if(!active && ShouldAutoQuery(DateTimeOffset.UtcNow)) Refresh(true);
        }
    }

}