namespace Template_Api.Core.Models.DTO.Product
{
    public class UpdateRequest
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string PictureUrl { get; set; }
        public string CategoryId { get; set; }
        public bool IsActive { get; set; }
    }
}
