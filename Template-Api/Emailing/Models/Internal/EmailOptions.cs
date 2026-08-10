namespace Template_Api.Emailing.Models.Internal
{
    public class EmailOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public bool UseSsl { get; set; } = true;
        public string FromName { get; set; }
        public string FromEmail { get; set; }
        public string EmailRecipient { get; set; }
    }
}
