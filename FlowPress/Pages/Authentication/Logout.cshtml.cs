using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FlowPress.Services;

namespace FlowPress.Pages.Authentication;

public class LogoutModel : PageModel
{
    private readonly SupabaseService _supabaseService;

    public LogoutModel(SupabaseService supabaseService)
    {
        _supabaseService = supabaseService;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Cierra sesión en Supabase
        await _supabaseService.SignOutAsync(HttpContext);

        // Cierra la cookie de autenticación
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Redirige al login
        return RedirectToPage("LoginPage");
    }
}