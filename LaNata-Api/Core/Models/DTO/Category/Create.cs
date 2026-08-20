namespace LaNata_Api.Core.Models.DTO.Category
{
    public class CreateRequest
    {
        public string Description { get; set; }
    }

    public class CreateResponse
    {
        public int Id { get; set; }
        public string Description { get; set; }
    }
}
