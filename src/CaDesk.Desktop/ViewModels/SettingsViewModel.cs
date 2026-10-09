using CaDesk.Core;
namespace CaDesk.Desktop.ViewModels;

public sealed class SettingsViewModel(ShellViewModel shell) : Observable
{
    public string DatabasePath => shell.Store.Repository.DatabasePath;
    public string BackupDirectory => shell.Store.Repository.BackupDirectory;
    public string Version => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.1.0";
    private bool busy;
    public bool IsBusy { get => busy; private set { Set(ref busy, value); Raise(nameof(CanUse)); } }
    public bool CanUse => !IsBusy;
    private string message = "";
    public string Message { get => message; private set => Set(ref message, value); }
    public async Task ExportAsync(string path)
    {
        if (IsBusy) return;
        IsBusy = true; Message = "Creating and checking your backup…";
        try { await shell.Store.ExportBackupAsync(path); Message = $"Backup saved: {path}"; }
        catch (Exception ex) { Message = ex is ValidationException ? ex.Message : "Backup could not be saved. Choose a writable folder with free space."; }
        finally { IsBusy = false; shell.IsBusy = false; }
    }
    public async Task RestoreAsync(string path)
    {
        if (IsBusy) return;
        IsBusy = true; Message = "Checking the selected backup…";
        try
        {
            await shell.Store.ValidateBackupAsync(path);
            if (!shell.Dialogs.Confirm("Restore backup?", "This replaces all current clients, work areas, study notes and preferences with the selected backup. A verified copy of your current data will be saved first. Continue?"))
            { Message = "Restore cancelled. Your current data is kept."; return; }
            Message = "Restoring your data…"; shell.IsBusy = true;
            await shell.Store.RestoreBackupAsync(path);
            shell.ApplyPreferences(); shell.Refresh();
            Message = "Backup restored and verified. Your previous data is kept in the backups folder.";
        }
        catch (Exception ex) { Message = ex is ValidationException or RecoveryException ? ex.Message : "Restore could not be completed. Your previous database and recovery backups are kept."; }
        finally { IsBusy = false; shell.IsBusy = false; }
    }
}
