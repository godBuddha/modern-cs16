using System;
using System.IO;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Launcher;

class Program
{
    private static Sdl2Window _window;
    private static GraphicsDevice _gd;
    private static CommandList _cl;
    private static ImGuiRenderer _ig;
    private static HttpClient _http = new HttpClient();

    // UI State
    private static string _username = "";
    private static string _password = "";
    private static string _loginError = "";
    private static string _jwt = "";
    private static bool _isLoggedIn = false;
    private static string _apiBase = "http://localhost:7777";
    
    static void Main(string[] args)
    {
        WindowCreateInfo wci = new WindowCreateInfo
        {
            X = 100,
            Y = 100,
            WindowWidth = 800,
            WindowHeight = 600,
            WindowTitle = "CS 1.6 Modern Launcher",
            WindowInitialState = WindowState.Normal
        };

        VeldridStartup.CreateWindowAndGraphicsDevice(
            wci,
            new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
            GraphicsBackend.OpenGL,
            out _window,
            out _gd);

        _cl = _gd.ResourceFactory.CreateCommandList();
        _ig = new ImGuiRenderer(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);

        _window.Resized += () =>
        {
            _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
            _ig.WindowResized(_window.Width, _window.Height);
        };

        while (_window.Exists)
        {
            InputSnapshot snapshot = _window.PumpEvents();
            if (!_window.Exists) { break; }

            _ig.Update(1f / 60f, snapshot);

            SubmitUI();

            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, new RgbaFloat(0.1f, 0.1f, 0.12f, 1f));
            _ig.Render(_gd, _cl);
            _cl.End();

            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers(_gd.MainSwapchain);
        }

        _gd.WaitForIdle();
        _cl.Dispose();
        _ig.Dispose();
        _gd.Dispose();
    }

    static void SubmitUI()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(new Vector2(_window.Width, _window.Height));
        ImGui.Begin("Main", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

        if (!_isLoggedIn)
        {
            RenderLogin();
        }
        else
        {
            RenderDashboard();
        }

        ImGui.End();
    }

    static void RenderLogin()
    {
        ImGui.SetCursorPosX((_window.Width - 300) / 2);
        ImGui.SetCursorPosY((_window.Height - 200) / 2);
        ImGui.BeginChild("LoginBox", new Vector2(300, 200), ImGuiChildFlags.Borders);
        ImGui.TextUnformatted("Login to CS 1.6 Modern");
        ImGui.Separator();

        ImGui.InputText("Username", ref _username, 50);
        ImGui.InputText("Password", ref _password, 50, ImGuiInputTextFlags.Password);
        
        if (!string.IsNullOrEmpty(_loginError))
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), _loginError);
        }

        if (ImGui.Button("Login", new Vector2(100, 30)))
        {
            Task.Run(async () => {
                try {
                    var payload = new { login = _username, password = _password };
                    var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                    var res = await _http.PostAsync($"{_apiBase}/api/auth/login", content);
                    if (res.IsSuccessStatusCode) {
                        var json = await res.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        _jwt = doc.RootElement.GetProperty("token").GetString();
                        _isLoggedIn = true;
                        _loginError = "";
                    } else {
                        _loginError = "Invalid username or password.";
                    }
                } catch (Exception ex) {
                    _loginError = ex.Message;
                }
            });
        }
        ImGui.EndChild();
    }

    private static string _leaderboardMsg = "Fetching...";
    private static JsonElement _leaderboardData;
    private static bool _leaderboardFetched = false;
    
    private static string _kycFront = "";
    private static string _kycBack = "";
    private static string _kycVideo = "";
    private static string _kycMsg = "";

    static void RenderDashboard()
    {
        if (ImGui.BeginTabBar("Menu"))
        {
            if (ImGui.BeginTabItem("Servers"))
            {
                ImGui.Text("Available Servers:");
                if (ImGui.Button("Connect to modern-cs16 (Local)"))
                {
                    Console.WriteLine($"Starting CS 1.6: hl.exe -game cstrike +connect 127.0.0.1:27015 +setinfo _jwt \"{_jwt}\"");
                }
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Leaderboard"))
            {
                if (!_leaderboardFetched) {
                    _leaderboardFetched = true;
                    Task.Run(async () => {
                        try {
                            var res = await _http.GetAsync($"{_apiBase}/api/players/rankings");
                            if (res.IsSuccessStatusCode) {
                                var json = await res.Content.ReadAsStringAsync();
                                _leaderboardData = JsonDocument.Parse(json).RootElement;
                                _leaderboardMsg = "";
                            } else _leaderboardMsg = "Failed to fetch leaderboard.";
                        } catch { _leaderboardMsg = "API Error."; }
                    });
                }
                ImGui.Text("Top 100 Players by ELO");
                ImGui.Separator();
                if (!string.IsNullOrEmpty(_leaderboardMsg)) { ImGui.Text(_leaderboardMsg); }
                else if (_leaderboardData.ValueKind == JsonValueKind.Array) {
                    if (ImGui.BeginTable("Ranks", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg)) {
                        ImGui.TableSetupColumn("Player"); ImGui.TableSetupColumn("ELO"); ImGui.TableSetupColumn("Wins");
                        ImGui.TableHeadersRow();
                        foreach(var p in _leaderboardData.EnumerateArray()) {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn(); ImGui.Text(p.GetProperty("playerName").GetString());
                            ImGui.TableNextColumn(); ImGui.Text(p.GetProperty("elo").GetInt32().ToString());
                            ImGui.TableNextColumn(); ImGui.Text(p.GetProperty("wins").GetInt32().ToString());
                        }
                        ImGui.EndTable();
                    }
                }
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("KYC"))
            {
                ImGui.Text("Upload Identity Verification Documents");
                ImGui.Separator();
                ImGui.InputText("CCCD Front (Path)", ref _kycFront, 200);
                ImGui.InputText("CCCD Back (Path)", ref _kycBack, 200);
                ImGui.InputText("Video URL", ref _kycVideo, 200);
                
                if (ImGui.Button("Submit KYC"))
                {
                    Task.Run(async () => {
                        try {
                            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwt);
                            var form = new MultipartFormDataContent();
                            
                            // Mocking file reading if path exists or just send dummy bytes if empty
                            var frontBytes = File.Exists(_kycFront) ? File.ReadAllBytes(_kycFront) : new byte[100];
                            var backBytes = File.Exists(_kycBack) ? File.ReadAllBytes(_kycBack) : new byte[100];
                            
                            form.Add(new ByteArrayContent(frontBytes), "cccdFront", "front.jpg");
                            form.Add(new ByteArrayContent(backBytes), "cccdBack", "back.jpg");
                            form.Add(new StringContent(_kycVideo), "videoUrl");
                            
                            var res = await _http.PostAsync($"{_apiBase}/api/kyc/upload", form);
                            _kycMsg = res.IsSuccessStatusCode ? "KYC Uploaded Successfully!" : "KYC Upload Failed.";
                        } catch(Exception ex) { _kycMsg = "Error: " + ex.Message; }
                    });
                }
                if (!string.IsNullOrEmpty(_kycMsg)) {
                    ImGui.TextUnformatted(_kycMsg);
                }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }
}
