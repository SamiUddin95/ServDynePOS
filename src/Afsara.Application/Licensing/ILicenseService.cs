namespace Afsara.Application.Licensing;

public enum LicenseState
{
    Missing,
    Invalid,
    Expired,
    ClockMismatch,
    Valid
}

public sealed record LicenseStatus(LicenseState State, string Message, string? Licensee = null, DateOnly? ExpiresOn = null)
{
    public const int WarningDays = 7;

    public bool IsValid => State == LicenseState.Valid;

    public int DaysRemaining => ExpiresOn is { } date ? date.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber : 0;

    public bool IsExpiringSoon => IsValid && DaysRemaining <= WarningDays;

    public string RemainingText => DaysRemaining switch
    {
        <= 0 => "expires today",
        1 => "1 day remaining",
        var days => $"{days} days remaining"
    };
}

public interface ILicenseService
{
    string MachineId { get; }
    Task<LicenseStatus> GetStatusAsync();
    Task<LicenseStatus> ActivateAsync(string licenseKey);
}
