namespace LaNata_Api.Core.Models.DTO.User
{
    public class CreateRequest
    {
        public int RoleId { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class CreateResponse
    {
        public int Id { get; set; }
        public string Role { get; set; }
        public string Email { get; set; }
    }
}
