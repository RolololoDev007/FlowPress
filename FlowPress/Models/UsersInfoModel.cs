using System.ComponentModel.DataAnnotations;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace FlowPress.Models;

// INFO
/// Este modelo se utiliza para definir la estructura de la BBDD.
/// También se usa para que se pueda hacer la validación de que el username nunca se pueda quedar vacio
[Table(("UsersInfo"))]
public class UsersInfoModel : BaseModel
{
    [PrimaryKey]
    [Column("id")]
    public string id { get; set; } = string.Empty;

    [Column("username")]
    [Required(ErrorMessage = "Username required")]
    public string username { get; set; } = string.Empty;
}