using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ZamzamWaterDelivery.Application.DTOs;
using ZamzamWaterDelivery.Application.Interfaces;
using ZamzamWaterDelivery.Domain.Common;
using ZamzamWaterDelivery.Domain.Entities;
using ZamzamWaterDelivery.Web.Infrastructure;

namespace ZamzamWaterDelivery.Web.Controllers;

public class AccountController : AppController
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuthService _auth;
    private readonly ILogger<AccountController> _logger;

    public AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users,
        IAuthService auth, ILogger<AccountController> logger)
    {
        _signIn = signIn;
        _users = users;
        _auth = auth;
        _logger = logger;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new RegisterDto());
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(dto);

        var user = await _auth.RegisterCustomerAsync(dto, ct);
        await _signIn.SignInAsync(user, isPersistent: false);
        await _auth.RecordLoginAsync(user);
        Success($"Welcome to Zamzam Water, {user.FullName}! Your account is ready.");
        return RedirectToAction("Index", "Products");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginDto());
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginDto dto, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(dto);

        var user = await _users.FindByEmailAsync(dto.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(dto);
        }
        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact support.");
            return View(dto);
        }

        var result = await _signIn.PasswordSignInAsync(user, dto.Password, dto.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            _logger.LogWarning("Account {UserId} locked out", user.Id);
            ModelState.AddModelError(string.Empty, "Too many failed attempts. Please try again in 15 minutes.");
            return View(dto);
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(dto);
        }

        await _auth.RecordLoginAsync(user);
        var roles = await _users.GetRolesAsync(user);
        if (roles.Contains(Roles.Admin)) return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        if (roles.Contains(Roles.Driver)) return RedirectToAction("Index", "Dashboard", new { area = "Driver" });

        return RedirectToLocal(returnUrl, "Index", "Products");
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        Info("You have been signed out.");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
