using Microsoft.EntityFrameworkCore;
using PFD.Shared.Models;

namespace PFD.Data.Repositories;

public class GroupRepository
{
    private readonly IDbContextFactory<PfdDbContext> _contextFactory;

    public GroupRepository(IDbContextFactory<PfdDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<TaskGroup> CreateGroupAsync(string name, int leaderUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var group = new TaskGroup
        {
            Name = name,
            LeaderUserId = leaderUserId,
            CreatedAt = DateTime.UtcNow
        };

        context.TaskGroups.Add(group);
        await context.SaveChangesAsync();

        // Auto-add leader as a member
        var membership = new GroupMember
        {
            GroupId = group.Id,
            UserId = leaderUserId,
            JoinedAt = DateTime.UtcNow
        };
        context.GroupMembers.Add(membership);
        await context.SaveChangesAsync();

        return group;
    }

    public async Task<List<TaskGroup>> GetGroupsForUserAsync(int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var groupIds = await context.GroupMembers
            .Where(gm => gm.UserId == userId)
            .Select(gm => gm.GroupId)
            .ToListAsync();

        return await context.TaskGroups
            .Where(g => groupIds.Contains(g.Id))
            .Include(g => g.Members)
            .ToListAsync();
    }

    public async Task<TaskGroup?> GetGroupByIdAsync(int groupId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.TaskGroups
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);
    }

    public async Task DeleteGroupAsync(int groupId, int leaderUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var group = await context.TaskGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.LeaderUserId == leaderUserId);
        if (group == null) return;

        // Clear GroupId on any shared tasks (revert to personal)
        var sharedTasks = await context.DailyTasks.Where(t => t.GroupId == groupId).ToListAsync();
        foreach (var task in sharedTasks)
        {
            task.GroupId = null;
        }

        // Remove all group members first (foreign key constraint)
        var members = await context.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync();
        context.GroupMembers.RemoveRange(members);

        context.TaskGroups.Remove(group);
        await context.SaveChangesAsync();
    }

    public async Task RenameGroupAsync(int groupId, string newName, int leaderUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var group = await context.TaskGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.LeaderUserId == leaderUserId);
        if (group == null) return;

        group.Name = newName;
        await context.SaveChangesAsync();
    }

    public async Task<bool> AddMemberAsync(int groupId, int userId, int requestingUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Only leader can add members
        var group = await context.TaskGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.LeaderUserId == requestingUserId);
        if (group == null) return false;

        // Check if already a member
        var existing = await context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
        if (existing) return false;

        context.GroupMembers.Add(new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return true;
    }

    public async Task RemoveMemberAsync(int groupId, int userId, int requestingUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Leader can remove anyone; members can remove themselves
        var group = await context.TaskGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null) return;

        if (requestingUserId != group.LeaderUserId && requestingUserId != userId)
            return;

        // Don't allow removing the leader
        if (userId == group.LeaderUserId) return;

        var membership = await context.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
        if (membership == null) return;

        context.GroupMembers.Remove(membership);
        await context.SaveChangesAsync();
    }

    public async Task<List<User>> GetGroupMembersAsync(int groupId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .Select(gm => gm.User!)
            .ToListAsync();
    }

    public async Task<bool> IsLeaderAsync(int groupId, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.TaskGroups.AnyAsync(g => g.Id == groupId && g.LeaderUserId == userId);
    }

    public async Task<bool> IsMemberAsync(int groupId, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
    }

    public async Task<User?> FindUserByUsernameAsync(string username)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Users.OrderBy(u => u.DisplayName ?? u.Username).ToListAsync();
    }
}
