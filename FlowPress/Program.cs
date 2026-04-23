using FlowPress.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// INFO Soporte para Razor Pages
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

// Servicios
builder.Services.AddScoped<SupabaseService>();

// INFO Configuración de autenticación con cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";  // INFO Página a la que se redirige si no está autenticado 
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(1); // INFO Duración de la cookie
    });

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// INFO Si no encuentra la ruta de la pagina salta la pagina de error
app.UseStatusCodePagesWithReExecute("/error");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// INFO Autenticación y autorización
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
