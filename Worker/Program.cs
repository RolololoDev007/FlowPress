using Npgsql;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

Console.WriteLine("🚀 FlowPress Worker starting...");

// ROLO Explicar funcionamiento del worker
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", false)
    .Build();

var connString = config.GetConnectionString("SupabaseDb")
    ?? throw new Exception("SupabaseDb connection string missing");

var instancesRoot = config["FlowPress:InstancesPath"]!;
var nginxAvail = config["FlowPress:NginxSitesAvailable"]!;
var nginxEnabled = config["FlowPress:NginxSitesEnabled"]!;
var basePort = int.Parse(config["FlowPress:BasePort"]!);

Directory.CreateDirectory(instancesRoot);

while (true)
{
    try
    {
        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        using var cmd = new NpgsqlCommand(@"
            select id, siteaddress, dockerscript, status
            from ""Instances""
            where status in ('pending','deleting')
            order by created_at
            limit 1
            for update skip locked
        ", conn);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            Thread.Sleep(5000);
            continue;
        }

        var id = reader.GetGuid(0);
        var domain = reader.GetString(1);
        var script = reader.GetString(2);
        var status = reader.GetString(3);
        reader.Close();

        if (status == "pending")
            Deploy(conn, id, domain, script);
        else
            Remove(conn, id, domain);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
        Thread.Sleep(5000);
    }
}

#region Metodos

// ROLO Explicar funcionamiento de los metodos
void Deploy(NpgsqlConnection conn, Guid id, string domain, string script)
{
    Console.WriteLine($"Deploying {domain}");

    var port = FindFreePort(basePort);
    var dir = Path.Combine(instancesRoot, id.ToString());
    Directory.CreateDirectory(dir);

    script = script.Replace("{{PORT}}", port.ToString());
    File.WriteAllText(Path.Combine(dir, "docker-compose.yml"), script);

    Run("docker compose up -d", dir);
    CreateNginx(domain, port);

    Run("sudo nginx -t && sudo systemctl reload nginx");
    UpdateStatus(conn, id, "running");

    Console.WriteLine($"{domain} running on port {port}");
}

void Remove(NpgsqlConnection conn, Guid id, string domain)
{
    Console.WriteLine($"🗑 Removing {domain}");

    var dir = Path.Combine(instancesRoot, id.ToString());
    if (Directory.Exists(dir))
        Run("docker compose down -v", dir);

    DeleteNginx(domain);
    Run("nginx -t && systemctl reload nginx");

    UpdateStatus(conn, id, "deleted");
}

void CreateNginx(string domain, int port)
{
    var conf = $@"
server {{
    listen 80;
    server_name {domain};

    location / {{
        proxy_pass http://127.0.0.1:{port};
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $remote_addr;
    }}
}}";

    var path = Path.Combine(nginxAvail, $"{domain}.conf");
    File.WriteAllText(path, conf);

    var link = Path.Combine(nginxEnabled, $"{domain}.conf");
    if (!File.Exists(link))
        Run($"ln -s {path} {link}");
}

void DeleteNginx(string domain)
{
    File.Delete(Path.Combine(nginxEnabled, $"{domain}.conf"));
    File.Delete(Path.Combine(nginxAvail, $"{domain}.conf"));
}

int FindFreePort(int start)
{
    var port = start;
    while (true)
    {
        try
        {
            var l = new TcpListener(IPAddress.Loopback, port);
            l.Start();
            l.Stop();
            return port;
        }
        catch { port++; }
    }
}

void UpdateStatus(NpgsqlConnection conn, Guid id, string status)
{
    using var cmd = new NpgsqlCommand(
        @"update ""Instances"" set status=@s where id=@i", conn);
    cmd.Parameters.AddWithValue("s", status);
    cmd.Parameters.AddWithValue("i", id);
    cmd.ExecuteNonQuery();
}

#endregion

void Run(string cmd, string? cwd = null)
{
    var p = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"{cmd}\"",
            WorkingDirectory = cwd ?? "/",
            RedirectStandardError = true
        }
    };

    p.Start();
    p.WaitForExit();

    if (p.ExitCode != 0)
        throw new Exception(p.StandardError.ReadToEnd());
}
