using System;

namespace Template_Api.Core.Models.DTO.Auth
{
    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public string Token { get; set; }
        public DateTime SessionExpiration { get; set; }
        public Item User { get; set; } = new();

        public class Item
        {
            public int Id { get; set; }
            public string Role { get; set; }
            public string Name { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
        }
    }
}
