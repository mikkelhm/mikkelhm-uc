using Microsoft.AspNetCore.Http;

namespace Mikkelhm.Core.Frontend.DisplayModels
{
    public class WeigthedTag
    {
        public string Tag { get; set; }
        public int Count { get; set; }
        public int Weight { get; set; }
        public string Url => PathString.FromUriComponent($"/tags/{Tag.ToLowerInvariant().Replace(" ", "-")}");

        public WeigthedTag(string tag, int count)
        {
            Tag = tag;
            Count = count;
        }

        public void SetWeight(int weight)
        {
            Weight = weight;
        }
    }
}
