using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Domain.Interfaces;

namespace ZamzamWaterDelivery.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IUnitOfWork _uow;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(UserManager<ApplicationUser> users, IUnitOfWork uow, ITokenService tokens, ILogger<AuthService> logger)
    {
        _users = users;
        _uow = uow;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<ApplicationUser> RegisterCustomerAsync(RegisterDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim();
        var phone = dto.PhoneNumber.Trim();

        if (await _users.FindByEmailAsync(email) is not null)
            throw new BusinessRuleException("An account with this email already exists. Please log in instead.");
        if (await _uow.Customers.QueryNoTracking().AnyAsync(c => c.PhoneNumber == phone && !c.IsDeleted, ct))
            throw new BusinessRuleException("This phone number is already registered.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumber = phone,
            FullName = dto.FullName.Trim(),
            IsActive = true
        };

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            UserAccountHelper.EnsureSucceeded(await _users.CreateAsync(user, dto.Password), "Registration failed.");
            UserAccountHelper.EnsureSucceeded(await _users.AddToRoleAsync(user, Roles.Customer), "Registration failed.");

            var customer = new Customer
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = email,
                PhoneNumber = phone,
                Cart = new Cart()
            };
            customer.Addresses.Add(new Address
            {
                AddressLine = dto.AddressLine.Trim(),
                City = dto.City.Trim(),
                Location = dto.Location.Trim(),
                IsDefault = true
            });
            await _uow.Customers.AddAsync(customer, ct);
            await _uow.SaveChangesAsync(ct);
        }, ct);

        _logger.LogInformation("New customer registered: {UserId}", user.Id);
        return user;
    }

    public async Task<AuthResultDto> LoginForTokenAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(dto.Email.Trim());
        if (user is null) throw new AuthenticationFailedException();
        if (!user.IsActive) throw new AuthenticationFailedException("This account has been deactivated. Please contact support.");
        if (await _users.IsLockedOutAsync(user))
            throw new AuthenticationFailedException("Too many failed attempts. Please try again later.");

        if (!await _users.CheckPasswordAsync(user, dto.Password))
        {
            await _users.AccessFailedAsync(user);
            throw new AuthenticationFailedException();
        }

        await _users.ResetAccessFailedCountAsync(user);
        await RecordLoginAsync(user);

        var roles = await _users.GetRolesAsync(user);
        var (token, expiresAt) = _tokens.CreateToken(user, roles);
        return new AuthResultDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Roles = roles
        };
    }

    public async Task RecordLoginAsync(ApplicationUser user)
    {
        user.LastLoginAt = DateTime.Now;
        await _users.UpdateAsync(user);
    }
}
