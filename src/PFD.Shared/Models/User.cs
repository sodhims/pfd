using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFD.Shared.Models;

[Table("users")]
public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DisplayName { get; set; }

    // Personal Info
    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string Theme { get; set; } = "teal";

    public bool IsDailyView { get; set; } = true;

    public bool UseLargeText { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<DailyTask> Tasks { get; set; } = new List<DailyTask>();
    public virtual ICollection<TaskGroup> LedGroups { get; set; } = new List<TaskGroup>();
    public virtual ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
}
