using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CryptoPriceTracker.Api.Data;
using CryptoPriceTracker.Api.Services;

namespace CryptoPriceTracker.Api.Controllers;

[Route("api/crypto")]
[ApiController]
public class CryptoController : ControllerBase
{
    private readonly CryptoPriceService _service;
    private readonly ApplicationDbContext _dbContext;

    public CryptoController(CryptoPriceService service, ApplicationDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [HttpPost("update-prices")]
    public async Task<IActionResult> UpdatePrices()
    {
        await _service.UpdatePricesAsync();
        return Ok(new { success = true, message = "Crypto prices updated successfully." });
    }

    [HttpGet("latest-prices")]
    public async Task<IActionResult> GetLatestPrices()
    {
        var coins = await _dbContext.CryptoAssets
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Symbol,
                c.IconUrl,
                CurrentPrice = c.PriceHistory
                    .OrderByDescending(p => p.Date)
                    .Select(p => p.Price)
                    .FirstOrDefault(),
                PreviousPrice = c.PriceHistory
                    .OrderByDescending(p => p.Date)
                    .Skip(1)
                    .Select(p => p.Price)
                    .FirstOrDefault(),
                LastUpdated = c.PriceHistory
                    .OrderByDescending(p => p.Date)
                    .Select(p => p.Date)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(coins);
    }
}
