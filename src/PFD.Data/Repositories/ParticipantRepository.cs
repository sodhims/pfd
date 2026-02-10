using Microsoft.EntityFrameworkCore;
using PFD.Shared.Models;

namespace PFD.Data.Repositories;

public class ParticipantRepository
{
    private readonly IDbContextFactory<PfdDbContext> _contextFactory;

    public ParticipantRepository(IDbContextFactory<PfdDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Participant>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Participants
            .OrderByDescending(p => p.MeetingCount)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Participant>> GetFrequentParticipantsAsync(int days = 30, int limit = 10)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        return await context.Participants
            .Where(p => p.LastMeetingDate >= cutoffDate || p.MeetingCount > 0)
            .OrderByDescending(p => p.MeetingCount)
            .ThenByDescending(p => p.LastMeetingDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Participant?> GetByNameAsync(string name)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Participants
            .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());
    }

    public async Task<Participant> GetOrCreateAsync(string name, string? email = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var participant = await context.Participants
            .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());

        if (participant == null)
        {
            participant = new Participant
            {
                Name = name,
                Email = email,
                MeetingCount = 0,
                CreatedAt = DateTime.UtcNow
            };
            context.Participants.Add(participant);
            await context.SaveChangesAsync();
        }
        return participant;
    }

    public async Task IncrementMeetingCountAsync(int participantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var participant = await context.Participants.FindAsync(participantId);
        if (participant != null)
        {
            participant.MeetingCount++;
            participant.LastMeetingDate = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }
}
