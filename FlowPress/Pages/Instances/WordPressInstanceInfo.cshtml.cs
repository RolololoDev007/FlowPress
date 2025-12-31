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
        Instance = await supabaseService.SelectInstanceByIdAsync(id);

        if (Instance == null)
            return NotFound();

        return Page();
    }
    // INFO
    /// Cuando se le de click al botón de start se mandara
    /// la petición de arranque al servidor docker
    public async Task<IActionResult> OnPostStartAsync(Guid id)
    {
        Instance = await supabaseService.SelectInstanceByIdAsync(id);
        if (Instance == null) return NotFound();
        
        await DockerCommandsAsync($"start {Instance.DockerInstanceNameDb}");
        await DockerCommandsAsync($"start {Instance.DockerInstanceNameWp}");
        await supabaseService.InstanceStatusAsync(id, "running");
        return RedirectToPage(new { id });
    }
    // INFO
    /// Cuando se le de click al botón de parar se mandara
    /// la petición de stop al servidor docker
    public async Task<IActionResult> OnPostStopAsync(Guid id)
    {
        Instance = await supabaseService.SelectInstanceByIdAsync(id);
        if (Instance == null) return NotFound();
        
        await DockerCommandsAsync($"stop {Instance.DockerInstanceNameDb}");
        await DockerCommandsAsync($"stop {Instance.DockerInstanceNameWp}");
        await supabaseService.InstanceStatusAsync(id, "stopped");
        
        return RedirectToPage(new { id });
    }
    // INFO
    /// Cuando se le de click al botón de reiniciar se mandara
    /// la petición de reiniciar al servidor docker
    public async Task<IActionResult> OnPostRestartAsync(Guid id)
    {
        Instance = await supabaseService.SelectInstanceByIdAsync(id);
        if (Instance == null) return NotFound();
        
        await supabaseService.InstanceStatusAsync(id, "restarted");
        await DockerCommandsAsync($"restart {Instance.DockerInstanceNameDb}");
        await DockerCommandsAsync($"restart {Instance.DockerInstanceNameWp}");
        await supabaseService.InstanceStatusAsync(id, "running");
        
        return RedirectToPage(new { id });
    }
    // INFO
    /// Este será el que se encargue de mandar las peticiones al servidor docker
    /// para ejecutar los comandos necesarios.
    private async Task DockerCommandsAsync(string dockerCommand)
    {
        /// ROLO Maquina Azure
        // var process = new Process
        // {
        //     StartInfo = new ProcessStartInfo
        //     {
        //         FileName = "ssh",
        //         Arguments = $"dockeruser@20.90.161.35 \"docker {dockerCommand}\"",
        //         RedirectStandardOutput = true,
        //         RedirectStandardError = true,
        //         UseShellExecute = false,
        //         CreateNoWindow = true
        //     }
        // };

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = $"-i ~/.ssh/id_ed25519 -o StrictHostKeyChecking=no dockeruser@flowpressifp.duckdns.org \"docker {dockerCommand}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"Remote Docker error:\n{stderr}");
    }
    // INFO 
    /// Con este metodo, rellenaremos el campo eliminated_at de la instancia a borrar.
    /// Además, paramos los contenedores docker asociados a la instancia.
    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        Instance = await supabaseService.SelectInstanceByIdAsync(id);

        if (Instance == null)
        {
            TempData["Message"] = "La instancia no existe";
            return RedirectToPage("/Instances/WordPressInstances");
        }

        await DockerCommandsAsync($"stop {Instance.DockerInstanceNameDb}");
        await DockerCommandsAsync($"stop {Instance.DockerInstanceNameWp}");

        bool deleted = await supabaseService.DeleteInstanceByIdAsync(id);

        if (!deleted)
        {
            TempData["Message"] = "Error al eliminar la instancia";
            return Page();
        }

        await supabaseService.InstanceStatusAsync(id, "deleted");

        TempData["Message"] = "Instancia eliminada";
        return RedirectToPage("/Instances/WordPressInstances");
    }
}