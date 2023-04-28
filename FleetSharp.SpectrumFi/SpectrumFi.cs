using System.Net.Http.Json;
using FleetSharp.CoinGecko;

namespace FleetSharp.SpectrumFi
{
    //https://api.spectrum.fi/v1/amm/markets
    internal class SpectrumFiPoolStats
    {
        public string? id { get; set; }
        public string? baseId { get; set; }
        public string? baseSymbol { get; set; }
        public string? quoteId { get; set; }
        public string? quoteSymbol { get; set; }
        public double lastPrice { get; set; }
        public long value { get; set; }
    }

    public static class SpectrumFi
    {
        private static HttpClient client = new HttpClient();
        private static List<SpectrumFiPoolStats>? poolStats = null;
        private static DateTime dtLastUpdate = DateTime.MinValue;
        private static SemaphoreSlim sema = new SemaphoreSlim(1, 1);

        public static string SpectrumFiAPI = "https://api.spectrum.fi/v1";
        public static int cacheResultsForXSeconds = 60 * 15;

        private static async Task<List<SpectrumFiPoolStats>?> GetAllPoolStats()
        {
            List<SpectrumFiPoolStats>? stats = null;
            stats = await client.GetFromJsonAsync<List<SpectrumFiPoolStats>>($"{SpectrumFiAPI}/amm/markets");
            return stats;
        }

        private static async Task TryUpdatePoolStats()
        {
            if ((DateTime.Now - dtLastUpdate).TotalSeconds >= cacheResultsForXSeconds)
            {
                await sema.WaitAsync();
                //Do check again now that we are "locked"
                if ((DateTime.Now - dtLastUpdate).TotalSeconds >= cacheResultsForXSeconds)
                {
                    try
                    {
                        var stats = await GetAllPoolStats();
                        if (stats != null)
                        {
                            dtLastUpdate = DateTime.Now;
                            if (poolStats == null) poolStats = stats;
                            else
                            {
                                poolStats = stats;
                            }
                        }
                    }
                    catch (Exception e)
                    {

                    }
                }
                sema.Release();
            }
        }

        public static async Task<double> GetLastPriceForTokenInERGCached(string tokenId)
        {
            await TryUpdatePoolStats();
            if (poolStats == null) return 0;

            var poolStat = poolStats.Where(x => x.baseId == "0000000000000000000000000000000000000000000000000000000000000000" && x.quoteId == tokenId).FirstOrDefault();
            if (poolStat == null) return 0;
            else return (1.0 / poolStat.lastPrice);
        }

        public static async Task<double> GetLastPriceForTokenInUSDCached(string tokenId)
        {
            var ergPrice = await CoinGecko.CoinGecko.GetCurrentERGPriceInUSDCached();

            //If we get the erg token id or "erg" (that's what rosen bridge returns), simply return the coingecko pricing.
            if (tokenId == "0000000000000000000000000000000000000000000000000000000000000000" || tokenId == "erg") return (ergPrice ?? 0);

            var priceInERG = await GetLastPriceForTokenInERGCached(tokenId);
            
            return (priceInERG * (ergPrice ?? 0));
        }
    }
}