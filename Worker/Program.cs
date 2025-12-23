using Npgsql;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

Console.WriteLine("🚀 FlowPress Worker starting...");

// ===============================
// CONFIGURACIÓN
// ===============================

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");

var sshUser = configuration["Docker:SshUser"];
var sshHost = configuration["Docker:SshHost"];


// Carpeta base donde se despliegan las instancias
var instancesRoot = configuration["Docker:InstancesPath"]; // cambia en Windows si hace falta

Directory.CreateDirectory(instancesRoot);

// ===============================
// LOOP PRINCIPAL
// ===============================

while (true)
{
    try
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // 1️⃣ Buscar una instancia pendiente
        var selectCmd = new NpgsqlCommand(@"
            select id, dockerscript
            from ""Instances""
            where status = 'pending'
            order by created_at
            limit 1
            for update skip locked
        ", conn);

        await using var reader = await selectCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            await Task.Delay(5000);
            continue;
        }

        var instanceId = reader.GetGuid(0);
        var dockerScript = reader.GetString(1);

        reader.Close();

        Console.WriteLine($"📦 Deploying instance {instanceId}");

        // 2️⃣ Marcar como deploying
        await UpdateStatus(conn, instanceId, "deploying");

        // 3️⃣ Crear carpeta de la instancia
        var instanceDir = Path.Combine(instancesRoot, instanceId.ToString());
        Directory.CreateDirectory(instanceDir);

        // 4️⃣ Escribir docker-compose.yml
        var composePath = Path.Combine(instanceDir, "docker-compose.yml");
        await File.WriteAllTextAsync(composePath, dockerScript);

        // 5️⃣ Ejecutar docker compose
        var remoteDir = $"{instancesRoot}/{instanceId}";

        // Crear carpeta remota
        RunProcess(
            "ssh",
            $"{sshUser}@{sshHost} \"mkdir -p {remoteDir}\""
        );

        // Copiar docker-compose.yml
        CopyComposeToRemote(
            composePath,
            sshUser,
            sshHost,
            remoteDir
        );

        // Ejecutar docker compose remoto
        RunRemoteDocker(
            sshUser,
            sshHost,
            remoteDir
        );
        
        // 6️⃣ Limpiar contraseña del script
        var cleanedScript = Regex.Replace(
            dockerScript,
            @"MYSQL_PASSWORD:.*|WORDPRESS_DB_PASSWORD:.*",
            "# PASSWORD REMOVED",
            RegexOptions.IgnoreCase
        );

        // 7️⃣ Actualizar DB (running + script limpio)
        var updateCmd = new NpgsqlCommand(@"
            update ""Instances""
            set status = 'running',
                dockerscript = @script,
                deployed_at = now()
            where id = @id
        ", conn);

        updateCmd.Parameters.AddWithValue("script", cleanedScript);
        updateCmd.Parameters.AddWithValue("id", instanceId);

        await updateCmd.ExecuteNonQueryAsync();

        Console.WriteLine($"✅ Instance {instanceId} deployed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERROR: {ex.Message}");
        await Task.Delay(5000);
    }
}

// ===============================
// MÉTODOS AUXILIARES
// ===============================

static async Task UpdateStatus(NpgsqlConnection conn, Guid id, string status)
{
    var cmd = new NpgsqlCommand(
        @"update ""Instances"" set status = @status where id = @id",
        conn
    );
    cmd.Parameters.AddWithValue("status", status);
    cmd.Parameters.AddWithValue("id", id);
    await cmd.ExecuteNonQueryAsync();
}

// static void RunDockerCompose(string workingDir)
// {
//     var psi = new ProcessStartInfo
//     {
//         FileName = "ssh",
//         Arguments = $"{Environment.GetEnvironmentVariable("SSH_USER")}@{Environment.GetEnvironmentVariable("SSH_HOST")} \"cd {workingDir} && docker compose up -d\"",
//         RedirectStandardOutput = true,
//         RedirectStandardError = true,
//         UseShellExecute = false,
//         CreateNoWindow = true
//     };
//
//     using var process = new Process { StartInfo = psi };
//     process.Start();
//     process.WaitForExit();
//
//     var output = process.StandardOutput.ReadToEnd();
//     var error = process.StandardError.ReadToEnd();
//
//     if (process.ExitCode != 0)
//     {
//         throw new Exception($"Docker Compose failed: {error}");
//     }
// }

static void CopyComposeToRemote(
    string localComposePath,
    string sshUser,
    string sshHost,
    string remoteDir
)
{
    RunProcess(
        "scp",
        $"{localComposePath} {sshUser}@{sshHost}:{remoteDir}/docker-compose.yml"
    );
}

static void RunRemoteDocker(
    string sshUser,
    string sshHost,
    string remoteDir
)
{
    RunProcess(
        "ssh",
        $"{sshUser}@{sshHost} \"cd {remoteDir} && docker compose up -d\""
    );
}

static void RunProcess(string file, string args)
{
    var p = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }
    };

    p.Start();
    p.WaitForExit();

    if (p.ExitCode != 0)
        throw new Exception($"{file} failed");
}

