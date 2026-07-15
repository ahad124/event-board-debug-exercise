using EventBoard.Api.Models;

namespace EventBoard.Api.Repositories;

public interface IEventRepository
{
    Task<IEnumerable<Event>> GetAllAsync();
    Task<(IReadOnlyList<Event> Items, int Total)> GetPagedAsync(int page, int pageSize);
    Task<Event?> GetByIdAsync(int id);
    Task<IEnumerable<Event>> GetByCategoryIdAsync(int categoryId);
    Task<IEnumerable<Event>> GetByOrganizerIdAsync(Guid organizerId);
    Task<Event> CreateAsync(Event @event);
    Task UpdateAsync(Event @event);
    Task DeleteAsync(int id);
}
