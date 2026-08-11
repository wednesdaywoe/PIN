using System.Threading.Tasks;
using WebHost.OperatorApi.Exceptions;

namespace WebHost.OperatorApi.Capability;

public class CapabilityRepository : ICapabilityRepository
{
    public async Task<HostInformation> GetHostInformationAsync(string environment, int build)
    {
        return await Task.FromResult(new HostInformation
                                     {
                                         // Plain HTTP on purpose. Wine's WinHTTP deadlocks its own critical
                                         // section during the concurrent TLS handshakes the client fires at
                                         // world entry, which hangs the process. Every port here is the HTTP
                                         // twin of the HTTPS one in config/appsettings.json (443xx -> 4x xx).
                                         // The client refuses a non-HTTPS oracle URL unless FirefallClient.exe
                                         // is patched at 0x4950af (74 -> EB); see Docs/Http-Only-Setup.md.
                                         FrontendHost = "http://localhost:4499",
                                         StoreHost = "http://localhost:4499",
                                         ChatServer = "http://localhost:4407",
                                         ReplayHost = $"http://localhost:4499/{environment}-{build}",
                                         WebHost = "http://localhost:4499",
                                         MarketHost = "http://localhost:4499",
                                         IngameHost = "http://localhost:4403",
                                         ClientapiHost = "http://localhost:4402",
                                         WebAssetHost = "http://localhost:4499",
                                         WebAccountsHost = "http://localhost:4499",
                                         RhsigscanHost = "http://localhost:4499"
                                     });
    }

    public async Task<ProductInformation> GetProductInformationAsync(string productName)
    {
        if (productName != "Firefall_Beta")
        {
            throw new NotFoundException($"Product '{productName}' is unknown");
        }

        return await Task.FromResult(new ProductInformation { Build = "beta-1973", Environment = "production", Region = "NA", PatchLevel = 0 });
    }
}