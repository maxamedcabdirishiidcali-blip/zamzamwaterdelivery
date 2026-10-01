using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Application.Mappings;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Enums;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

public class DriverService : IDriverService
{
    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _users;

    public DriverService(IUnitOfWork uow, UserManager<ApplicationUser> users)
    {
        _uow = uow;
        _users = users;
    }

    public async Task<PagedResult<DriverDto>> SearchAsync(DriverFilter filter, CancellationToken ct = default)
    {
        var query = _uow.Drivers.QueryNoTracking().Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(d => d.FullName.Contains(term) || d.PhoneNumber.Contains(term) || d.Email.Contains(term) ||
                                     d.VehicleNumber.Contains(term) || d.LicenseNumber.Contains(term));
        }
        if (filter.Status.HasValue) query = query.Where(d => d.Status == filter.Status);

        return await query.OrderBy(d => d.Status).ThenBy(d => d.FullName)
            .Select(Projections.Driver)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);
    }

    public async Task<IReadOnlyList<DriverDto>> GetAssignableDriversAsync(CancellationToken ct = default) =>
        await _uow.Drivers.QueryNoTracking()
            .Where(d => !d.IsDeleted && d.User.IsActive && d.Status != DriverStatus.Offline)
            .OrderBy(d => d.Status).ThenBy(d => d.FullName)
            .Select(Projections.Driver)
            .ToListAsync(ct);

    public async Task<DriverDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _uow.Drivers.QueryNoTracking().Where(d => d.Id == id && !d.IsDeleted).Select(Projections.Driver).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Driver", id);

    public async Task<DriverDto> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
        await _uow.Drivers.QueryNoTracking().Where(d => d.UserId == userId && !d.IsDeleted).Select(Projections.Driver).FirstOrDefaultAsync(ct)
        ?? throw new ForbiddenAccessException("No driver profile is linked to this account.");

    public async Task<DriverDto> CreateAsync(DriverCreateDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim();
        if (await _users.FindByEmailAsync(email) is not null)
            throw new BusinessRuleException($"An account with the email '{email}' already exists.");

        var driverId = 0;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = dto.FullName.Trim(),
                PhoneNumber = dto.PhoneNumber.Trim(),
                IsActive = true
            };
            UserAccountHelper.EnsureSucceeded(await _users.CreateAsync(user, dto.Password), "Could not create the driver account.");
            UserAccountHelper.EnsureSucceeded(await _users.AddToRoleAsync(user, Roles.Driver), "Could not assign the driver role.");

            var driver = new Driver { UserId = user.Id };
            Apply(driver, dto);
            await _uow.Drivers.AddAsync(driver, ct);
            await _uow.SaveChangesAsync(ct);
            driverId = driver.Id;
        }, ct);

        return await GetByIdAsync(driverId, ct);
    }

    public async Task<DriverDto> UpdateAsync(int id, DriverUpdateDto dto, CancellationToken ct = default)
    {
        var driver = await LoadWithUserAsync(id, ct);
        var email = dto.Email.Trim();

        if (!string.Equals(driver.User.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _users.FindByEmailAsync(email);
            if (existing is not null && existing.Id != driver.UserId)
                throw new BusinessRuleException($"An account with the email '{email}' already exists.");
            driver.User.Email = email;
            driver.User.UserName = email;
        }

        driver.User.FullName = dto.FullName.Trim();
        driver.User.PhoneNumber = dto.PhoneNumber.Trim();
        Apply(driver, dto);
        driver.UpdatedAt = DateTime.Now;

        UserAccountHelper.EnsureSucceeded(await _users.UpdateAsync(driver.User), "Could not update the driver account.");
        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var driver = await LoadWithUserAsync(id, ct);
        var deliveries = _uow.Deliveries.QueryNoTracking().Where(d => d.DriverId == id);

        if (await deliveries.AnyAsync(d => d.Status == DeliveryStatus.Assigned || d.Status == DeliveryStatus.Accepted ||
                                          d.Status == DeliveryStatus.OutForDelivery, ct))
            throw new BusinessRuleException(
                $"{driver.FullName} has active deliveries. Reassign them to another driver before deleting.");

        // Drivers with delivery history are archived and their login is locked, so reports stay correct.
        if (await deliveries.AnyAsync(ct))
        {
            driver.IsDeleted = true;
            driver.Status = DriverStatus.Offline;
            driver.UpdatedAt = DateTime.Now;
            await UserAccountHelper.ArchiveLoginAsync(_users, driver.User);
            await _uow.SaveChangesAsync(ct);
            return false;
        }

        // Deleting the login cascades to the driver profile.
        UserAccountHelper.EnsureSucceeded(await _users.DeleteAsync(driver.User), "Could not delete the driver.");
        return true;
    }

    public async Task SetStatusAsync(int id, DriverStatus status, CancellationToken ct = default)
    {
        var driver = await _uow.Drivers.GetByIdAsync(id, ct) ?? throw new NotFoundException("Driver", id);
        driver.Status = status;
        driver.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SetMyStatusAsync(string userId, DriverStatus status, CancellationToken ct = default)
    {
        var driver = await _uow.Drivers.Query().FirstOrDefaultAsync(d => d.UserId == userId, ct)
                     ?? throw new ForbiddenAccessException("No driver profile is linked to this account.");
        driver.Status = status;
        driver.UpdatedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        var driver = await LoadWithUserAsync(id, ct);
        if (!isActive) driver.Status = DriverStatus.Offline;
        driver.UpdatedAt = DateTime.Now;
        await UserAccountHelper.SetActiveAsync(_users, driver.User, isActive);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<DriverStatsDto> GetStatsAsync(string userId, CancellationToken ct = default)
    {
        var deliveries = _uow.Deliveries.QueryNoTracking().Where(d => d.Driver != null && d.Driver.UserId == userId);
        var today = DateTime.Today;

        return new DriverStatsDto
        {
            AssignedCount = await deliveries.CountAsync(d => d.Status == DeliveryStatus.Assigned || d.Status == DeliveryStatus.Accepted, ct),
            InProgressCount = await deliveries.CountAsync(d => d.Status == DeliveryStatus.OutForDelivery, ct),
            DeliveredToday = await deliveries.CountAsync(d => d.Status == DeliveryStatus.Delivered && d.DeliveredAt >= today, ct),
            TotalDelivered = await deliveries.CountAsync(d => d.Status == DeliveryStatus.Delivered, ct),
            CashToCollect = await deliveries
                .Where(d => (d.Status == DeliveryStatus.Assigned || d.Status == DeliveryStatus.Accepted || d.Status == DeliveryStatus.OutForDelivery)
                            && d.Order.PaymentMethod == PaymentMethod.CashOnDelivery)
                .SumAsync(d => (decimal?)d.Order.Total, ct) ?? 0
        };
    }

    private async Task<Driver> LoadWithUserAsync(int id, CancellationToken ct) =>
        await _uow.Drivers.Query().Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct)
        ?? throw new NotFoundException("Driver", id);

    private static void Apply(Driver driver, DriverUpdateDto dto)
    {
        driver.FullName = dto.FullName.Trim();
        driver.PhoneNumber = dto.PhoneNumber.Trim();
        driver.Email = dto.Email.Trim();
        driver.VehicleType = dto.VehicleType.Trim();
        driver.VehicleNumber = dto.VehicleNumber.Trim().ToUpperInvariant();
        driver.LicenseNumber = dto.LicenseNumber.Trim().ToUpperInvariant();
        driver.Status = dto.Status;
    }
}
