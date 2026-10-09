using CaDesk.Core;
using System.Text.Json;

// This project intentionally references the Core DLL downloaded from the published v1.0.1 ZIP.
if (args[0] == "--probe-newer")
{
    try { _ = new Repository(args[1]); throw new Exception("Previous release accepted an incompatible database."); }
    catch (ValidationException ex) when (ex.Message.Contains("newer")) { Console.WriteLine("PASS: v1.0.1 refuses schema 3 without changing data."); }
    return;
}
var profile = Path.GetFullPath(args[0]);
var path = Path.Combine(profile, "CA Desk", "ca-desk.db");
var repo = new Repository(path);
var today = DateOnly.FromDateTime(DateTime.Today);
for (var i = 0; i < 150; i++)
{
    var c = repo.SaveClient(null, $"Fixture Client {i:000}", $"Fictional client {i}\nDescription retained across updates.");
    for (var j = 0; j < 20; j++)
        repo.SaveWork(null, c, $"Work {j:00} — Client {i:000}", (WorkStatus)(j % 3), j % 5 == 0 ? null : today.AddDays(j - 10), $"Old-release work notes {i}/{j}\nLine two, apostrophe's and Unicode ₹.");
}
var snapshot = repo.Load();
File.WriteAllText(Path.Combine(profile, "manifest.json"), JsonSerializer.Serialize(new { snapshot.Clients, snapshot.WorkAreas }));
Console.WriteLine($"Created using published v1.0.1 Core: {snapshot.Clients.Count} clients and {snapshot.WorkAreas.Count} work areas.");
