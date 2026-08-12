using LaNata_Api.Core.Models.Constants;

namespace LaNata_Api.Core.Models.DTO
{
    public class SortRequest
    {
        public string ColumnSort { get; set; }
        public string SortDirection { get; set; } = SortDirectionCode.DESC;
    }
}
