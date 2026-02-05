using PFD.Shared.Models;

namespace PFD.Shared.Interfaces;

public interface IGroupService
{
    Task<TaskGroup> CreateGroupAsync(string name, int leaderUserId);
    Task<List<TaskGroup>> GetGroupsForUserAsync(int userId);
    Task<TaskGroup?> GetGroupByIdAsync(int groupId);
    Task DeleteGroupAsync(int groupId, int leaderUserId);
    Task RenameGroupAsync(int groupId, string newName, int leaderUserId);
    Task<bool> AddMemberByUsernameAsync(int groupId, string username, int requestingUserId);
    Task RemoveMemberAsync(int groupId, int userId, int requestingUserId);
    Task<List<User>> GetGroupMembersAsync(int groupId);
    Task<bool> IsLeaderAsync(int groupId, int userId);
    Task ShareTaskToGroupAsync(int taskId, int groupId, int userId);
    Task UnshareTaskAsync(int taskId, int userId);
    Task<List<User>> GetAllUsersAsync();
}
