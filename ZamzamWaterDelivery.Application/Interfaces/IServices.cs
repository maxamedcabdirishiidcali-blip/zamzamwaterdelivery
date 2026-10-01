using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Application.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(ProductFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetActiveProductsAsync(CancellationToken ct = default);
    Task<ProductDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(ProductUpsertDto dto, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(int id, ProductUpsertDto dto, CancellationToken ct = default);
    /// <summary>Deletes the product; returns false when it was archived because it has order history.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default);
    Task UpdateStockAsync(int id, int stockQuantity, CancellationToken ct = default);
    Task UpdatePriceAsync(int id, decimal price, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CategoryUpsertDto dto, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(int id, CategoryUpsertDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public interface ICartService
{
    Task<CartDto> GetCartAsync(string userId, CancellationToken ct = default);
    Task<CartSummaryDto> AddItemAsync(string userId, int productId, int quantity, CancellationToken ct = default);
    Task<CartSummaryDto> UpdateQuantityAsync(string userId, int cartItemId, int quantity, CancellationToken ct = default);
    Task<CartSummaryDto> RemoveItemAsync(string userId, int cartItemId, CancellationToken ct = default);
    Task ClearAsync(string userId, CancellationToken ct = default);
    Task<int> GetItemCountAsync(string userId, CancellationToken ct = default);
}

public interface IOrderService
{
    Task<OrderDetailsDto> PlaceOrderFromCartAsync(string userId, CheckoutDto dto, CancellationToken ct = default);
    Task<OrderDetailsDto> PlaceOrderAsync(string userId, CreateOrderDto dto, CancellationToken ct = default);
    Task<CheckoutDto> GetCheckoutDefaultsAsync(string userId, CancellationToken ct = default);

    Task<PagedResult<OrderDto>> SearchAsync(OrderFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(string userId, CancellationToken ct = default);
    Task<OrderDetailsDto> GetDetailsAsync(int orderId, CancellationToken ct = default);
    Task<OrderDetailsDto> GetCustomerOrderDetailsAsync(string userId, int orderId, CancellationToken ct = default);
    Task<OrderDetailsDto> TrackAsync(string userId, string orderNumber, CancellationToken ct = default);

    Task ChangeStatusAsync(int orderId, OrderStatus newStatus, string? reason = null, CancellationToken ct = default);
    Task AssignDriverAsync(int orderId, int driverId, CancellationToken ct = default);
    Task CancelAsync(int orderId, string? reason, CancellationToken ct = default);
    Task CancelByCustomerAsync(string userId, int orderId, string? reason, CancellationToken ct = default);
}

public interface IDeliveryService
{
    Task<PagedResult<DeliveryDto>> SearchAsync(DeliveryFilter filter, CancellationToken ct = default);
    Task<DeliveryDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task UpdateAsync(int id, DeliveryUpdateDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<DeliveryDto>> GetDriverDeliveriesAsync(string driverUserId, bool activeOnly, CancellationToken ct = default);
    Task<DeliveryDto> GetDriverDeliveryAsync(string driverUserId, int deliveryId, CancellationToken ct = default);
    Task AcceptAsync(string driverUserId, int deliveryId, CancellationToken ct = default);
    Task StartAsync(string driverUserId, int deliveryId, CancellationToken ct = default);
    Task CompleteAsync(string driverUserId, int deliveryId, CancellationToken ct = default);
}

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> SearchAsync(CustomerFilter filter, CancellationToken ct = default);
    Task<CustomerDetailsDto> GetDetailsAsync(int id, CancellationToken ct = default);
    Task<CustomerDto?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default);
    Task UpdateAsync(int id, CustomerUpdateDto dto, CancellationToken ct = default);
    /// <summary>Deletes the customer; returns false when archived because they have orders.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IDriverService
{
    Task<PagedResult<DriverDto>> SearchAsync(DriverFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<DriverDto>> GetAssignableDriversAsync(CancellationToken ct = default);
    Task<DriverDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<DriverDto> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<DriverDto> CreateAsync(DriverCreateDto dto, CancellationToken ct = default);
    Task<DriverDto> UpdateAsync(int id, DriverUpdateDto dto, CancellationToken ct = default);
    /// <summary>Deletes the driver; returns false when archived because of delivery history.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task SetStatusAsync(int id, DriverStatus status, CancellationToken ct = default);
    Task SetMyStatusAsync(string userId, DriverStatus status, CancellationToken ct = default);
    Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default);
    Task<DriverStatsDto> GetStatsAsync(string userId, CancellationToken ct = default);
}

public interface IPaymentService
{
    /// <summary>Creates the payment record for a new order through the matching gateway.</summary>
    Task<Payment> InitiateAsync(Order order, CancellationToken ct = default);
    Task<PagedResult<PaymentDto>> SearchAsync(PaymentFilter filter, CancellationToken ct = default);
    Task<PaymentDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PaymentDto> RecordPaymentAsync(PaymentCreateDto dto, CancellationToken ct = default);
    Task MarkAsPaidAsync(int id, string? transactionReference, CancellationToken ct = default);
    Task RefundAsync(int id, CancellationToken ct = default);
    IReadOnlyList<PaymentMethod> AvailableMethods { get; }
}

/// <summary>
/// A payment provider. Cash on Delivery is built in; mobile money (EVC Plus, Zaad, Sahal)
/// or card gateways can be added by implementing this interface and registering it in DI.
/// </summary>
public interface IPaymentGateway
{
    PaymentMethod Method { get; }
    Task<PaymentResult> InitiateAsync(Order order, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyUserAsync(string userId, NotificationType type, string title, string message, int? orderId = null, CancellationToken ct = default);
    Task NotifyAdminsAsync(NotificationType type, string title, string message, int? orderId = null, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDto>> GetForUserAsync(string userId, int take = 50, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default);
    Task MarkAsReadAsync(string userId, int notificationId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(string userId, CancellationToken ct = default);
}

/// <summary>External delivery channel for notifications (SMS, WhatsApp, e-mail).</summary>
public interface INotificationChannel
{
    NotificationChannel Channel { get; }
    bool IsEnabled { get; }
    Task SendAsync(NotificationMessage message, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<DashboardChartsDto> GetChartsAsync(CancellationToken ct = default);
}

public interface IReportService
{
    Task<ReportDto> GenerateAsync(ReportRequest request, CancellationToken ct = default);
    Task<ExportedFile> ExportAsync(ReportRequest request, ExportFormat format, CancellationToken ct = default);
}

public interface IReportExporter
{
    ExportFormat Format { get; }
    string ContentType { get; }
    string FileExtension { get; }
    byte[] Export(ReportDto report);
}

public interface IAuthService
{
    Task<ApplicationUser> RegisterCustomerAsync(RegisterDto dto, CancellationToken ct = default);
    Task<AuthResultDto> LoginForTokenAsync(LoginDto dto, CancellationToken ct = default);
    Task RecordLoginAsync(ApplicationUser user);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(ApplicationUser user, IEnumerable<string> roles);
}

public interface IFileStorageService
{
    /// <summary>Saves the file and returns its public relative URL.</summary>
    Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken ct = default);
    void Delete(string? relativeUrl);
}
