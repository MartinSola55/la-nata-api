namespace Template_Api.Core.Models.Internal
{
    public class Token
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string Role { get; set; }

        public string FullName => $"{LastName}, {Name}";
    }
}
