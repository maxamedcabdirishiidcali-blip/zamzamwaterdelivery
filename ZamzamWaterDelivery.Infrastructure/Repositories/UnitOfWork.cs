using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Interfaces;
using ZamzamWaterDelivery.Infrastructure.Data;

namespace ZamzamWaterDelivery.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(ApplicationDbContext db) => _db = db;

    public IRepository<Customer> Customers => Repo<Customer>();
    public IRepository<Address> Addresses => Repo<Address>();
    public IRepository<Driver> Drivers => Repo<Driver>();
    public IRepository<Category> Categories => Repo<Category>();
    public IRepository<Product> Products => Repo<Product>();
    public IRepository<Cart> Carts => Repo<Cart>();
    public IRepository<CartItem> CartItems => Repo<CartItem>();
    public IRepository<Order> Orders => Repo<Order>();
    public IRepository<OrderItem> OrderItems => Repo<OrderItem>();
    public IRepository<Delivery> Deliveries => Repo<Delivery>();
    public IRepository<Payment> Payments => Repo<Payment>();
    public IRepository<Notification> Notifications => Repo<Notification>();

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        // Join an outer transaction when one is already running.
        if (_db.Database.CurrentTransaction is not null)
        {
            await work();
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await work();
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // Discard pending changes so a failed operation cannot leak into a later SaveChanges.
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    private IRepository<T> Repo<T>() where T : BaseEntity
    {
        if (!_repositories.TryGetValue(typeof(T), out var repo))
        {
            repo = new Repository<T>(_db);
            _repositories[typeof(T)] = repo;
        }
        return (IRepository<T>)repo;
    }
}
