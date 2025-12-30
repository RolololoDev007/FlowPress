using System.ComponentModel.DataAnnotations;

namespace FlowPress.Models;

// INFO 
/// Este modelo se utiliza para definir la estructura de la BBDD.
/// También se usa para que se pueda hacer la validacion de que el usuario, no pueda dejar según que campos vacios
public class RegisterModel
{
    [Required(ErrorMessage = "Nombre de usuario obligatorio")]
    public string Username { get; set; } = string.Empty;
    [Required(ErrorMessage = "Correo electrónico obligatorio")]
    [EmailAddress(ErrorMessage = "Correo electrónico no válido")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Contraseña obligatoria")]
    public string Password { get; set; } = string.Empty;
    [Required(ErrorMessage = "Contraseña obligatoria")]
    public string ConfirmPassword { get; set; } = string.Empty;
}