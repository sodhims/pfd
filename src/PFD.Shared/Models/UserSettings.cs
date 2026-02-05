using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFD.Shared.Models;

[Table("user_settings")]
public class UserSettings
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Theme { get; set; } = "teal";

    public bool IsDailyView { get; set; } = true;

    public bool UseLargeText { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("calendar_credentials")]
public class CalendarCredentials
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty; // "Google", "Microsoft"

    [MaxLength(500)]
    public string? ClientId { get; set; }

    [MaxLength(500)]
    public string? ClientSecret { get; set; }

    [MaxLength(200)]
    public string? TenantId { get; set; } // For Microsoft

    [MaxLength(500)]
    public string? RedirectUri { get; set; }

    // OAuth tokens (encrypted in production)
    [MaxLength(2000)]
    public string? AccessToken { get; set; }

    [MaxLength(2000)]
    public string? RefreshToken { get; set; }

    public DateTime? TokenExpiry { get; set; }

    public bool IsConnected { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
