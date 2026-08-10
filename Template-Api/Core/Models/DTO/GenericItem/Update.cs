namespace Template_Api.Core.Models.DTO.GenericItem
{
    public class UpdateRequest
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
    }
}
