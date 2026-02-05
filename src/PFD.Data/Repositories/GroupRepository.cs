using Microsoft.EntityFrameworkCore;
using PFD.Shared.Models;

namespace PFD.Data.Repositories;

public class GroupRepository
{
    private readonly PfdDbContext _context;

    public GroupRepository(PfdDbContext context)
    {
        _context = context;
    }

    public async Task<TaskGroup> CreateGroupAsync(string name, int leaderUserId)
    {
        var group = new TaskGroup
        {
            Name = name,
            LeaderUserId = leaderUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskGroups.Add(group);
        await _context.SaveChangesAsync();

        // Auto-add leader as a member
        var membership = new GroupMember
        {
            GroupId = group.Id,
            UserId = leaderUserId,
            JoinedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(membership);
        await _context.SaveChangesAsync();

        return group;
    }

    public async Task<List<TaskGroup>> GetGroupsForUserAsync(int userId)
    {
        var groupIds = await _context.GroupMembers
            .Where(gm => gm.UserId == userId)
            .Select(gm => gm.GroupId)
            .ToListAsync();

        return await _context.TaskGroups
            .Where(g => groupIds.Contains(g.Id))
            .Include(g => g.Members)
            .ToListAsync();
    }

    public async Task<TaskGroup?> GetGroupByIdAsync(int groupId)
    {
        return await _context.TaskGroups
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);
    }

    public async Task DeleteGroupAsync(int groupId, int leaderUserId)
    {
        var group = await _context.TaskGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.LeaderUserId == leaderUserId);
        if (group == null) return;

        // Clear GroupId on any shared tasks (revert to personal)
        var sharedTasks = await _context.DailyTasks.Where(t => t.GroupId == groupId).ToListAsync();
        foreach (var task in sharedTasks)
        {
            task.GroupId = null;
        }

        // Remove all group members first (foreign key constraint)
        var members = await _context.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync();
        _context.GroupMembers.RemoveRange(members);

        _context.TaskGroups.Remove(group);
        await _context.SaveChangesAsync();
    }

    public async Task RenameGroupAsync(int groupId, string newName, int leaderUserId)
    {
        var group = await _context.TaskGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.LeaderUserId == leaderUserId);
        if (group == null) return;

        group.Name = newName;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> AddMemberAsync(int groupId, int userId, int requestingUserId)
    {
        // Only leader can add members
        var group = await _context.TaskGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.LeaderUserId == requestingUserId);
        if (group == null) return false;

        // Check if already a member
        var existing = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
        if (existing) return false;

        _context.GroupMembers.Add(new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task RemoveMemberAsync(int groupId, int userId, int requestingUserId)
    {
        // Leader can remove anyone; members can remove themselves
        var group = await _context.TaskGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null) return;

        if (requestingUserId != group.LeaderUserId && requestingUserId != userId)
            return;

        // Don't allow removing the leader
        if (userId == group.LeaderUserId) return;

        var membership = await _context.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
        if (membership == null) return;

        _context.GroupMembers.Remove(membership);
        await _context.SaveChangesAsync();
    }

    public async Task<List<User>> GetGroupMembersAsync(int groupId)
    {
        return await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .Select(gm => gm.User!)
            .ToListAsync();
    }

    public async Task<bool> IsLeaderAsync(int groupId, int userId)
    {
        return await _context.TaskGroups.AnyAsync(g => g.Id == groupId && g.LeaderUserId == userId);
    }

    public async Task<bool> IsMemberAsync(int groupId, int userId)
    {
        return await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
    }

    public async Task<User?> FindUserByUsernameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users.OrderBy(u => u.DisplayName ?? u.Username).ToListAsync();
    }
}
