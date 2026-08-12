namespace LaNata_Api.Core.Models.DTO.Product
{
    public class UpdateRequest
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public int Stock { get; set; }
        public decimal Price { get; set; }
        public decimal? BreakPrice { get; set; }
    }
}
