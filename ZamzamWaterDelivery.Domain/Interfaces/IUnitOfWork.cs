using ZamzamWaterDelivery.Domain.Entities;

namespace ZamzamWaterDelivery.Domain.Interfaces;

public interface IUnitOfWork
{
    IRepository<Customer> Customers { get; }
    IRepository<Address> Addresses { get; }
    IRepository<Driver> Drivers { get; }
    IRepository<Category> Categories { get; }
    IRepository<Product> Products { get; }
    IRepository<Cart> Carts { get; }
    IRepository<CartItem> CartItems { get; }
    IRepository<Order> Orders { get; }
    IRepository<OrderItem> OrderItems { get; }
    IRepository<Delivery> Deliveries { get; }
    IRepository<Payment> Payments { get; }
    IRepository<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Runs the work inside a database transaction: commits on success, rolls back on error.</summary>
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);
}
