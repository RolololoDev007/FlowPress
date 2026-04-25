using System.Diagnostics;
using System.Net;
using FlowPress.Models;
using FlowPress.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlowPress.Pages.Instances;

[Authorize]
public class WordPressInstanceInfo(
    SupabaseService supabaseService,
    RemoteDockerService remoteDockerService,
    IHttpClientFactory httpClientFactory) : PageModel
{
    public InstancesModel? Instance { get; set; }
    [TempData] public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public MonitoringSnapshot? Monitoring { get; set; }

    public async Task<IActionResult> OnGet(Guid id)
    {
        Instance = await LoadOwnedInstanceAsync(id);

        if (Instance == null)
            return NotFound();

        Monitoring = await BuildMonitoringSnapshotAsync(Instance);
        return Page();
    }

    public async Task<IActionResult> OnPostStartAsync(Guid id)
    {
        Instance = await LoadOwnedInstanceAsync(id);
        if (Instance == null) return NotFound();

        try
        {
            await remoteDockerService.ExecuteDockerCommandAsync($"start {Instance.DockerInstanceNameDb}");
            await remoteDockerService.ExecuteDockerCommandAsync($"start {Instance.DockerInstanceNameWp}");
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

    public async Task<IActionResult> OnPostStopAsync(Guid id)
    {
        Instance = await LoadOwnedInstanceAsync(id);
        if (Instance == null) return NotFound();

        try
        {
            await remoteDockerService.ExecuteDockerCommandAsync($"stop {Instance.DockerInstanceNameDb}");
            await remoteDockerService.ExecuteDockerCommandAsync($"stop {Instance.DockerInstanceNameWp}");
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

    public async Task<IActionResult> OnPostRestartAsync(Guid id)
    {
        Instance = await LoadOwnedInstanceAsync(id);
        if (Instance == null) return NotFound();

        try
        {
            await supabaseService.InstanceStatusAsync(id, "restarted");
            await remoteDockerService.ExecuteDockerCommandAsync($"restart {Instance.DockerInstanceNameDb}");
            await remoteDockerService.ExecuteDockerCommandAsync($"restart {Instance.DockerInstanceNameWp}");
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
            await remoteDockerService.ExecuteDockerCommandAsync($"stop {Instance.DockerInstanceNameDb}");
            await remoteDockerService.ExecuteDockerCommandAsync($"stop {Instance.DockerInstanceNameWp}");

            var deleted = await supabaseService.DeleteInstanceByIdAsync(id);

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

    private async Task<MonitoringSnapshot> BuildMonitoringSnapshotAsync(InstancesModel instance)
    {
        var checkedAt = DateTimeOffset.Now;

        if (string.IsNullOrWhiteSpace(instance.SiteAddress))
        {
            return new MonitoringSnapshot(
                "No configurado",
                "La instancia no tiene un dominio configurado.",
                null,
                null,
                null,
                checkedAt,
                0,
                false);
        }

        if (instance.DockerStatus is "stopped" or "pending" or "error")
        {
            var summary = instance.DockerStatus switch
            {
                "stopped" => "La instancia está apagada.",
                "pending" => "La instancia sigue desplegándose.",
                "error" => "La instancia tiene un error de despliegue.",
                _ => "La instancia no está disponible."
            };

            return new MonitoringSnapshot(
                "Sin respuesta",
                summary,
                $"https://{instance.SiteAddress}",
                null,
                null,
                checkedAt,
                0,
                false);
        }

        var httpsUrl = $"https://{instance.SiteAddress}";
        var httpsProbe = await ProbeAsync(httpsUrl, checkedAt);
        if (httpsProbe.IsHealthy || httpsProbe.StatusCode is not null)
            return httpsProbe;

        var httpUrl = $"http://{instance.SiteAddress}";
        return await ProbeAsync(httpUrl, checkedAt);
    }

    private async Task<MonitoringSnapshot> ProbeAsync(string url, DateTimeOffset checkedAt)
    {
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(8);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var isHealthy = statusCode is >= 200 and < 400;
            var label = isHealthy ? "Operativa" : "Incidencia";
            var summary = isHealthy
                ? "La web responde correctamente."
                : $"La web responde con HTTP {statusCode}.";

            return new MonitoringSnapshot(
                label,
                summary,
                url,
                statusCode,
                stopwatch.ElapsedMilliseconds,
                checkedAt,
                CalculateHealthScore(response.StatusCode),
                isHealthy);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new MonitoringSnapshot(
                "Sin respuesta",
                $"No se pudo comprobar la web: {ex.Message}",
                url,
                null,
                stopwatch.ElapsedMilliseconds > 0 ? stopwatch.ElapsedMilliseconds : null,
                checkedAt,
                0,
                false);
        }
    }

    private static int CalculateHealthScore(HttpStatusCode statusCode)
    {
        var numericCode = (int)statusCode;

        if (numericCode is >= 200 and < 400)
            return 100;

        if (numericCode is >= 400 and < 500)
            return 50;

        if (numericCode is >= 500 and < 600)
            return 15;

        return 0;
    }

    public record MonitoringSnapshot(
        string StatusLabel,
        string Summary,
        string? CheckedUrl,
        int? StatusCode,
        long? ResponseTimeMs,
        DateTimeOffset CheckedAt,
        int HealthScore,
        bool IsHealthy);
}
