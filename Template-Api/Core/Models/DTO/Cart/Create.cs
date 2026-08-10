using System.Collections.Generic;

namespace Template_Api.Core.Models.DTO.Cart
{
    public class CreateRequest
    {
        public List<CartProductRequest> Products { get; set; } = [];
    }

    public class CreateResponse
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public decimal Total { get; set; }
        public List<CartProductResponse> Products { get; set; } = [];
    }

    public class CartProductRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class CartProductResponse
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal SettedPrice { get; set; }
        public decimal Total { get; set; }
    }
}
