using Microsoft.Data.Sqlite;
using System.IO;
using System.Threading;

namespace BarcodeStudio.Services;

/// <summary>Online SQLite snapshots to every writable local drive. One validated backup per drive.</summary>
public sealed class BackupService : IDisposable
{
    private readonly string _source;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private Task? _periodicTask;

    public BackupService(string sourceDatabase) => _source = sourceDatabase;

    public void StartPeriodic(TimeSpan interval)
    {
        if (_periodicTask != null) return;
        _periodicTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(_stop.Token))
                    await RunAsync(_stop.Token);
            }
            catch (OperationCanceledException) { }
        });
    }

    public Task<BackupResult> RunAsync(CancellationToken ct = default) => Task.Run(async () =>
    {
        await _gate.WaitAsync(ct);
        try
        {
            var successes = new List<string>();
            var failures = new List<string>();
            if (!File.Exists(_source)) return new BackupResult(successes, ["قاعدة البيانات غير موجودة."]);

            foreach (var drive in DriveInfo.GetDrives())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                    // Location is intentionally below a dedicated folder, not directly at drive root.
                    var directory = Path.Combine(drive.RootDirectory.FullName, "HT_SOLUTIONS", "BarcodeStudio", "Backup");
                    Directory.CreateDirectory(directory);
                    var target = Path.Combine(directory, "barcodes.db");
                    var temp = Path.Combine(directory, $"barcodes.{Guid.NewGuid():N}.tmp");
                    try
                    {
                        // SQLite's online backup API takes a consistent snapshot while the app remains active.
                        using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
                        {
                            DataSource = _source, Mode = SqliteOpenMode.ReadOnly
                        }.ToString()))
                        using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
                        {
                            DataSource = temp, Mode = SqliteOpenMode.ReadWriteCreate
                        }.ToString()))
                        {
                            source.Open();
                            destination.Open();
                            source.BackupDatabase(destination);
                        }
                        // Integrity check the new copy BEFORE replacing the known-good backup.
                        using (var check = new SqliteConnection(new SqliteConnectionStringBuilder
                        {
                            DataSource = temp, Mode = SqliteOpenMode.ReadOnly
                        }.ToString()))
                        {
                            check.Open();
                            using var cmd = check.CreateCommand();
                            cmd.CommandText = "PRAGMA integrity_check;";
                            if (!string.Equals(cmd.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                                throw new IOException("فشل اختبار سلامة نسخة SQLite.");
                        }
                        // Rename inside the same drive is atomic on normal Windows file systems.
                        File.Move(temp, target, overwrite: true);
                        successes.Add(target);
                    }
                    finally
                    {
                        try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or NotSupportedException)
                {
                    failures.Add($"{drive.Name}: {ex.Message}");
                }
            }
            return new BackupResult(successes, failures);
        }
        finally { _gate.Release(); }
    }, ct);

    public void Dispose() { _stop.Cancel(); _stop.Dispose(); }
}

public sealed record BackupResult(IReadOnlyList<string> SuccessfulPaths, IReadOnlyList<string> Errors);
