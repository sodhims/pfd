using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFD.Shared.Models;

[Table("participants")]
public class Participant
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    /// <summary>
    /// Number of meetings this participant has attended (for frequency-based suggestions)
    /// </summary>
    public int MeetingCount { get; set; }

    /// <summary>
    /// Last meeting date for recency-based suggestions
    /// </summary>
    public DateTime? LastMeetingDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property
    /// </summary>
    public virtual ICollection<TaskParticipant> TaskParticipants { get; set; } = new List<TaskParticipant>();
}
