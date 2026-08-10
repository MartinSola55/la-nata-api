using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Template_Api.Core.Models
{
    [Table("_Migrations")]
    public class Migration
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(260)]
        public string FileName { get; set; }

        public DateTime ExecutedAt { get; set; }
    }
}
