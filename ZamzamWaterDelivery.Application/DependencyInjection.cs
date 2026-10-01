using Microsoft.Extensions.DependencyInjection;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Application.Services;
using ZamzamWaterDelivery.Application.Services.Payments;

namespace ZamzamWaterDelivery.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuthService, AuthService>();

        // Payment gateways: register additional IPaymentGateway implementations here
        // (e.g. EVC Plus / Zaad mobile money) to enable more methods at checkout.
        services.AddScoped<IPaymentGateway, CashOnDeliveryGateway>();

        return services;
    }
}
