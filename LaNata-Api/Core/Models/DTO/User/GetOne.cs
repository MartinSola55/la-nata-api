using System;

namespace LaNata_Api.Core.Models.DTO.User
{
    public class GetOneRequest
    {
        public int Id { get; set; }
    }

    public class GetOneResponse
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public string Role { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
