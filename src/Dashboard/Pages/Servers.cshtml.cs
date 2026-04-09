using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dashboard.Pages;

public class ServersModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public ServersModel(IConfiguration config, IHttpClientFactory http)
    {
        _config = config;
        _http = http;
    }

    public string ApiUrl { get; set; } = "";       // server-side
    public string PublicApiUrl { get; set; } = "";  // client-side JS (browser)
    public List<ServerItem> Servers { get; set; } = new();

    public async Task OnGetAsync()
    {
        ApiUrl       = _config["API_URL"]        ?? "http://api:7777";
        PublicApiUrl = _config["PUBLIC_API_URL"] ?? _config["API_URL"] ?? "http://localhost:7777";
        try
        {
            var client = _http.CreateClient();
            var response = await client.GetFromJsonAsync<List<ServerItem>>($"{ApiUrl}/api/servers");
            Servers = response ?? new();
        }
        catch
        {
            Servers = new();
        }
    }
}

public record ServerItem(
    int Id,
    string Name,
    string Host,
    int Port,
    string CurrentMap,
    string Description,
    bool IsActive,
    int MaxPlayers
);
