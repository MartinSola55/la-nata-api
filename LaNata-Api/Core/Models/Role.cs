using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LaNata_Api.Core.Models
{
    // Maps the legacy [Rol] table. Its rows are "Administrador" and "Empleado" — see Roles.
    [Table("Rol")]
    public class Role
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("description")]
        public string Name { get; set; }
    }
}
