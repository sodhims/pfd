using Microsoft.EntityFrameworkCore;
using PFD.Shared.Models;

namespace PFD.Data.Repositories;

public class ParticipantRepository
{
    private readonly PfdDbContext _context;

    public ParticipantRepository(PfdDbContext context)
    {
        _context = context;
    }

    public async Task<List<Participant>> GetAllAsync()
    {
        return await _context.Participants
            .OrderByDescending(p => p.MeetingCount)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Participant>> GetFrequentParticipantsAsync(int days = 30, int limit = 10)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        return await _context.Participants
            .Where(p => p.LastMeetingDate >= cutoffDate || p.MeetingCount > 0)
            .OrderByDescending(p => p.MeetingCount)
            .ThenByDescending(p => p.LastMeetingDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Participant?> GetByNameAsync(string name)
    {
        return await _context.Participants
            .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());
    }

    public async Task<Participant> GetOrCreateAsync(string name, string? email = null)
    {
        var participant = await GetByNameAsync(name);
        if (participant == null)
        {
            participant = new Participant
            {
                Name = name,
                Email = email,
                MeetingCount = 0,
                CreatedAt = DateTime.UtcNow
            };
            _context.Participants.Add(participant);
            await _context.SaveChangesAsync();
        }
        return participant;
    }

    public async Task IncrementMeetingCountAsync(int participantId)
    {
        var participant = await _context.Participants.FindAsync(participantId);
        if (participant != null)
        {
            participant.MeetingCount++;
            participant.LastMeetingDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
