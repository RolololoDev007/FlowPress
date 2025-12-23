using FlowPress.Models;
using FlowPress.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlowPress.Pages.Authentication
{
    public class RegisterPage(SupabaseService supabaseService) : PageModel
    {
        [BindProperty]
        public RegisterModel  RegisterModel { get; set; } = new();
        
        public string Message { get; set; } = "";

        public bool IsRegistering { get; set; } = false;

        public void OnGet()
        {
            
        }
        
        // INFO
        /// Este metodo es el que se ejecutará cuando le demos clic al botón de registrarse
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();
            // Aqui comprueba que las contraseñas que ha introducido el usuario contiene mas de 6 caracteres,
            // Si la contraseñas no contiene 6 caracteres, entrara en este IF en el cual almacenara en la variable Message
            // un texto de error de que la contraseña debe tener mas de 6 caracteres
            if (RegisterModel.Password.Length < 6)
            {
                Message = "❌ Passwords must be at least 6 characters";
                return Page();
            }
            // Aqui comprueba que las contraseñas que ha introducido el usuario coinciden,
            // Si las contraseñas no coinciden, entrara en este IF en el cual almacenara en la variable Message
            // un texto de error de que las contraseñas no coinciden
            if (RegisterModel.Password != RegisterModel.ConfirmPassword)
            {
                Message = "❌ Passwords do not match";
                return Page();
            }

            IsRegistering = true;
            Message = "Registering...";
            
            // Una vez que ha comprobado que las contraseñas son correctas, intentara hacer el registro del usuario
            try
            {
                var session = await supabaseService.SignUpAsync(RegisterModel.Email, RegisterModel.Password);
                
                if (session != null && session.User != null)
                {
                    Message = "✅ Registration successful!";
                    await CrearUsername(session.User.Id);
                    return RedirectToPage("LoginPage");
                }
                else
                {
                    Message = "❌ Registration failed";
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("422"))
                {
                    Message = $"❌ Error: Email Address Registered";
                }
                else
                {
                    Message = $"❌ Error: {ex.Message}";
                }
            }

            IsRegistering = false;
            return Page();
        }
        
        // INFO
        /// Hace un insert en la tabla de UsersInfo para poder almacenar el nombre de usuario
        /// que el usuario introduzca, ya que con Supabase la tabla de autentificacion no almacena nombre de usuario
        /// unicamente almacena el email, la contraseña hardcoded y un UserID que crea en formato GUID
        private async Task CrearUsername(string userId)
        {
            var user = new UsersInfoModel()
            {
                id = userId,
                username = RegisterModel.Username
            };

            await supabaseService.InsertAsync(user);
        }
    }
}
