using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CryptoPriceTracker.Api.Data;
using CryptoPriceTracker.Api.Models;

namespace CryptoPriceTracker.Api.Services;

public class CryptoPriceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly HttpClient _httpClient;

    public CryptoPriceService(ApplicationDbContext dbContext, HttpClient httpClient)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;

        // Important: CoinGecko blocks requests without User-Agent
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoPriceTrackerApp/1.0");
    }

    /// <summary>
    /// Fetch latest coin prices from CoinGecko and save to database
    /// </summary>
    public async Task UpdatePricesAsync()
    {
        var assets = await _dbContext.CryptoAssets.ToListAsync();

        // If no assets in DB, fetch top 10 default coins
        if (!assets.Any())
        {
            await FetchDefaultCoinsAsync();
            assets = await _dbContext.CryptoAssets.ToListAsync();
        }

        var ids = string.Join(",", assets.Select(a => a.ExternalId));
        var url = $"https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&ids={ids}&order=market_cap_desc";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching prices: {ex.Message}");
            return;
        }

        var json = await response.Content.ReadAsStringAsync();
        var marketData = JsonSerializer.Deserialize<List<CoinGeckoMarketDto>>(json);

        if (marketData == null) return;

        var today = DateTime.UtcNow.Date;

        foreach (var asset in assets)
        {
            var coin = marketData.FirstOrDefault(c => c.id == asset.ExternalId);
            if (coin == null) continue;

            // Save icon once
            if (string.IsNullOrEmpty(asset.IconUrl))
                asset.IconUrl = coin.image;

            // Prevent duplicate price entry per day
            bool exists = await _dbContext.CryptoPriceHistories
                .AnyAsync(p => p.CryptoAssetId == asset.Id && p.Date == today);

            if (exists) continue;

            _dbContext.CryptoPriceHistories.Add(new CryptoPriceHistory
            {
                CryptoAssetId = asset.Id,
                Price = coin.current_price,
                Date = today
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Fetch top 10 coins from CoinGecko if DB is empty
    /// </summary>
    private async Task FetchDefaultCoinsAsync()
    {
        var url = "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&order=market_cap_desc&per_page=10&page=1";
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching default coins: {ex.Message}");
            return;
        }

        var json = await response.Content.ReadAsStringAsync();
        var marketData = JsonSerializer.Deserialize<List<CoinGeckoMarketDto>>(json);

        if (marketData == null) return;

        foreach (var coin in marketData)
        {
            // Prevent duplicate assets
            if (await _dbContext.CryptoAssets.AnyAsync(a => a.ExternalId == coin.id)) continue;

            _dbContext.CryptoAssets.Add(new CryptoAsset
            {
                Name = coin.name,
                Symbol = coin.symbol,
                ExternalId = coin.id,
                IconUrl = coin.image
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    // DTO for CoinGecko response
    private class CoinGeckoMarketDto
    {
        public string id { get; set; } = null!;
        public string symbol { get; set; } = null!;
        public string name { get; set; } = null!;
        public decimal current_price { get; set; }
        public string image { get; set; } = null!;
    }
}
