namespace LaNata_Api.Core.Models.DTO.User
{
    public class GetComboUsersRequest
    {
        public int? UserId { get; set; }
        public bool IncludeAll { get; set; }
    }
}
