using System.ComponentModel.DataAnnotations;

namespace FlowPress.Models;

public class CreateInstanceInputModel
{
    [Required(ErrorMessage = "Nombre del sitio obligatorio")]
    [StringLength(120, ErrorMessage = "El nombre del sitio no puede superar los 120 caracteres.")]
    public string SiteName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Dirección del sitio obligatoria")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Usa solo letras minúsculas, números y guiones.")]
    [StringLength(63, ErrorMessage = "La dirección no puede superar los 63 caracteres.")]
    public string SiteAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Usuario administrador obligatorio")]
    [RegularExpression("^[A-Za-z0-9 _.@-]+$", ErrorMessage = "El usuario solo puede contener letras, números, espacios, guiones, guiones bajos, puntos y @.")]
    [StringLength(60, ErrorMessage = "El usuario no puede superar los 60 caracteres.")]
    public string AdminUser { get; set; } = string.Empty;

    [Required(ErrorMessage = "Correo electrónico obligatorio")]
    [EmailAddress(ErrorMessage = "Introduce un correo electrónico válido.")]
    [StringLength(255, ErrorMessage = "El correo electrónico no puede superar los 255 caracteres.")]
    public string AdminEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contraseña obligatoria")]
    [MinLength(12, ErrorMessage = "La contraseña debe tener al menos 12 caracteres.")]
    [StringLength(255, ErrorMessage = "La contraseña no puede superar los 255 caracteres.")]
    public string AdminPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debes confirmar la contraseña.")]
    [Compare(nameof(AdminPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmAdminPassword { get; set; } = string.Empty;
}
