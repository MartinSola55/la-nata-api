using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Order
{
    public class ConfirmRequest
    {
        public int Id { get; set; }
    }

    public class ConfirmResponse
    {
        public int Id { get; set; }
        public List<MissingStockItem> Missing { get; set; } = [];

        public class MissingStockItem
        {
            public int ProductId { get; set; }
            public string Description { get; set; }
            public int? VariantId { get; set; }
            public string VariantDescription { get; set; }
            public int RequestedQuantity { get; set; }
            public int AvailableStock { get; set; }
        }
    }
}
