namespace Nexora.Application.Dto
{
    public record LanguageDto(
        string Code,
        string Label,
        bool IsSelected
    );

    public record NotificationPreferencesDto(
        bool EmailAlerts,
        bool SmsAlerts,
        bool PushAlerts
    );

    public record AccountInfoDto(
        string FirstName,
        string LastName,
        string Email,
        string Country,
        string City,
        string? PhoneNumber
    );

    public record SecuritySettingsDto(
        bool Is2faActive,
        string LastPasswordChange
    );

    public record SystemSettingsResponseDto(
        LanguageDto[] Languages,
        NotificationPreferencesDto Notifications,
        AccountInfoDto Account,
        SecuritySettingsDto Security
    );

    public record UpdatePasswordRequest(string CurrentPassword, string NewPassword);

    public record UpdateNotificationRequest(bool EmailAlerts, bool SmsAlerts, bool PushAlerts);

    public record UpdateLanguageRequest(string LanguageCode);
}
