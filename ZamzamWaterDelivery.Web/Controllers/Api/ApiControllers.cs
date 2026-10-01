using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Web.Infrastructure;

namespace ZamzamWaterDelivery.Web.Controllers.Api;

/// <summary>Base for REST endpoints: JWT bearer auth, JSON, no antiforgery (no cookies are accepted).</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[IgnoreAntiforgeryToken]
[SuppressLinkGeneration]
public abstract class ApiControllerBase : ControllerBase
{
    protected string UserId => User.GetUserId();

    protected static ApiResponse<T> Success<T>(T data, string? message = null) => ApiResponse<T>.Ok(data, message);
}

/// <summary>Registration and login (returns a JWT).</summary>
[AllowAnonymous]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Registers a new customer and returns an access token.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterDto dto, CancellationToken ct)
    {
        await _auth.RegisterCustomerAsync(dto, ct);
        var result = await _auth.LoginForTokenAsync(new LoginDto { Email = dto.Email, Password = dto.Password }, ct);
        return StatusCode(StatusCodes.Status201Created, Success(result, "Registration successful."));
    }

    /// <summary>Authenticates a user and returns a bearer token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ApiResponse<AuthResultDto>> Login(LoginDto dto, CancellationToken ct) =>
        Success(await _auth.LoginForTokenAsync(dto, ct), "Login successful.");
}

