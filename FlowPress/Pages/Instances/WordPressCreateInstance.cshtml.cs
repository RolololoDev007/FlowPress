using System.Security.Claims;
using FlowPress.Models;
using FlowPress.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Supabase.Postgrest.Exceptions;

namespace FlowPress.Pages.Instances;

[Authorize]
public class WordPressCreateInstance(
    SupabaseService supabaseService,
    WordPressProvisioningService provisioningService) : PageModel
{
    [BindProperty] public CreateInstanceInputModel Input { get; set; } = new();

    public void OnGet()
    {
        Input.AdminEmail = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        Input.AdminUser = User.FindFirstValue("username") ?? string.Empty;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Page();

        var normalizedSubdomain = Input.SiteAddress.Trim().ToLowerInvariant();
        var siteAddressToCheck = normalizedSubdomain + ".flowpress.site";
        var existingInstance = await supabaseService.SelectInstancesSiteAddressAsync(siteAddressToCheck);

        if (existingInstance != null)
        {
            ModelState.AddModelError("Input.SiteAddress", "Esta dirección ya está en uso.");
            return Page();
        }

        var userId = User.FindFirstValue("userid");
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        try
        {
            var instanceId = await CreateInstanceAsync(userId, normalizedSubdomain);
            await provisioningService.ProvisionAsync(instanceId, Input.AdminPassword, cancellationToken);
            return RedirectToPage("WordPressInstanceInfo", new { id = instanceId });
        }
        catch (PostgrestException)
        {
            ModelState.AddModelError(string.Empty, "Ha ocurrido un error al crear la instancia.");
            return Page();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(
                string.Empty,
                $"La instancia se creó, pero falló el aprovisionamiento automático: {ex.Message}");
            return Page();
        }
    }

    private async Task<Guid> CreateInstanceAsync(string userId, string normalizedSubdomain)
    {
        var instance = new InstancesModel
        {
            IdUser = userId,
            SiteName = Input.SiteName.Trim(),
            SiteAddress = normalizedSubdomain + ".flowpress.site",
            WpAdminUser = Input.AdminUser.Trim(),
            WpAdminEmail = Input.AdminEmail.Trim(),
            DockerStatus = "pending",
            ProvisioningStatus = "pending"
        };

        await supabaseService.InsertAsync(instance);
        return instance.Id;
    }
}
