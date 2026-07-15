using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Repositories;

public class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Event>> GetAllAsync()
    {
        return await _context.Events
            .Include(e => e.Category)
            .Include(e => e.Organizer)
            .Include(e => e.Bookings)
            .AsNoTracking()
            .ToListAsync();
    }

    // Paged listing. Counts the total once, then materialises only the requested page
    // (with its RSVP bookings) instead of loading every event and every booking.
    public async Task<(IReadOnlyList<Event> Items, int Total)> GetPagedAsync(int page, int pageSize)
    {
        var baseQuery = _context.Events.AsNoTracking();
        var total = await baseQuery.CountAsync();

        var items = await baseQuery
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(e => e.Category)
            .Include(e => e.Organizer)
            .Include(e => e.Bookings)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Event?> GetByIdAsync(int id)
    {
        return await _context.Events
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Organizer)
            .Include(e => e.Bookings)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<Event>> GetByCategoryIdAsync(int categoryId)
    {
        return await _context.Events
            .Where(e => e.CategoryId == categoryId)
            .Include(e => e.Category)
            .Include(e => e.Organizer)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Event>> GetByOrganizerIdAsync(Guid organizerId)
    {
        return await _context.Events
            .Where(e => e.OrganizerId == organizerId)
            .Include(e => e.Category)
            .Include(e => e.Organizer)
            .Include(e => e.Bookings)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Event> CreateAsync(Event @event)
    {
        _context.Events.Add(@event);
        await _context.SaveChangesAsync();
        return @event;
    }

    public async Task UpdateAsync(Event @event)
    {
        _context.Entry(@event).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var @event = await _context.Events.FindAsync(id);
        if (@event != null)
        {
            _context.Events.Remove(@event);
            await _context.SaveChangesAsync();
        }
    }
}
