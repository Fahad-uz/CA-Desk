using CaDesk.Core;
using System.Windows.Input;

namespace CaDesk.Desktop.ViewModels;

public sealed class NoteCardViewModel(StudyNote note, ShellViewModel shell)
{
    public StudyNote Note { get; } = note;
    public long Id => Note.Id;
    public string Heading => Note.Heading;
    public string DateLabel => Note.NoteDate.ToString("d MMMM yyyy");
    public string Preview
    {
        get
        {
            var text = Note.Body.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length == 0 ? "Open this note to start writing." : text.Length > 180 ? text[..180] + "…" : text;
        }
    }
    public ICommand OpenCommand { get; } = new RelayCommand(_ => shell.OpenNote(note.Id));
    public ICommand DeleteCommand { get; } = new AsyncCommand(() => shell.DeleteNoteAsync(note), shell.ReportError);
}

public sealed class NotesViewModel(ShellViewModel shell) : Observable
{
    private string search = "";
    public string Search { get => search; set { if (Set(ref search, value)) Refresh(); } }
    public IReadOnlyList<NoteCardViewModel> Items { get; private set; } = [];
    public bool IsEmpty => Items.Count == 0;
    public string EmptyTitle => shell.Store.Snapshot.Notes.Count == 0 ? "A place for everything you learn" : "No matching notes";
    public string EmptyDescription => shell.Store.Snapshot.Notes.Count == 0 ? "Capture an idea, a topic or a little progress. Your study notes appear on the calendar." : "Try another heading or a word from the note body.";
    public string CountLabel => $"{shell.Store.Snapshot.Notes.Count} saved {(shell.Store.Snapshot.Notes.Count == 1 ? "note" : "notes")}";
    public string ResultLabel => $"{Items.Count} shown · newest date first";
    public ICommand AddCommand => shell.AddNoteCommand;
    public ICommand ClearSearchCommand { get; } = new RelayCommand(_ => shell.Notes.Search = "");
    public void Refresh()
    {
        var query = Search.Trim();
        Items = shell.Store.Snapshot.Notes.Where(n => n.Heading.Contains(query, StringComparison.OrdinalIgnoreCase) || n.Body.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.NoteDate).ThenByDescending(n => n.Id).Select(n => new NoteCardViewModel(n, shell)).ToArray();
        foreach (var p in new[] { nameof(Items), nameof(IsEmpty), nameof(EmptyTitle), nameof(EmptyDescription), nameof(CountLabel), nameof(ResultLabel) }) Raise(p);
    }
}

public sealed class NoteEditorViewModel : EditorViewModel
{
    private readonly long? id;
    private readonly string originalHeading, originalBody;
    private readonly DateTime? originalDate;
    private string heading, body;
    private DateTime? noteDate;
    public override string Title => id == null ? "New study note" : "Study note";
    public override string DateErrorMessage => "Choose a valid date for this note.";
    public string Heading { get => heading; set { if (Set(ref heading, value)) Raise(nameof(IsDirty)); } }
    public string Body { get => body; set { if (Set(ref body, value)) Raise(nameof(IsDirty)); } }
    public DateTime? NoteDate
    {
        get => noteDate;
        set { if (Set(ref noteDate, value)) { Raise(nameof(IsDirty)); Raise(nameof(DateLabel)); } }
    }
    public string DateLabel => NoteDate?.ToString("dddd, d MMMM yyyy") ?? "Choose a date";
    public bool IsDirty => Heading != originalHeading || Body != originalBody || NoteDate != originalDate || !DateInputValid;
    public NoteEditorViewModel(ShellViewModel shell, StudyNote? note) : base(shell)
    {
        id = note?.Id; heading = originalHeading = note?.Heading ?? ""; body = originalBody = note?.Body ?? "";
        noteDate = originalDate = note?.NoteDate.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
    }
    protected override Task SaveAsync()
    {
        if (!DateInputValid || NoteDate == null) throw new ValidationException(DateErrorMessage);
        return Shell.Store.SaveNoteAsync(id, Heading, Body, DateOnly.FromDateTime(NoteDate.Value));
    }
}
