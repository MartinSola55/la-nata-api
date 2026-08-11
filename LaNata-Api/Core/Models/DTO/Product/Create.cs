namespace LaNata_Api.Core.Models.DTO.Product
{
    public class CreateRequest
    {
        public string Description { get; set; }
        public long Stock { get; set; }
        public double Price { get; set; }
        public double BreakPrice { get; set; }
    }

    public class CreateResponse
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public long Stock { get; set; }
        public double Price { get; set; }
        public double BreakPrice { get; set; }
    }
}
