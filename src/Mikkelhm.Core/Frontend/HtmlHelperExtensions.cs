using System.Reflection;

namespace Mikkelhm.Core.Frontend
{
    public static class HtmlHelperExtensions
    {
        /// <summary>
        /// Returns a tag weight based on the current tag collection out of x
        /// </summary>
        /// <param name="postsByTag"></param>
        /// <param name="maxWeight"></param>
        /// <returns></returns>
        public static int GetTagWeight(int tagCount, decimal maxWeight, decimal maxCount)
        {
            return Convert.ToInt32(Math.Ceiling((decimal)tagCount * maxWeight / (decimal)maxCount));
        }

        public static string GetUmbracoVersion()
        {
            var version = typeof(Umbraco.Cms.Core.Constants).Assembly.GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Revision}";
        }
    }
}
