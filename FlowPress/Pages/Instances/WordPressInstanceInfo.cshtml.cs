using FlowPress.Models;
using FlowPress.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace FlowPress.Pages.Instances;

[Authorize]
public class WordPressInstanceInfo(SupabaseService supabaseService) : PageModel
{
    public InstancesModel? Instance { get; set; }

    public async Task<IActionResult> OnGet(Guid id)
    {
        Instance = await supabaseService.SelectInstanceById(id);

        if (Instance == null)
            return NotFound();

        return Page();
    }
    // ROLO Hacer toda la lógica (Mirar de hacer mediante conexión ssh)
    public IActionResult OnPostStart()
    {
        RunCommand("docker compose start");
        return RedirectToPage();
    }
    public IActionResult OnPostStop()
    {
        RunCommand("docker compose stop");
        return RedirectToPage();
    }
    public IActionResult OnPostRestart()
    {
        RunCommand("docker compose restart");
        return RedirectToPage();
    }
    private void RunCommand(string command)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit();
    }
    
    // INFO 
    /// Con este metodo, rellenaremos el campo eliminated_at de la instancia a borrar.
    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        bool deleted = await supabaseService.DeleteInstanceById(id);
    
        if (!deleted)
        {
            // Recarga la instancia antes de volver a la página
            Instance = await supabaseService.SelectInstanceById(id);
            return Page();
        }
        TempData["Message"] = "Instancia eliminada";
        return RedirectToPage("/Instances/WordPressInstances");
    }
}