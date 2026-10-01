using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Web.Models;

namespace ZamzamWaterDelivery.Web.Areas.Admin.Controllers;

public class DeliveriesController : AdminController
{
    private readonly IDeliveryService _deliveries;
    private readonly IDriverService _drivers;
    private readonly DeliverySettings _settings;

    public DeliveriesController(IDeliveryService deliveries, IDriverService drivers, IOptions<DeliverySettings> settings)
    {
        _deliveries = deliveries;
        _drivers = drivers;
        _settings = settings.Value;
    }

    public async Task<IActionResult> Index([FromQuery] DeliveryFilter filter, CancellationToken ct)
    {
        var drivers = await _drivers.SearchAsync(new DriverFilter { PageSize = 100 }, ct);
        return View(new ListViewModel<DeliveryDto, DeliveryFilter>
        {
            Filter = filter,
            Result = await _deliveries.SearchAsync(filter, ct),
            Lookup = drivers.Items.Select(d => new SelectListItem(d.FullName, d.Id.ToString(), d.Id == filter.DriverId))
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var delivery = await _deliveries.GetByIdAsync(id, ct);
        return View(await BuildAsync(delivery, new DeliveryUpdateDto
        {
            DriverId = delivery.DriverId,
            DeliveryDate = delivery.DeliveryDate,
            DeliveryTime = delivery.DeliveryTime,
            DeliveryFee = delivery.DeliveryFee,
            Notes = delivery.Notes
        }, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = nameof(DeliveryEditViewModel.Form))] DeliveryUpdateDto form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(await BuildAsync(await _deliveries.GetByIdAsync(id, ct), form, ct));

        await _deliveries.UpdateAsync(id, form, ct);
        Success("Delivery updated.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<DeliveryEditViewModel> BuildAsync(DeliveryDto delivery, DeliveryUpdateDto form, CancellationToken ct) => new()
    {
        Delivery = delivery,
        Form = form,
        Drivers = await DriverOptionsAsync(_drivers, form.DriverId, ct),
        TimeSlots = _settings.AvailableTimeSlots
    };
}

public class PaymentsController : AdminController
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments) => _payments = payments;

    public async Task<IActionResult> Index([FromQuery] PaymentFilter filter, CancellationToken ct) =>
        View(new ListViewModel<PaymentDto, PaymentFilter> { Filter = filter, Result = await _payments.SearchAsync(filter, ct) });

    [HttpPost]
    public async Task<IActionResult> MarkPaid(int id, string? transactionReference, CancellationToken ct)
    {
        await _payments.MarkAsPaidAsync(id, transactionReference, ct);
        Success("Payment marked as paid.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Refund(int id, CancellationToken ct)
    {
        await _payments.RefundAsync(id, ct);
        Success("Payment refunded.");
        return RedirectToAction(nameof(Index));
    }
}

public class ReportsController : AdminController
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    public async Task<IActionResult> Index([FromQuery] ReportRequest request, bool generate, CancellationToken ct)
    {
        var model = new ReportViewModel { Request = request };
        if (generate && ModelState.IsValid) model.Report = await _reports.GenerateAsync(request, ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] ReportRequest request, ExportFormat format, CancellationToken ct)
    {
        var file = await _reports.ExportAsync(request, format, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
