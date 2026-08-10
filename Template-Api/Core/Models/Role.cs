using System.ComponentModel.DataAnnotations;

namespace Template_Api.Core.Models
{
    public class Role
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
