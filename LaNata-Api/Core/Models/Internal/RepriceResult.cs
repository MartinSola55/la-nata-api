using System;
using System.Collections.Generic;
using System.Linq;

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

    public class RepriceSummary
    {
        // Dictionary rejects a null key even when it is int?, so a line with no variant is keyed 0
        private const int NoVariant = 0;

        public Dictionary<int, RepriceResult> ByVariant { get; } = [];

        public int Lines
        {
            get { return ByVariant.Values.Sum(x => x.Lines); }
        }

        public int Skipped
        {
            get { return ByVariant.Values.Sum(x => x.Skipped); }
        }

        public List<int> OrderIds
        {
            get { return [.. ByVariant.Values.SelectMany(x => x.OrderIds).Distinct().Order()]; }
        }

        public int Orders
        {
            get { return OrderIds.Count; }
        }

        public RepriceResult For(int? variantId)
        {
            return ByVariant.TryGetValue(variantId ?? NoVariant, out var result) ? result : new RepriceResult();
        }

        public RepriceResult Fold(Func<int, bool> predicate)
        {
            var folded = new RepriceResult();

            foreach (var bucket in ByVariant.Where(x => predicate(x.Key)))
            {
                folded.Lines += bucket.Value.Lines;
                folded.Skipped += bucket.Value.Skipped;

                foreach (var orderId in bucket.Value.OrderIds.Where(x => !folded.OrderIds.Contains(x)))
                    folded.OrderIds.Add(orderId);
            }

            return folded;
        }

        public RepriceResult Track(int? variantId)
        {
            var key = variantId ?? NoVariant;

            if (!ByVariant.TryGetValue(key, out var result))
            {
                result = new RepriceResult();
                ByVariant[key] = result;
            }

            return result;
        }
    }
}