public class ProductsController : ApiControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    /// <summary>Lists products. Anonymous users and customers only see active products.</summary>
    [HttpGet, AllowAnonymous]
    public async Task<ApiResponse<PagedResult<ProductDto>>> GetAll([FromQuery] ProductFilter filter, CancellationToken ct)
    {
        if (!User.IsInRole(Roles.Admin)) filter.IsActive = true;
        return Success(await _products.GetProductsAsync(filter, ct));
    }

    [HttpGet("{id:int}"), AllowAnonymous]
    public async Task<ApiResponse<ProductDto>> Get(int id, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(id, ct);
        if (!product.IsActive && !User.IsInRole(Roles.Admin)) throw new NotFoundException("Product", id);
        return Success(product);
    }

    [HttpPost, Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(ProductUpsertDto dto, CancellationToken ct)
    {
        var product = await _products.CreateAsync(dto, ct);
        return Created($"/api/products/{product.Id}", Success(product, "Product created."));
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse<ProductDto>> Update(int id, ProductUpsertDto dto, CancellationToken ct) =>
        Success(await _products.UpdateAsync(id, dto, ct), "Product updated.");

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse> Delete(int id, CancellationToken ct)
    {
        var removed = await _products.DeleteAsync(id, ct);
        return ApiResponse.Ok(removed ? "Product deleted." : "Product deleted (archived because past orders include it).");
    }
}

public class OrdersController : ApiControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) => _orders = orders;

    /// <summary>Admins search all orders; customers get their own orders.</summary>
    [HttpGet, Authorize(Roles = $"{Roles.Admin},{Roles.Customer}", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> GetAll([FromQuery] OrderFilter filter, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Admin)) return Ok(Success(await _orders.SearchAsync(filter, ct)));
        return Ok(Success(await _orders.GetCustomerOrdersAsync(UserId, ct)));
    }

    [HttpGet("{id:int}"), Authorize(Roles = $"{Roles.Admin},{Roles.Customer}", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse<OrderDetailsDto>> Get(int id, CancellationToken ct) =>
        Success(User.IsInRole(Roles.Admin)
            ? await _orders.GetDetailsAsync(id, ct)
            : await _orders.GetCustomerOrderDetailsAsync(UserId, id, ct));

    /// <summary>Places an order for the signed-in customer.</summary>
    [HttpPost, Authorize(Roles = Roles.Customer, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(ApiResponse<OrderDetailsDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateOrderDto dto, CancellationToken ct)
    {
        var order = await _orders.PlaceOrderAsync(UserId, dto, ct);
        return Created($"/api/orders/{order.Id}", Success(order, "Order placed."));
    }

    /// <summary>Changes the order status (admin).</summary>
    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse<OrderDetailsDto>> UpdateStatus(int id, UpdateOrderStatusDto dto, CancellationToken ct)
    {
        await _orders.ChangeStatusAsync(id, dto.Status, dto.Reason, ct);
        return Success(await _orders.GetDetailsAsync(id, ct), "Order status updated.");
    }

    [HttpPost("{id:int}/assign-driver"), Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse<OrderDetailsDto>> AssignDriver(int id, AssignDriverDto dto, CancellationToken ct)
    {
        await _orders.AssignDriverAsync(id, dto.DriverId, ct);
        return Success(await _orders.GetDetailsAsync(id, ct), "Driver assigned.");
    }

    [HttpPost("{id:int}/cancel"), Authorize(Roles = $"{Roles.Admin},{Roles.Customer}", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse> Cancel(int id, CancelOrderDto dto, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Admin)) await _orders.CancelAsync(id, dto.Reason, ct);
        else await _orders.CancelByCustomerAsync(UserId, id, dto.Reason, ct);
        return ApiResponse.Ok("Order cancelled.");
    }
}

[Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CustomersController : ApiControllerBase
{
    private readonly ICustomerService _customers;

    public CustomersController(ICustomerService customers) => _customers = customers;

    [HttpGet]
    public async Task<ApiResponse<PagedResult<CustomerDto>>> GetAll([FromQuery] CustomerFilter filter, CancellationToken ct) =>
        Success(await _customers.SearchAsync(filter, ct));

    [HttpGet("{id:int}")]
    public async Task<ApiResponse<CustomerDetailsDto>> Get(int id, CancellationToken ct) =>
        Success(await _customers.GetDetailsAsync(id, ct));
}

[Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DriversController : ApiControllerBase
{
    private readonly IDriverService _drivers;

    public DriversController(IDriverService drivers) => _drivers = drivers;

    [HttpGet]
    public async Task<ApiResponse<PagedResult<DriverDto>>> GetAll([FromQuery] DriverFilter filter, CancellationToken ct) =>
        Success(await _drivers.SearchAsync(filter, ct));

    [HttpGet("{id:int}")]
    public async Task<ApiResponse<DriverDto>> Get(int id, CancellationToken ct) => Success(await _drivers.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DriverDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(DriverCreateDto dto, CancellationToken ct)
    {
        var driver = await _drivers.CreateAsync(dto, ct);
        return Created($"/api/drivers/{driver.Id}", Success(driver, "Driver created."));
    }

    [HttpPut("{id:int}")]
    public async Task<ApiResponse<DriverDto>> Update(int id, DriverUpdateDto dto, CancellationToken ct) =>
        Success(await _drivers.UpdateAsync(id, dto, ct), "Driver updated.");
}

public class DeliveriesController : ApiControllerBase
{
    private readonly IDeliveryService _deliveries;

    public DeliveriesController(IDeliveryService deliveries) => _deliveries = deliveries;

    [HttpGet, Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse<PagedResult<DeliveryDto>>> GetAll([FromQuery] DeliveryFilter filter, CancellationToken ct) =>
        Success(await _deliveries.SearchAsync(filter, ct));

    [HttpGet("{id:int}"), Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse<DeliveryDto>> Get(int id, CancellationToken ct) => Success(await _deliveries.GetByIdAsync(id, ct));

    /// <summary>Updates schedule, fee or notes, and assigns/reassigns the driver.</summary>
    [HttpPut("{id:int}"), Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse<DeliveryDto>> Update(int id, DeliveryUpdateDto dto, CancellationToken ct)
    {
        await _deliveries.UpdateAsync(id, dto, ct);
        return Success(await _deliveries.GetByIdAsync(id, ct), "Delivery updated.");
    }

    /// <summary>Deliveries assigned to the signed-in driver.</summary>
    [HttpGet("mine"), Authorize(Roles = Roles.Driver, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse<IReadOnlyList<DeliveryDto>>> Mine([FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        Success(await _deliveries.GetDriverDeliveriesAsync(UserId, activeOnly, ct));

    [HttpPost("{id:int}/accept"), Authorize(Roles = Roles.Driver, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse> Accept(int id, CancellationToken ct)
    {
        await _deliveries.AcceptAsync(UserId, id, ct);
        return ApiResponse.Ok("Delivery accepted.");
    }

    [HttpPost("{id:int}/start"), Authorize(Roles = Roles.Driver, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse> Start(int id, CancellationToken ct)
    {
        await _deliveries.StartAsync(UserId, id, ct);
        return ApiResponse.Ok("Out for delivery.");
    }

    [HttpPost("{id:int}/complete"), Authorize(Roles = Roles.Driver, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ApiResponse> Complete(int id, CancellationToken ct)
    {
        await _deliveries.CompleteAsync(UserId, id, ct);
        return ApiResponse.Ok("Delivered.");
    }
}

[Authorize(Roles = Roles.Admin, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PaymentsController : ApiControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments) => _payments = payments;

    [HttpGet]
    public async Task<ApiResponse<PagedResult<PaymentDto>>> GetAll([FromQuery] PaymentFilter filter, CancellationToken ct) =>
        Success(await _payments.SearchAsync(filter, ct));

    /// <summary>Records a payment received for an order (e.g. cash collected or a mobile money reference).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(PaymentCreateDto dto, CancellationToken ct)
    {
        var payment = await _payments.RecordPaymentAsync(dto, ct);
        return StatusCode(StatusCodes.Status201Created, Success(payment, "Payment recorded."));
    }
}

/// <summary>
/// Keeps API endpoints out of URL generation so MVC links such as asp-action="Create" or "Cancel"
/// never resolve to an API route that happens to share the action name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SuppressLinkGenerationAttribute : Attribute, Microsoft.AspNetCore.Routing.ISuppressLinkGenerationMetadata
{
    public bool SuppressLinkGeneration => true;
}