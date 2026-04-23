using FlowPress.Models;
using FlowPress.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace FlowPress.Pages.Instances;

[Authorize]
public class WordPressInstanceInfo(SupabaseService supabaseService, IConfiguration configuration) : PageModel
{
    public InstancesModel? Instance { get; set; }
    [TempData] public string? Message { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGet(Guid id)
    {
        Instance = await LoadOwnedInstanceAsync(id);

        if (Instance == null)
            return NotFound();

        return Page();
    }
    // INFO
    /// Cuando se le de click al botón de start se mandara
    /// la petición de arranque al servidor docker
    public async Task<IActionResult> OnPostStartAsync(Guid id)
    {
        Instance = await LoadOwnedInstanceAsync(id);
        if (Instance == null) return NotFound();

        try
        {
            await DockerCommandsAsync($"start {Instance.DockerInstanceNameDb}");
            await DockerCommandsAsync($"start {Instance.DockerInstanceNameWp}");
            await supabaseService.InstanceStatusAsync(id, "running");
            Message = "Instancia iniciada correctamente.";
            return RedirectToPage(new { id });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
    // INFO
    /// Cuando se le de click al botón de parar se mandara
    /// la petición de stop al servidor docker
    public async Task<IActionResult> OnPostStopAsync(Guid id)
    {
        Instance = await LoadOwnedInstanceAsync(id);
        if (Instance == null) return NotFound();

        try
        {
            await DockerCommandsAsync($"stop {Instance.DockerInstanceNameDb}");
            await DockerCommandsAsync($"stop {Instance.DockerInstanceNameWp}");
            await supabaseService.InstanceStatusAsync(id, "stopped");
            Message = "Instancia apagada correctamente.";
            return RedirectToPage(new { id });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
    // INFO
    /// Cuando se le de click al botón de reiniciar se mandara
    /// la petición de reiniciar al servidor docker
    public async Task<IActionResult> OnPostRestartAsync(Guid id)
    {
        Instance = await LoadOwnedInstanceAsync(id);
        if (Instance == null) return NotFound();

        try
        {
            await supabaseService.InstanceStatusAsync(id, "restarted");
            await DockerCommandsAsync($"restart {Instance.DockerInstanceNameDb}");
            await DockerCommandsAsync($"restart {Instance.DockerInstanceNameWp}");
            await supabaseService.InstanceStatusAsync(id, "running");
            Message = "Instancia reiniciada correctamente.";
            return RedirectToPage(new { id });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
    // INFO
    /// Este será el que se encargue de mandar las peticiones al servidor docker
    /// para ejecutar los comandos necesarios.
    private async Task DockerCommandsAsync(string dockerCommand)
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configuredIdentityFile = configuration["RemoteDocker:SshKeyPath"];
        var identityFile = string.IsNullOrWhiteSpace(configuredIdentityFile)
            ? null
            : configuredIdentityFile.Replace("~", homeDirectory);
        var sshHost = configuration["RemoteDocker:Host"] ?? "flowpressifp.duckdns.org";
        var sshUser = configuration["RemoteDocker:User"] ?? "dockeruser";
        var sshPort = configuration["RemoteDocker:Port"] ?? "22";
        var connectTimeout = configuration["RemoteDocker:ConnectTimeoutSeconds"] ?? "10";

        if (string.IsNullOrWhiteSpace(sshHost))
            throw new InvalidOperationException("No hay un host SSH configurado en RemoteDocker:Host");

        string? firstError = null;

        if (!string.IsNullOrWhiteSpace(identityFile))
        {
            if (!System.IO.File.Exists(identityFile))
                throw new FileNotFoundException($"No se ha encontrado la clave SSH en {identityFile}");

            firstError = await ExecuteSshCommandAsync(sshUser, sshHost, sshPort, connectTimeout, dockerCommand, identityFile);
            if (firstError == null)
                return;
        }

        var fallbackError = await ExecuteSshCommandAsync(sshUser, sshHost, sshPort, connectTimeout, dockerCommand, null);
        if (fallbackError == null)
            return;

        throw new Exception($"Error ejecutando Docker remoto en {sshHost}:{sshPort}: {fallbackError}".Trim());
    }

    private static async Task<string?> ExecuteSshCommandAsync(
        string sshUser,
        string sshHost,
        string sshPort,
        string connectTimeout,
        string dockerCommand,
        string? identityFile)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ssh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!string.IsNullOrWhiteSpace(identityFile))
        {
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(identityFile);
        }

        process.StartInfo.ArgumentList.Add("-p");
        process.StartInfo.ArgumentList.Add(sshPort);
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add("StrictHostKeyChecking=no");
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add($"ConnectTimeout={connectTimeout}");
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add("BatchMode=yes");
        process.StartInfo.ArgumentList.Add($"{sshUser}@{sshHost}");
        process.StartInfo.ArgumentList.Add($"docker {dockerCommand}");

        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return process.ExitCode == 0 ? null : stderr;
    }
    // INFO 
    /// Con este metodo, rellenaremos el campo eliminated_at de la instancia a borrar.
    /// Además, paramos los contenedores docker asociados a la instancia.
    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        Instance = await LoadOwnedInstanceAsync(id);

        if (Instance == null)
        {
            TempData["Message"] = "La instancia no existe";
            return RedirectToPage("/Instances/WordPressInstances");
        }

        try
        {
            await DockerCommandsAsync($"stop {Instance.DockerInstanceNameDb}");
            await DockerCommandsAsync($"stop {Instance.DockerInstanceNameWp}");

            bool deleted = await supabaseService.DeleteInstanceByIdAsync(id);

            if (!deleted)
            {
                ErrorMessage = "Error al eliminar la instancia";
                return Page();
            }

            Message = "Instancia marcada para eliminación";
            return RedirectToPage("/Instances/WordPressInstances");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    private Task<InstancesModel?> LoadOwnedInstanceAsync(Guid id)
    {
        return supabaseService.SelectOwnedInstanceByIdAsync(id);
    }
}
