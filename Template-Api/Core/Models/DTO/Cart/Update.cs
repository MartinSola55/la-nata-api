using System.Collections.Generic;

namespace Template_Api.Core.Models.DTO.Cart
{
    public class UpdateRequest
    {
        public int Id { get; set; }
        public List<CartProductRequest> Products { get; set; } = [];
    }
}
