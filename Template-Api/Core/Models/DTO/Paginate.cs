using Template_Api.Core.Models.Constants;

namespace Template_Api.Core.Models.DTO
{
    public class PaginateRequest
    {
        public int Page { get; set; } = 1;
        public string ColumnSort { get; set; } = "createdAt";
        public string SortDirection { get; set; } = SortDirectionCode.DESC;
    }
}
