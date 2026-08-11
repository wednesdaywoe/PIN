using System;
using Microsoft.AspNetCore.Mvc;

namespace WebHost.ClientApi.Controllers;

// Minimal stubs for endpoints the client polls but PIN does not implement yet. Each
// returns an empty JSON array rather than 404. The client's Lua treats every one of
// these responses as a list (it iterates the result or takes its length), so an empty
// array is the safe "nothing here" answer; a 404 instead makes those handlers index a
// nil response and throw, which contributes to the in-game UI stalls and crashes traced
// in the client logs. Replace any of these with real data if/when the feature is built.
[ApiController]
public class StubsController : ControllerBase
{
    // Looking-for-party list (Dashboard squad builder). OnListReceivedHelper reads
    // data.total_count and iterates data.results, so this must be an object with those
    // fields - a bare array makes it iterate a nil and throw.
    [Route("api/v3/squad_builder/lfp")]
    [HttpGet]
    public object SquadBuilderLfp()
    {
        return new { TotalCount = 0, Results = Array.Empty<object>() };
    }

    // Player's market sell listings. Market.lua does `g_Listings = args` then loops with
    // `#args`, so an array is required.
    [Route("api/v2/characters/{characterId}/market/listings")]
    [HttpGet]
    public object MarketListings(string characterId)
    {
        return Array.Empty<object>();
    }

    // Battleframes offered for sale (garage webframe, via lib_WebCache `frames_sale`).
    [Route("api/v3/garage_slots/battleframes_for_sale")]
    [HttpGet]
    public object BattleframesForSale()
    {
        return Array.Empty<object>();
    }
}
