namespace LaNata_Api.Core.Models.DTO.Order
{
    public class RemoveProductRequest
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public decimal UnitPrice { get; set; }

        // Removing the whole reserved quantity deletes the line; anything less just subtracts
        public int Quantity { get; set; }
    }
}
