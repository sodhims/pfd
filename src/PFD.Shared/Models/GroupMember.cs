using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PFD.Shared.Models;

[Table("group_members")]
public class GroupMember
{
    [Key]
    public int Id { get; set; }

    public int GroupId { get; set; }

    [ForeignKey("GroupId")]
    public virtual TaskGroup? Group { get; set; }

    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
