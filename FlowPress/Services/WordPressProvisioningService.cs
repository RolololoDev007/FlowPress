using System.Security.Cryptography;
using System.Text;
using FlowPress.Models;

namespace FlowPress.Services;

public class WordPressProvisioningService(
    RemoteDockerService remoteDockerService,
    SupabaseService supabaseService)
{
    public async Task ProvisionAsync(Guid instanceId, string adminPassword, CancellationToken cancellationToken = default)
    {
        var instance = await supabaseService.SelectInstanceByIdAsync(instanceId);
        if (instance == null)
            throw new InvalidOperationException("No se ha encontrado la instancia recién creada.");

        if (string.IsNullOrWhiteSpace(instance.DockerScript))
            throw new InvalidOperationException("La instancia no tiene un dockerscript generado.");

        await supabaseService.UpdateProvisioningStatusAsync(instanceId, "provisioning");

        var slug = BuildSlug(instance.SiteAddress);
        var remoteBasePath = $"/opt/flowpress/instances/{slug}";
        var remoteComposePath = $"{remoteBasePath}/compose.yaml";
        var port = await ReserveRemotePortAsync(cancellationToken);

        var renderedCompose = instance.DockerScript
            .Replace("{{PORT}}", port.ToString())
            .Replace("{{WP_ADMIN_USER}}", EscapeForYamlSingleQuotedScalar(instance.WpAdminUser))
            .Replace("{{WP_ADMIN_EMAIL}}", EscapeForYamlSingleQuotedScalar(instance.WpAdminEmail))
            .Replace("{{WP_ADMIN_PASSWORD}}", EscapeForYamlSingleQuotedScalar(adminPassword));

        var composeBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(renderedCompose));

        var remoteScript = $"""
set -e
mkdir -p {QuoteForShell(remoteBasePath)}
printf '%s' {QuoteForShell(composeBase64)} | base64 -d > {QuoteForShell(remoteComposePath)}
docker compose -f {QuoteForShell(remoteComposePath)} up -d --remove-orphans
docker ps --filter name={QuoteForShell(instance.DockerInstanceNameDb)} --filter name={QuoteForShell(instance.DockerInstanceNameWp)}
""";

        try
        {
            await remoteDockerService.ExecuteRemoteShellCommandAsync(remoteScript, cancellationToken);
            await supabaseService.UpdateProvisioningStatusAsync(instanceId, "ready");
            await supabaseService.InstanceStatusAsync(instanceId, "running");
        }
        catch
        {
            await supabaseService.UpdateProvisioningStatusAsync(instanceId, "error");
            await supabaseService.InstanceStatusAsync(instanceId, "error");
            throw;
        }
    }

    private async Task<int> ReserveRemotePortAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = RandomNumberGenerator.GetInt32(20000, 40000);
            var validationCommand = $"! ss -ltn '( sport = :{candidate} )' | grep -q :{candidate}";

            try
            {
                await remoteDockerService.ExecuteRemoteShellCommandAsync(validationCommand, cancellationToken);
                return candidate;
            }
            catch
            {
                // Probamos otro puerto si este ya está ocupado.
            }
        }

        throw new InvalidOperationException("No se pudo reservar un puerto libre para la nueva instancia.");
    }

    private static string BuildSlug(string siteAddress)
    {
        var rawSlug = siteAddress.ToLowerInvariant();
        var characters = rawSlug
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();

        return new string(characters).Trim('_');
    }

    private static string EscapeForYamlSingleQuotedScalar(string value)
    {
        return value.Replace("'", "''");
    }

    private static string QuoteForShell(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'")}'";
    }
}
