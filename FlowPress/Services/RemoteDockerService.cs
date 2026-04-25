using System.Diagnostics;

namespace FlowPress.Services;

public class RemoteDockerService(IConfiguration configuration)
{
    public Task ExecuteDockerCommandAsync(string dockerCommand, CancellationToken cancellationToken = default)
    {
        return ExecuteRemoteShellCommandAsync($"docker {dockerCommand}", cancellationToken);
    }

    public async Task ExecuteRemoteShellCommandAsync(string shellCommand, CancellationToken cancellationToken = default)
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
            throw new InvalidOperationException("No hay un host SSH configurado en RemoteDocker:Host.");

        string? firstError = null;

        if (!string.IsNullOrWhiteSpace(identityFile))
        {
            if (!File.Exists(identityFile))
                throw new FileNotFoundException($"No se ha encontrado la clave SSH en {identityFile}");

            firstError = await ExecuteSshCommandAsync(
                sshUser,
                sshHost,
                sshPort,
                connectTimeout,
                shellCommand,
                identityFile,
                cancellationToken);

            if (firstError == null)
                return;
        }

        var fallbackError = await ExecuteSshCommandAsync(
            sshUser,
            sshHost,
            sshPort,
            connectTimeout,
            shellCommand,
            null,
            cancellationToken);

        if (fallbackError == null)
            return;

        throw new InvalidOperationException($"Error ejecutando comandos remotos en {sshHost}:{sshPort}: {fallbackError}".Trim());
    }

    private static async Task<string?> ExecuteSshCommandAsync(
        string sshUser,
        string sshHost,
        string sshPort,
        string connectTimeout,
        string shellCommand,
        string? identityFile,
        CancellationToken cancellationToken)
    {
        using var process = new Process
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
        process.StartInfo.ArgumentList.Add($"sh -lc {QuoteForShell(shellCommand)}");

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode == 0)
            return null;

        return string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
    }

    private static string QuoteForShell(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'")}'";
    }
}
