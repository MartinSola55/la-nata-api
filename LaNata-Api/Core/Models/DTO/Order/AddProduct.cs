namespace LaNata_Api.Core.Models.DTO.Order
{
    public class AddProductRequest
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        // When set, the quantity joins the existing line at that price instead of opening a new one
        // at the current catalogue price. This is how the legacy screen kept an old price alive.
        public decimal? UnitPrice { get; set; }
    }
}
