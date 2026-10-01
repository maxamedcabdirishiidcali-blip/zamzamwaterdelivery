using ZamzamWaterDelivery.Domain.Common;

namespace ZamzamWaterDelivery.Domain.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    /// <summary>Tracked query over the entity set (use when the result will be modified).</summary>
    IQueryable<T> Query();

    /// <summary>No-tracking query for read-only scenarios.</summary>
    IQueryable<T> QueryNoTracking();

    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
