namespace ZamzamWaterDelivery.Domain.Enums;

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Preparing = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum PaymentMethod
{
    CashOnDelivery = 0,
    MobileMoney = 1,
    OnlinePayment = 2
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3
}

public enum DriverStatus
{
    Available = 0,
    Busy = 1,
    Offline = 2
}

public enum DeliveryStatus
{
    Unassigned = 0,
    Assigned = 1,
    Accepted = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum NotificationType
{
    NewOrder = 0,
    OrderConfirmed = 1,
    DriverAssigned = 2,
    OutForDelivery = 3,
    OrderDelivered = 4,
    OrderCancelled = 5,
    General = 6
}

public enum NotificationChannel
{
    InApp = 0,
    Sms = 1,
    WhatsApp = 2,
    Email = 3
}

public enum ReportType
{
    DailySales = 0,
    MonthlySales = 1,
    Orders = 2,
    Customers = 3,
    ProductSales = 4,
    DriverDeliveries = 5
}

public enum ExportFormat
{
    Csv = 0,
    Excel = 1,
    Pdf = 2
}
