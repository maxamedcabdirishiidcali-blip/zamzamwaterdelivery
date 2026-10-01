using Microsoft.AspNetCore.Identity;
using ZamzamWaterDelivery.Application.Common;
using ZamzamWaterDelivery.Domain.Entities;

namespace ZamzamWaterDelivery.Application.Services;

internal static class UserAccountHelper
{
    /// <summary>
    /// Activates or deactivates a login. Deactivation locks the account and rotates the
    /// security stamp so existing sessions are signed out.
    /// </summary>
    public static async Task SetActiveAsync(UserManager<ApplicationUser> users, ApplicationUser user, bool isActive)
    {
        user.IsActive = isActive;
        user.LockoutEnabled = true;
        user.LockoutEnd = isActive ? null : DateTimeOffset.MaxValue;
        EnsureSucceeded(await users.UpdateAsync(user), "Could not update the account.");
        if (!isActive) EnsureSucceeded(await users.UpdateSecurityStampAsync(user), "Could not sign the user out.");
    }

    /// <summary>
    /// Disables an archived account and renames its login so the email address can be registered again.
    /// </summary>
    public static async Task ArchiveLoginAsync(UserManager<ApplicationUser> users, ApplicationUser user)
    {
        var archived = $"deleted.{user.Id}@archived.local";
        user.Email = archived;
        user.UserName = archived;
        await SetActiveAsync(users, user, false);
    }

    public static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
            throw new BusinessRuleException(message, result.Errors.Select(e => e.Description));
    }
}
