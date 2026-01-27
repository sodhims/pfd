using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface IOllamaService
{
    Task<TaskMetadata?> AugmentTaskAsync(string taskText, List<Participant>? recentParticipants = null);
    Task<bool> IsAvailableAsync();
}
