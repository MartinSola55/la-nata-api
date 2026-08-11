namespace LaNata_Api.Core.Models.Internal
{
    // One printed line of an order detail. The shipment is a row too, which is why this is a flat
    // model instead of the ProductOrder entity.
    public class OrderReportRow
    {
        public string Description { get; set; }
        public int Quantity { get; set; }
        public double UnitPrice { get; set; }
        public double? BreakPrice { get; set; }
        public double Subtotal { get; set; }
    }
}
