using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dashboard.Pages;

public class ServerModel : PageModel
{
    private readonly IConfiguration _config;
    public ServerModel(IConfiguration config) => _config = config;

    public string ApiUrl { get; set; } = "";

    public void OnGet()
    {
        // PUBLIC_API_URL = URL accessible from browser (localhost:7777)
        // API_URL = Docker-internal (api:7777) for server-side calls only
        ApiUrl = _config["PUBLIC_API_URL"] ?? _config["API_URL"] ?? "http://localhost:7777";
    }
}
