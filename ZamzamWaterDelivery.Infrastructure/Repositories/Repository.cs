using Microsoft.EntityFrameworkCore;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Interfaces;
using ZamzamWaterDelivery.Infrastructure.Data;

namespace ZamzamWaterDelivery.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly DbSet<T> _set;

    public Repository(ApplicationDbContext db) => _set = db.Set<T>();

    public IQueryable<T> Query() => _set;

    public IQueryable<T> QueryNoTracking() => _set.AsNoTracking();

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _set.FindAsync(new object[] { id }, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) => await _set.AddAsync(entity, ct);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);
}
