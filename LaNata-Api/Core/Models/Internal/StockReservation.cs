using System.Collections.Generic;

namespace LaNata_Api.Core.Models.Internal
{
    public class StockReservation
    {
        public Dictionary<int, int> ByProduct { get; set; } = [];
        public Dictionary<int, int> ByVariant { get; set; } = [];

        public int ForProduct(int productId)
        {
            return ByProduct.GetValueOrDefault(productId);
        }

        public int ForVariant(int variantId)
        {
            return ByVariant.GetValueOrDefault(variantId);
        }
    }
}
