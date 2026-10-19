using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BarcodeStudio.Services;

public sealed record LicenseStatus(bool IsActivated, int DaysRemaining);

public sealed class LicenseService
{
    private const int TrialDays = 3;
    private const string RegistryPath = @"Software\BarcodeStudio\License";
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BarcodeStudio");
    private string StatePath => Path.Combine(_directory, "license-state.json");
    private string KeyPath => Path.Combine(_directory, "license-key.txt");

    public string MotherboardSerial => GetHardwareValue("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber");
    public string ProcessorId => GetHardwareValue("SELECT ProcessorId FROM Win32_Processor", "ProcessorId");

    // Exact same algorithm used by FrmActivate: trim, uppercase, SHA256, first 16 hex chars.
    public static string GenerateActivationCode(string motherboardSerial, string processorId)
    {
        var mb = motherboardSerial.Trim().ToUpper();
        var cpu = processorId.Trim().ToUpper();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{mb}-{cpu}"));
        return Convert.ToHexString(hash)[..16];
    }

    private static string GetHardwareValue(string query, string field)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject obj in searcher.Get())
                return obj[field]?.ToString()?.Trim() is { Length: > 0 } value ? value : "Unknown";
        }
        catch { /* Some machines restrict WMI; keep the legacy fallback. */ }
        return "Unknown";
    }

    private bool HardwareAvailable => MotherboardSerial != "Unknown" && ProcessorId != "Unknown";

    public bool Activate(string enteredCode)
    {
        if (!HardwareAvailable) return false;
        string expected = GenerateActivationCode(MotherboardSerial, ProcessorId);
        if (!string.Equals(enteredCode.Trim(), expected, StringComparison.OrdinalIgnoreCase)) return false;
        Directory.CreateDirectory(_directory);
        File.WriteAllText(KeyPath, expected, Encoding.UTF8);
        return true;
    }

    public LicenseStatus CheckLicense()
    {
        Directory.CreateDirectory(_directory);
        if (HardwareAvailable && File.Exists(KeyPath))
        {
            var saved = File.ReadAllText(KeyPath).Trim();
            if (string.Equals(saved, GenerateActivationCode(MotherboardSerial, ProcessorId), StringComparison.OrdinalIgnoreCase))
                return new LicenseStatus(true, int.MaxValue);
        }

        // Track first use and latest check in two locations to limit easy resets and clock rollback.
        DateTime utcNow = DateTime.UtcNow;
        TrialState? fileState = null;
        try { if (File.Exists(StatePath)) fileState = JsonSerializer.Deserialize<TrialState>(File.ReadAllText(StatePath)); }
        catch { return new LicenseStatus(false, 0); } // Corrupted state should not restart the trial.
        DateTime? registryFirst = null, registryLast = null;
        using (var reg = Registry.CurrentUser.CreateSubKey(RegistryPath))
        {
            registryFirst = ParseUtc(reg?.GetValue("FirstRunUtc") as string);
            registryLast = ParseUtc(reg?.GetValue("LastRunUtc") as string);
        }
        if (File.Exists(StatePath) && fileState == null) return new LicenseStatus(false, 0);
        if (fileState != null && registryFirst == null) return new LicenseStatus(false, 0);
        if (fileState == null && registryFirst != null) return new LicenseStatus(false, 0);
        var first = new[] { fileState?.FirstRunUtc, registryFirst }.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(utcNow).Min();
        var last = new[] { fileState?.LastRunUtc, registryLast }.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(utcNow).Max();
        if (utcNow.AddMinutes(5) < last) return new LicenseStatus(false, 0); // Clock moved backwards.
        var next = new TrialState(first, utcNow);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(next));
        using (var reg = Registry.CurrentUser.CreateSubKey(RegistryPath))
        {
            reg?.SetValue("FirstRunUtc", first.ToString("O"));
            reg?.SetValue("LastRunUtc", utcNow.ToString("O"));
        }
        var remaining = TimeSpan.FromDays(TrialDays) - (utcNow - first);
        return new LicenseStatus(false, remaining > TimeSpan.Zero ? (int)Math.Ceiling(remaining.TotalDays) : 0);
    }

    private static DateTime? ParseUtc(string? value) => DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : null;
    private sealed record TrialState(DateTime FirstRunUtc, DateTime LastRunUtc);
}
