using LaNata_Api.Core.Models.Constants;
using LaNata_Api.Core.Models.DTO;

namespace LaNata_Api.Core.Services
{
    public static class ServiceRequestValidator
    {
        public static string ValidateSort(SortRequest rq)
        {
            if (rq == null)
                return null;

            if (!string.IsNullOrEmpty(rq.ColumnSort) && rq.ColumnSort.Length > ValidationLimits.SortColumnMaxLength)
                return Messages.Error.FieldTooLong("orden", ValidationLimits.SortColumnMaxLength);

            if (!string.IsNullOrEmpty(rq.SortDirection) && rq.SortDirection.Length > 4)
                return Messages.Error.InvalidField("dirección de orden");

            return null;
        }

        public static string ValidatePagination(PaginateRequest rq)
        {
            var sortError = ValidateSort(rq);
            if (sortError != null)
                return sortError;

            if (rq != null && (rq.Page <= 0 || rq.Page > Pagination.MaxPage))
                return Messages.Error.InvalidField("página");

            return null;
        }

        public static string ValidateSearch(string search)
        {
            return !string.IsNullOrEmpty(search) && search.Length > ValidationLimits.SearchMaxLength
                ? Messages.Error.FieldTooLong("búsqueda", ValidationLimits.SearchMaxLength)
                : null;
        }

        public static string ValidatePositiveId(int? id, string fieldName)
        {
            return id.HasValue && id.Value <= 0
                ? Messages.Error.FieldGraterThanZero(fieldName)
                : null;
        }
    }
}
