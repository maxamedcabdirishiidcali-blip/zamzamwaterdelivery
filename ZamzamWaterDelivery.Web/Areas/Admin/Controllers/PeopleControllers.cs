using Microsoft.AspNetCore.Mvc;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Web.Models;

namespace ZamzamWaterDelivery.Web.Areas.Admin.Controllers;

public class CustomersController : AdminController
{
    private readonly ICustomerService _customers;
    private readonly IAuthService _auth;

    public CustomersController(ICustomerService customers, IAuthService auth)
    {
        _customers = customers;
        _auth = auth;
    }

    [HttpGet]
    public IActionResult Create() => View(new RegisterDto());

    [HttpPost]
    public async Task<IActionResult> Create(RegisterDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(dto);
        var user = await _auth.RegisterCustomerAsync(dto, ct);
        Success($"Customer {user.FullName} was added. They can log in with {user.Email}.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var c = await _customers.GetDetailsAsync(id, ct);
        ViewData["CustomerId"] = id;
        return View(new CustomerUpdateDto
        {
            FullName = c.FullName, PhoneNumber = c.PhoneNumber, Email = c.Email, AddressLine = c.AddressLine ?? string.Empty,
            City = c.City ?? string.Empty, Location = c.Location ?? string.Empty, IsActive = c.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CustomerUpdateDto dto, CancellationToken ct)
    {
        ViewData["CustomerId"] = id;
        if (!ModelState.IsValid) return View(dto);
        await _customers.UpdateAsync(id, dto, ct);
        Success("Customer updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var removed = await _customers.DeleteAsync(id, ct);
        Success(removed ? "Customer deleted." : "Customer deleted. Their past orders are kept for reports.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Index([FromQuery] CustomerFilter filter, CancellationToken ct) =>
        View(new ListViewModel<CustomerDto, CustomerFilter> { Filter = filter, Result = await _customers.SearchAsync(filter, ct) });

    public async Task<IActionResult> Details(int id, CancellationToken ct) => View(await _customers.GetDetailsAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id, bool isActive, CancellationToken ct)
    {
        await _customers.SetActiveAsync(id, isActive, ct);
        Success(isActive ? "Customer account activated." : "Customer account deactivated and signed out.");
        return RedirectToAction(nameof(Details), new { id });
    }
}

public class DriversController : AdminController
{
    private readonly IDriverService _drivers;
    private readonly IDeliveryService _deliveries;

    public DriversController(IDriverService drivers, IDeliveryService deliveries)
    {
        _drivers = drivers;
        _deliveries = deliveries;
    }

    public async Task<IActionResult> Index([FromQuery] DriverFilter filter, CancellationToken ct) =>
        View(new ListViewModel<DriverDto, DriverFilter> { Filter = filter, Result = await _drivers.SearchAsync(filter, ct) });

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var deliveries = await _deliveries.SearchAsync(new DeliveryFilter { DriverId = id, PageSize = 20 }, ct);
        return View(new DriverDetailsViewModel { Driver = await _drivers.GetByIdAsync(id, ct), Deliveries = deliveries.Items });
    }

    [HttpGet]
    public IActionResult Create() => View(new DriverCreateDto());

    [HttpPost]
    public async Task<IActionResult> Create(DriverCreateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(dto);
        var driver = await _drivers.CreateAsync(dto, ct);
        Success($"Driver {driver.FullName} created. They can now log in with {driver.Email}.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var d = await _drivers.GetByIdAsync(id, ct);
        ViewData["DriverId"] = id;
        return View(new DriverUpdateDto
        {
            FullName = d.FullName, PhoneNumber = d.PhoneNumber, Email = d.Email, VehicleType = d.VehicleType,
            VehicleNumber = d.VehicleNumber, LicenseNumber = d.LicenseNumber, Status = d.Status
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, DriverUpdateDto dto, CancellationToken ct)
    {
        ViewData["DriverId"] = id;
        if (!ModelState.IsValid) return View(dto);
        await _drivers.UpdateAsync(id, dto, ct);
        Success("Driver updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, DriverStatus status, CancellationToken ct)
    {
        await _drivers.SetStatusAsync(id, status, ct);
        Success("Driver status updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id, bool isActive, CancellationToken ct)
    {
        await _drivers.SetActiveAsync(id, isActive, ct);
        Success(isActive ? "Driver account activated." : "Driver account deactivated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var removed = await _drivers.DeleteAsync(id, ct);
        Success(removed
            ? "Driver deleted."
            : "Driver deleted. Their login was disabled and delivery history is kept for reports.");
        return RedirectToAction(nameof(Index));
    }
}
