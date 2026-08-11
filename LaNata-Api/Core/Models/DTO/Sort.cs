using LaNata_Api.Core.Models.Constants;

namespace LaNata_Api.Core.Models.DTO
{
    // Base for listings that return the whole filtered set. PaginateRequest adds the page on top of
    // it for the listings that do page (users, audit trail).
    public class SortRequest
    {
        // No default on purpose: each service's OrderQuery owns its natural order, so an unset
        // ColumnSort keeps the listing sorted the way the legacy screens showed it
        public string ColumnSort { get; set; }
        public string SortDirection { get; set; } = SortDirectionCode.DESC;
    }
}
