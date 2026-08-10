using System.Collections.Generic;

namespace Template_Api.Emailing.Models.Internal
{
    public record EmailAttachment(string FileName, byte[] Content, string ContentType);

    public class EmailMessage
    {
        public List<string> To { get; set; } = [];
        public string Subject { get; set; }
        public string TemplateCode { get; set; }
        public Dictionary<string, string> Tokens { get; set; } = [];
        public List<EmailAttachment> Attachments { get; set; } = [];
    }
}
