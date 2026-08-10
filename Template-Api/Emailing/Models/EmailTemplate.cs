using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Template_Api.Emailing.Models
{
    [Table("EmailTemplate", Schema = "emailing")]
    public class EmailTemplate
    {
        [Key]
        public string TemplateCode { get; set; }
        public string Html { get; set; }
    }
}
