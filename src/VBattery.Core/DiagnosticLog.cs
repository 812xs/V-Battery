using System.Collections.Concurrent;

namespace VBattery.Core;

/// <summary>Bellekte tutulan sınırlı tanılama günlüğü (tanılama raporunda kullanılır).</summary>
public static class DiagnosticLog
{
    private const int MaxLines = 500;
    private static readonly ConcurrentQueue<string> Lines = new();

    public static void Write(string source, string message)
    {
        Lines.Enqueue($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}] {message}");
        while (Lines.Count > MaxLines && Lines.TryDequeue(out _)) { }
    }

    public static void Write(string source, Exception ex, string? context = null) =>
        Write(source, $"{context ?? "Hata"}: {ex.GetType().Name}: {ex.Message}");

    public static IReadOnlyList<string> Snapshot() => Lines.ToArray();
}
