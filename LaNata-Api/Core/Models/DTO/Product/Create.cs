namespace LaNata_Api.Core.Models.DTO.Product
{
    public class CreateRequest
    {
        public string Description { get; set; }
        public int Stock { get; set; }
        public decimal Price { get; set; }
        public decimal? BreakPrice { get; set; }
    }

    public class CreateResponse
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public int Stock { get; set; }
        public decimal Price { get; set; }
        public decimal? BreakPrice { get; set; }
    }
}
