using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Licensing;
using Afsara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace Afsara.Infrastructure.Licensing;

public sealed class LicenseService : ILicenseService
{
    // Public key only. The matching private key must stay with the license generator and never ship with the app.
    private const string PublicKeyXml =
        "<RSAKeyValue><Modulus>qjRvBNCIo9xJ5lGfQpJmiVbcenAzV6EcIHgZ1WlPFvuYiquBFZ+XNGHEJ0HSJwAEnj1WsKzKvzYc/B2uw8W1VcjDKvuxGUniB9tQ9yq6VqrCBM47FsMV9ozvxmE8TN9Ie2Dt65FSAUY3KKMY8XUd4L+lc79164rm9H4AxVnsf8neHzQgVKOKkZXwJxlEJ1PH872/DVL4zbNxkRqkxh/vCzSHKx3ZbaR7+FxYtP3eT+GnciuPmFAU/ovBNOeTGhSAqRGObj4aWBzJMOVGPZ0oOmRF3gTkaDEi4U8qDHOPyAfUXPM2kKQZUqKFti30ktqnN2K95K0IdGM8wjIZZ4dazQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    private const string DateFormat = "dd MMM yyyy";
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromDays(1);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IApplicationDbContextFactory _factory;
    private readonly Lazy<string> _machineId = new(ComputeMachineId);

    public LicenseService(IApplicationDbContextFactory factory) => _factory = factory;

    public string MachineId => _machineId.Value;

    public async Task<LicenseStatus> GetStatusAsync()
    {
        await using var db = _factory.Create();
        var stored = await db.Licenses.OrderBy(l => l.Id).FirstOrDefaultAsync();
        if (stored is null)
            return new(LicenseState.Missing, "No license is installed. Please enter your license key to continue.");

        if (!IsIntact(stored))
            return new(LicenseState.Invalid, "The installed license data is damaged. Please enter your license key again.");

        if (!TryReadKey(stored.LicenseKey, out var payload))
            return new(LicenseState.Invalid, "The installed license key is not valid. Please enter a valid license key.");

        var now = DateTime.UtcNow;
        if (now < stored.LastSeenUtc - ClockTolerance)
            return ClockMismatch();

        var status = Evaluate(payload);
        if (status.IsValid && now > stored.LastSeenUtc)
        {
            stored.LastSeenUtc = now;
            stored.Checksum = Sign(stored.LicenseKey, now);
            await db.SaveChangesAsync();
        }
        return status;
    }

    public async Task<LicenseStatus> ActivateAsync(string licenseKey)
    {
        var key = string.Concat((licenseKey ?? string.Empty).Where(c => !char.IsWhiteSpace(c)));
        if (key.Length == 0)
            throw new AppException("Please enter your license key.");
        if (!TryReadKey(key, out var payload))
            throw new AppException("The license key is not valid. Please make sure it was copied completely.");

        var status = Evaluate(payload);
        if (!status.IsValid) throw new AppException(status.Message);

        await using var db = _factory.Create();
        var stored = await db.Licenses.OrderBy(l => l.Id).FirstOrDefaultAsync();
        if (stored is null)
        {
            stored = new AppLicense();
            db.Licenses.Add(stored);
        }
        else
        {
            stored.UpdatedAt = DateTime.Now;
        }

        var now = DateTime.UtcNow;
        stored.LicenseKey = key;
        stored.Licensee = payload.Licensee ?? string.Empty;
        stored.ExpiresOn = payload.ExpiresOn;
        stored.MachineId = string.IsNullOrWhiteSpace(payload.MachineId) ? null : payload.MachineId.Trim();
        stored.LastSeenUtc = now;
        stored.Checksum = Sign(key, now);
        await db.SaveChangesAsync();
        return status;
    }

    private LicenseStatus Evaluate(LicensePayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.MachineId) &&
            !string.Equals(payload.MachineId.Trim(), MachineId, StringComparison.OrdinalIgnoreCase))
            return new(LicenseState.Invalid, "This license key was issued for a different computer.");

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (payload.IssuedOn is { } issued && today < issued.AddDays(-1))
            return ClockMismatch();

        if (today > payload.ExpiresOn)
            return new(LicenseState.Expired,
                $"Your license expired on {payload.ExpiresOn.ToString(DateFormat)}. Please clear your pending dues and enter the new license key to continue.",
                payload.Licensee, payload.ExpiresOn);

        return new(LicenseState.Valid,
            $"Licensed to {payload.Licensee}. Valid until {payload.ExpiresOn.ToString(DateFormat)}.",
            payload.Licensee, payload.ExpiresOn);
    }

    private static LicenseStatus ClockMismatch() => new(LicenseState.ClockMismatch,
        "Your system date and time appear to be incorrect. Please correct the date and time, then try again.");

    private static bool TryReadKey(string key, out LicensePayload payload)
    {
        payload = null!;
        var parts = key.Split('.');
        if (parts.Length != 2) return false;

        try
        {
            var data = Convert.FromBase64String(parts[0]);
            var signature = Convert.FromBase64String(parts[1]);

            using var rsa = RSA.Create();
            rsa.FromXmlString(PublicKeyXml);
            if (!rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) return false;

            var parsed = JsonSerializer.Deserialize<LicensePayload>(data, JsonOptions);
            if (parsed is null || parsed.ExpiresOn == default) return false;
            payload = parsed;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException)
        {
            return false;
        }
    }

    // Detects manual edits to the Licenses row (e.g. rolling LastSeenUtc back).
    private static bool IsIntact(AppLicense stored) =>
        !string.IsNullOrEmpty(stored.LicenseKey) &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(stored.Checksum ?? string.Empty),
            Encoding.ASCII.GetBytes(Sign(stored.LicenseKey, stored.LastSeenUtc)));

    private static string Sign(string key, DateTime lastSeenUtc)
    {
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes("ServDyne.License|v1"));
        return Convert.ToBase64String(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes($"{key}|{lastSeenUtc.Ticks}")));
    }

    private static string ComputeMachineId()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"ServDyne|{ReadMachineGuid() ?? Environment.MachineName}"));
        var hex = Convert.ToHexString(hash, 0, 8);
        return string.Join("-", Enumerable.Range(0, 4).Select(i => hex.Substring(i * 4, 4)));
    }

    private static string? ReadMachineGuid()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string;
        }
        catch
        {
            return null;
        }
    }

    private sealed record LicensePayload(string Licensee, DateOnly ExpiresOn, DateOnly? IssuedOn, string? MachineId);
}
