using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFD.Shared.Models;

[Table("task_groups")]
public class TaskGroup
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int LeaderUserId { get; set; }

    [ForeignKey("LeaderUserId")]
    public virtual User? Leader { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public virtual ICollection<DailyTask> SharedTasks { get; set; } = new List<DailyTask>();
}
