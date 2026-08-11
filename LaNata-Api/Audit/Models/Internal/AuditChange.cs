namespace LaNata_Api.Audit.Models.Internal
{
    public class AuditChange
    {
        // Field is the property name, FieldName the label shown to the user
        public string Field { get; set; }
        public string FieldName { get; set; }
        public string PreviousValue { get; set; }
        public string UpdatedValue { get; set; }
        public string Description { get; set; }
    }
}
