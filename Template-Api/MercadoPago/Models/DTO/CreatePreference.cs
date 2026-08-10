using System.Collections.Generic;

namespace Template_Api.MercadoPago.Models.DTO
{
    public class CreatePreferenceRequest
    {
        public string ExternalReference { get; set; }
        public string PayerEmail { get; set; }
        public string PayerName { get; set; }
        public string PayerLastName { get; set; }
        public bool? BinaryMode { get; set; }
        public List<PreferenceItem> Items { get; set; } = [];
        
        public class PreferenceItem
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string PictureUrl { get; set; }
            public string CategoryId { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }
    }

    public class CreateCartPreferenceRequest
    {
        public int CartId { get; set; }
        public string PayerEmail { get; set; }
        public string PayerName { get; set; }
        public string PayerLastName { get; set; }
        public bool? BinaryMode { get; set; }
    }

    public class CreatePreferenceResponse
    {
        public int Id { get; set; }
        public int? CartId { get; set; }
        public string PreferenceId { get; set; }
        public string ExternalReference { get; set; }
        public string Status { get; set; }
        public decimal TransactionAmount { get; set; }
        public string CurrencyId { get; set; }
        public string InitPoint { get; set; }
        public string SandboxInitPoint { get; set; }
    }
}
