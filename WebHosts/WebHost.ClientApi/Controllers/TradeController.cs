using Microsoft.AspNetCore.Mvc;

namespace WebHost.ClientApi.Controllers;

[ApiController]
public class TradeController : ControllerBase
{
    [Route("api/v3/trade/products/garage_slot_perk_respec")]
    [HttpGet]
    public object GarageSlotPerkRespec()
    {
        return new { };
    }

    // No inventory-expansion products for sale. Inventory.lua sorts this array and
    // disables the buy button when it is empty; returning 404 instead makes that
    // handler index a nil response and throw, contributing to the in-game UI stalls.
    [Route("api/v3/trade/products/inventory_expansion")]
    [HttpGet]
    public object InventoryExpansion()
    {
        return System.Array.Empty<object>();
    }
}