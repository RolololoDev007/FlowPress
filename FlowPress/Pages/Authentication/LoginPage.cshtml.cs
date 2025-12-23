using FlowPress.Models;
using FlowPress.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlowPress.Pages.Authentication;

public class LoginPage (SupabaseService supabaseService) : PageModel
{
    // INFO
    /// Inicializamos la variable LoginModel, la cual vincularemos con el LoginModel, para poder hacer uso de las variables definidas en ese Modelo
    [BindProperty]
    public LoginModel LoginModel { get; set; } = new();

    public IActionResult OnGet()
    {
        return Page();
    }
    
    // INFO
    /// Este metodo es el que se ejecutará cuando le demos clic al botón de Login
    public async Task<IActionResult> OnPostLoginAsync()
    {
        // Intenta proceder con el Login
        try
        {
            // Llamaremos al metodo que hemos creado en el SupabaseService.cs
            bool success = await supabaseService.SignInAsync(LoginModel.Email, LoginModel.Password, HttpContext);
            
            if (success)
            {
                // Si la validación es correcta, nos redirigira a la página principal
                return RedirectToPage("/Index");
            }
            else
            {
                // Si la validación es incorrecta, almacenará en la variable Message que tenemos en la vista,
                // un mensaje que indica que el Email o Contraseña son incorrectos
                ViewData["Message"] = "❌ Email o contraseña incorrectos";
                return Page();
            }
        }
        catch (Exception ex)
        {
            // Si no ha podido proceder con el login, almacenará en la variable Message que tenemos en la vista,
            // un mensaje que mostrara el codigo de error que ha dado el Login
            ViewData["Message"] = $"❌ Error: {ex.Message}";
            return Page();
        }
    }
}