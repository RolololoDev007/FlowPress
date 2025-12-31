using System.Security.Claims;
using FlowPress.Models;
using FlowPress.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Supabase.Postgrest.Exceptions;

namespace FlowPress.Pages.Instances;

[Authorize]
public class WordPressCreateInstance(SupabaseService supabaseService) : PageModel
{
    [BindProperty] public InstancesModel InstancesModel { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Verificar si el SiteAddress ya existe
        var siteAddressToCheck = InstancesModel.SiteAddress.ToLower() + ".flowpress.site";
        var existingInstances = await supabaseService.SelectInstancesSiteAddressAsync(siteAddressToCheck);

        if (existingInstances != null)
        {
            ModelState.AddModelError("InstancesModel.SiteAddress",
                "Esta dirección ya está en uso.");
            return Page();
        }
        // Obtener el UserId desde los claims
        var userId = User.FindFirstValue("userid");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var instanceId = await CrearInstancia(userId);
            return RedirectToPage("WordPressInstanceInfo", new { id = instanceId });
        }
        catch (PostgrestException ex)
        {
            ModelState.AddModelError(string.Empty, "Ha ocurrido un error al crear la instancia.");
            return Page();
        }
    }

    private async Task<Guid> CrearInstancia(string userId)
    {
        var instancia = new InstancesModel()
        {
            IdUser = userId,
            SiteName = InstancesModel.SiteName,
            SiteAddress = InstancesModel.SiteAddress.ToLower() + ".flowpress.site",
            DockerStatus = "pending"
        };

        await supabaseService.InsertAsync(instancia);
        // INFO
        /// Recogemos el id de la instancia creada para luego poder hacer el redireccionamiento
        return instancia.Id;
    }
}