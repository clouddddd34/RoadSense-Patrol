using System.Text.Json;

namespace RoadSensePatrol;

public sealed record LeaderboardEntry(string Name, int Score, DateTime WhenUtc);

public sealed class LeaderboardStore
{
    public const int MaxNameLength = 20;
    private readonly string path;
    public List<LeaderboardEntry> Entries { get; private set; } = [];

    public LeaderboardStore(string? directoryOverride = null)
    {
        var directory = directoryOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RoadSensePatrol");
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "leaderboard.json");
        Reload();
    }

    public void Reload()
    {
        try
        {
            Entries = (JsonSerializer.Deserialize<List<LeaderboardEntry>>(File.ReadAllText(path)) ?? [])
                .Where(e => e.Score >= 0 && !string.IsNullOrWhiteSpace(e.Name) && e.Name.Length <= MaxNameLength)
                .OrderByDescending(e => e.Score).ThenBy(e => e.WhenUtc).Take(10).ToList();
        }
        catch { Entries = []; }
    }

    public bool Qualifies(int score) => score > 0 && (Entries.Count < 10 || score > Entries[^1].Score);

    public void Add(string name, int score)
    {
        name = new string(name.Select(c => char.IsWhiteSpace(c) ? ' ' : c)
            .Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '\'' or '.').ToArray());
        name = string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length > MaxNameLength) name = name[..MaxNameLength].TrimEnd();
        if (name.Length == 0) name = "PLAYER";
        Entries.Add(new(name, score, DateTime.UtcNow));
        Entries = Entries.OrderByDescending(e => e.Score).ThenBy(e => e.WhenUtc).Take(10).ToList();
        Save();
    }

    public void Clear() { Entries.Clear(); Save(); }

    private void Save()
    {
        try
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Entries, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
        catch { /* A booth run must continue even if the disk is read-only. */ }
    }
}
