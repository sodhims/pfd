using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFD.Shared.Models;

[Table("task_participants")]
public class TaskParticipant
{
    [Key]
    public int Id { get; set; }

    public int TaskId { get; set; }

    [ForeignKey(nameof(TaskId))]
    public virtual DailyTask Task { get; set; } = null!;

    public int ParticipantId { get; set; }

    [ForeignKey(nameof(ParticipantId))]
    public virtual Participant Participant { get; set; } = null!;

    /// <summary>
    /// True if this participant was suggested by AI, false if manually added
    /// </summary>
    public bool IsSuggested { get; set; }
}
