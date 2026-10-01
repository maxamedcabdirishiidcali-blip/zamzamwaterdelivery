using ZamzamWaterDelivery.Domain.Enums;

namespace ZamzamWaterDelivery.Domain.Common;

/// <summary>Defines the allowed order status workflow.</summary>
public static class OrderStatusRules
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
        [OrderStatus.Confirmed] = new[] { OrderStatus.Preparing, OrderStatus.OutForDelivery, OrderStatus.Cancelled },
        [OrderStatus.Preparing] = new[] { OrderStatus.OutForDelivery, OrderStatus.Cancelled },
        [OrderStatus.OutForDelivery] = new[] { OrderStatus.Delivered },
        [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    /// <summary>The ordered steps shown on the tracking progress bar.</summary>
    public static readonly OrderStatus[] TrackingSteps =
    {
        OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Preparing,
        OrderStatus.OutForDelivery, OrderStatus.Delivered
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static IReadOnlyList<OrderStatus> NextStatuses(OrderStatus from) =>
        Transitions.TryGetValue(from, out var allowed) ? allowed : Array.Empty<OrderStatus>();

    public static bool CanBeCancelled(OrderStatus status) => CanTransition(status, OrderStatus.Cancelled);

    public static bool CanAssignDriver(OrderStatus status) =>
        status is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Preparing;
}
