using System.Collections.Generic;

namespace LaNata_Api.Core.Models.Internal
{
    public class RepriceResult
    {
        public int Lines { get; set; }
        public int Skipped { get; set; }
        public List<int> OrderIds { get; set; } = [];

        public int Orders
        {
            get { return OrderIds.Count; }
        }
    }
}
