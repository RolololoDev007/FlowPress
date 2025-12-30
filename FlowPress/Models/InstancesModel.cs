using System.ComponentModel.DataAnnotations;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace FlowPress.Models;

// INFO
/// Este modelo se utiliza para definir la estructura de la BBDD
/// de la tabla Instancias
[Table(("Instances"))]
public class InstancesModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Column("iduser")]
    public string IdUser { get; set; } = string.Empty;
    
    [Column("sitename")]
    [Required(ErrorMessage = "Site name required")]
    public string SiteName { get; set; } = string.Empty;
    
    [Column("siteaddress")]
    [Required(ErrorMessage = "Site address required")]
    public string SiteAddress { get; set; } = string.Empty;
    
    [Column("dockerinstancenamewp")]
    public string DockerInstanceNameWp { get; set; } = string.Empty;
    
    [Column("dockerinstancenamedb")]
    public string DockerInstanceNameDb { get; set; } = string.Empty;
    
    [Column("status")]
    public string DockerStatus { get; set; } = string.Empty;    
    
    [Column("eliminated_at")]
    public DateTime? EliminatedAt { get; set; }
}