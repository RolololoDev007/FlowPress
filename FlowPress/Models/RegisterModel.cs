using System.ComponentModel.DataAnnotations;

namespace FlowPress.Models;

// INFO 
/// Este modelo se utiliza para definir la estructura de la BBDD.
/// También se usa para que se pueda hacer la validacion de que el usuario, no pueda dejar según que campos vacios
public class RegisterModel
{
    [Required(ErrorMessage = "Username required")]
    public string Username { get; set; } = string.Empty;
    [Required(ErrorMessage = "Email required")]
    [EmailAddress(ErrorMessage = "The email address is invalid.")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Password required")]
    public string Password { get; set; } = string.Empty;
    [Required(ErrorMessage = "Password required")]
    public string ConfirmPassword { get; set; } = string.Empty;
}