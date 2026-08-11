namespace LaNata_Api.Core.Models.DTO
{
    public class PaginateRequest : SortRequest
    {
        public int Page { get; set; } = 1;
    }
}
