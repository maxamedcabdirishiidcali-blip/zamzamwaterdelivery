using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Application.Mappings;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _users;

    public CustomerService(IUnitOfWork uow, UserManager<ApplicationUser> users)
    {
        _uow = uow;
        _users = users;
    }

    public async Task<PagedResult<CustomerDto>> SearchAsync(CustomerFilter filter, CancellationToken ct = default)
    {
        var query = _uow.Customers.QueryNoTracking().Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(c => c.FullName.Contains(term) || c.Email.Contains(term) || c.PhoneNumber.Contains(term));
        }
        if (filter.IsActive.HasValue) query = query.Where(c => c.IsActive == filter.IsActive);
        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim();
            query = query.Where(c => c.Addresses.Any(a => a.City.Contains(city)));
        }

        return await query.OrderByDescending(c => c.CreatedAt)
            .Select(Projections.Customer)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);
    }

    public async Task<CustomerDetailsDto> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        var summary = await _uow.Customers.QueryNoTracking().Where(c => c.Id == id && !c.IsDeleted).Select(Projections.Customer).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Customer", id);

        var addresses = await _uow.Addresses.QueryNoTracking()
            .Where(a => a.CustomerId == id)
            .OrderByDescending(a => a.IsDefault)
            .Select(a => new AddressDto
            {
                Id = a.Id, AddressLine = a.AddressLine, City = a.City, Location = a.Location, IsDefault = a.IsDefault
            })
            .ToListAsync(ct);

        var orders = await _uow.Orders.QueryNoTracking()
            .Where(o => o.CustomerId == id)
            .OrderByDescending(o => o.OrderDate)
            .Select(Projections.Order)
            .ToListAsync(ct);

        var user = await _users.FindByIdAsync(summary.UserId);

        return new CustomerDetailsDto
        {
            Id = summary.Id,
            UserId = summary.UserId,
            FullName = summary.FullName,
            Email = summary.Email,
            PhoneNumber = summary.PhoneNumber,
            IsActive = summary.IsActive,
            AddressLine = summary.AddressLine,
            City = summary.City,
            Location = summary.Location,
            OrderCount = summary.OrderCount,
            TotalSpent = summary.TotalSpent,
            CreatedAt = summary.CreatedAt,
            LastLoginAt = user?.LastLoginAt,
            Addresses = addresses,
            Orders = orders
        };
    }

    public async Task<CustomerDto?> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
        await _uow.Customers.QueryNoTracking().Where(c => c.UserId == userId).Select(Projections.Customer).FirstOrDefaultAsync(ct);

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        var customer = await LoadAsync(id, ct);
        customer.IsActive = isActive;
        customer.UpdatedAt = DateTime.Now;
        await UserAccountHelper.SetActiveAsync(_users, customer.User, isActive);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(int id, CustomerUpdateDto dto, CancellationToken ct = default)
    {
        var customer = await LoadAsync(id, ct);
        var email = dto.Email.Trim();
        var phone = dto.PhoneNumber.Trim();

        if (!string.Equals(customer.User.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _users.FindByEmailAsync(email);
            if (existing is not null && existing.Id != customer.UserId)
                throw new BusinessRuleException($"An account with the email '{email}' already exists.");
            customer.User.Email = email;
            customer.User.UserName = email;
        }
        if (await _uow.Customers.QueryNoTracking().AnyAsync(c => c.PhoneNumber == phone && c.Id != id && !c.IsDeleted, ct))
            throw new BusinessRuleException("This phone number is already used by another customer.");

        customer.FullName = dto.FullName.Trim();
        customer.Email = email;
        customer.PhoneNumber = phone;
        customer.UpdatedAt = DateTime.Now;
        customer.User.FullName = customer.FullName;
        customer.User.PhoneNumber = phone;

        var address = customer.Addresses.OrderByDescending(a => a.IsDefault).FirstOrDefault();
        if (address is null)
        {
            address = new Address { IsDefault = true };
            customer.Addresses.Add(address);
        }
        address.AddressLine = dto.AddressLine.Trim();
        address.City = dto.City.Trim();
        address.Location = dto.Location.Trim();

        UserAccountHelper.EnsureSucceeded(await _users.UpdateAsync(customer.User), "Could not update the customer.");
        if (customer.IsActive != dto.IsActive)
        {
            customer.IsActive = dto.IsActive;
            await UserAccountHelper.SetActiveAsync(_users, customer.User, dto.IsActive);
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var customer = await LoadAsync(id, ct);

        // Customers with orders are archived so the order history and reports stay correct.
        if (await _uow.Orders.QueryNoTracking().AnyAsync(o => o.CustomerId == id, ct))
        {
            if (await _uow.Orders.QueryNoTracking().AnyAsync(o => o.CustomerId == id &&
                    o.Status != Domain.Enums.OrderStatus.Delivered && o.Status != Domain.Enums.OrderStatus.Cancelled, ct))
                throw new BusinessRuleException($"{customer.FullName} has open orders. Deliver or cancel them before deleting.");

            customer.IsDeleted = true;
            customer.IsActive = false;
            customer.UpdatedAt = DateTime.Now;
            await UserAccountHelper.ArchiveLoginAsync(_users, customer.User);
            await _uow.SaveChangesAsync(ct);
            return false;
        }

        // Deleting the login cascades to the customer, cart, addresses and notifications.
        UserAccountHelper.EnsureSucceeded(await _users.DeleteAsync(customer.User), "Could not delete the customer.");
        return true;
    }

    private async Task<Customer> LoadAsync(int id, CancellationToken ct) =>
        await _uow.Customers.Query()
            .Include(c => c.User)
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
        ?? throw new NotFoundException("Customer", id);
}
