namespace ZamzamWaterDelivery.Domain.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
    public const string Driver = "Driver";

    public static readonly string[] All = { Admin, Customer, Driver };
}
