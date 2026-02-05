using PFD.Data.Repositories;
using PFD.Shared.Interfaces;
using PFD.Shared.Models;

namespace PFD.Services;

public class GroupService : IGroupService
{
    private readonly GroupRepository _groupRepository;
    private readonly TaskRepository _taskRepository;

    public GroupService(GroupRepository groupRepository, TaskRepository taskRepository)
    {
        _groupRepository = groupRepository;
        _taskRepository = taskRepository;
    }

    public async Task<TaskGroup> CreateGroupAsync(string name, int leaderUserId)
    {
        return await _groupRepository.CreateGroupAsync(name, leaderUserId);
    }

    public async Task<List<TaskGroup>> GetGroupsForUserAsync(int userId)
    {
        return await _groupRepository.GetGroupsForUserAsync(userId);
    }

    public async Task<TaskGroup?> GetGroupByIdAsync(int groupId)
    {
        return await _groupRepository.GetGroupByIdAsync(groupId);
    }

    public async Task DeleteGroupAsync(int groupId, int leaderUserId)
    {
        await _groupRepository.DeleteGroupAsync(groupId, leaderUserId);
    }

    public async Task RenameGroupAsync(int groupId, string newName, int leaderUserId)
    {
        await _groupRepository.RenameGroupAsync(groupId, newName, leaderUserId);
    }

    public async Task<bool> AddMemberByUsernameAsync(int groupId, string username, int requestingUserId)
    {
        var user = await _groupRepository.FindUserByUsernameAsync(username);
        if (user == null) return false;

        return await _groupRepository.AddMemberAsync(groupId, user.Id, requestingUserId);
    }

    public async Task RemoveMemberAsync(int groupId, int userId, int requestingUserId)
    {
        await _groupRepository.RemoveMemberAsync(groupId, userId, requestingUserId);
    }

    public async Task<List<User>> GetGroupMembersAsync(int groupId)
    {
        return await _groupRepository.GetGroupMembersAsync(groupId);
    }

    public async Task<bool> IsLeaderAsync(int groupId, int userId)
    {
        return await _groupRepository.IsLeaderAsync(groupId, userId);
    }

    public async Task ShareTaskToGroupAsync(int taskId, int groupId, int userId)
    {
        // Validate: user owns the task
        var task = await _taskRepository.GetByIdAsync(taskId, userId);
        if (task == null || task.UserId != userId) return;

        // Validate: user is leader of the target group
        if (!await _groupRepository.IsLeaderAsync(groupId, userId)) return;

        task.GroupId = groupId;
        task.UpdatedAt = DateTime.UtcNow;
        await _taskRepository.UpdateAsync(task);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _groupRepository.GetAllUsersAsync();
    }

    public async Task UnshareTaskAsync(int taskId, int userId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, userId);
        if (task == null || task.UserId != userId) return;

        task.GroupId = null;
        task.UpdatedAt = DateTime.UtcNow;
        await _taskRepository.UpdateAsync(task);
    }

}
