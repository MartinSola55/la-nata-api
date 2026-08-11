namespace LaNata_Api.Core.Models.Internal
{
    // The legacy [User] table has no name columns, so the email is the only human-readable identity
    // the token can carry
    public class Token
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
    }
}
