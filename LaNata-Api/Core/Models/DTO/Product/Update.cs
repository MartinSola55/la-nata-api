namespace LaNata_Api.Core.Models.DTO.Product
{
    public class UpdateRequest
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public long Stock { get; set; }
        public double Price { get; set; }
        public double BreakPrice { get; set; }
    }
}
