using FlowPress.Models;
using FlowPress.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlowPress.Pages.Instances;

[Authorize]
public class WordPressInstances(SupabaseService supabaseService) : PageModel
{
    public List<InstancesModel>? Instances;

    public async Task OnGet()
    {
        Instances = await supabaseService.SelectInstancesAsync();
    }
}