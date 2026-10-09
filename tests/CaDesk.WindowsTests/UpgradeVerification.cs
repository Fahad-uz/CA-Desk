using CaDesk.Core;
using CaDesk.Desktop;
using CaDesk.Desktop.ViewModels;
using System.IO;
using System.Text.Json;

namespace CaDesk.WindowsTests;

internal static class UpgradeVerification
{
    public static async Task Run(string profile, Action<bool, string> check)
    {
        var expected = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(Path.Combine(profile, "manifest.json")))!;
        var paths = new AppDataPaths(profile);
        check(!File.Exists(paths.DatabasePath) && File.Exists(paths.LegacyPath), "Upgrade fixture uses the actual previous-release data location");
        var repo = paths.OpenRepository();
        var store = new ApplicationStore(repo); await store.ReloadAsync();
        var actual = store.Snapshot;
        check(expected.Clients.OrderBy(c => c.Id).SequenceEqual(actual.Clients.OrderBy(c => c.Id)) && expected.WorkAreas.OrderBy(w => w.Id).SequenceEqual(actual.WorkAreas.OrderBy(w => w.Id)), "Upgrade preserves every field, ID and relationship from v1.0.1: 150 clients and 3,000 work areas");
        check(actual.Clients.All(c => c.DueDate == null) && actual.Notes.Count == 0 && DatabaseSafety.Validate(paths.DatabasePath) == 3, "Ordered forward migrations supply empty optional due dates and an independent study-notes table");
        var backup = Directory.GetFiles(paths.BackupDirectory, "*pre-migration*.db").Single();
        check(DatabaseSafety.Validate(backup) == 1 && DatabaseSafety.Validate(paths.LegacyPath) == 1, "Verified pre-upgrade backup and original legacy database remain untouched");
        var window = new MainWindow(store, false); window.Show(); var shell = window.Shell; shell.Refresh();
        check(shell.Master.Items.Count == 3000 && shell.Dashboard.Progress == Progress.From(expected.WorkAreas), "Upgraded Master Work Table and dashboard progress match the previous release");
        shell.Calendar.Mode = CalendarMode.Month;
        var start = shell.Calendar.Days[0].Date; var end = shell.Calendar.Days[^1].Date;
        check(shell.Calendar.Days.Sum(d => d.Items.Count + d.ExtraCount) == expected.WorkAreas.Count(w => w.Deadline >= start && w.Deadline <= end), "Upgraded calendar retains every deadline in the displayed period");
        await store.SaveNoteAsync(null, "Upgrade study note", "Independent body\nSecond line", DateOnly.FromDateTime(DateTime.Today));
        await store.SaveClientAsync(actual.Clients[0].Id, actual.Clients[0].Name, actual.Clients[0].Description, DateOnly.FromDateTime(DateTime.Today).AddDays(10));
        var exported = Path.Combine(profile, "complete-export.db"); await store.ExportBackupAsync(exported);
        await store.DeleteNoteAsync(store.Snapshot.Notes.Single().Id); await store.DeleteClientAsync(actual.Clients[0].Id);
        await store.RestoreBackupAsync(exported);
        check(store.Snapshot.Clients.Count == 150 && store.Snapshot.WorkAreas.Count == 3000 && store.Snapshot.Notes.Single().Body.Contains('\n') && store.Snapshot.Clients.Single(c => c.Id == actual.Clients[0].Id).DueDate != null, "Post-upgrade backup restores clients, relationships, deadlines, due dates and study notes together");
        window.Close();
        var reopened = paths.OpenRepository().Load();
        check(reopened.Clients.Count == 150 && reopened.WorkAreas.Count == 3000 && reopened.Notes.Count == 1, "Reinstall/reopen selects the existing canonical database without re-importing the legacy copy");
        await store.RestoreBackupAsync(backup);
        check(expected.Clients.OrderBy(c => c.Id).SequenceEqual(store.Snapshot.Clients.OrderBy(c => c.Id)) && expected.WorkAreas.OrderBy(w => w.Id).SequenceEqual(store.Snapshot.WorkAreas.OrderBy(w => w.Id)), "The original pre-upgrade backup restores and migrates with all previous-release values intact");
    }
}
