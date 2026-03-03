using AzureDevOpsAgents.Api.Data;
using AzureDevOpsAgents.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AzureDevOpsAgents.Api.Controllers;

/// <summary>
/// OAuth 2.0 authorization code flow for Azure DevOps.
/// 
/// Flow:
///   1. GET /api/auth/ado/login?connectionId=xxx  → redirects to ADO OAuth consent
///   2. ADO redirects to GET /api/auth/ado/callback?code=xxx&state=connectionId
///   3. Backend exchanges code for tokens, stores encrypted in DB
///   4. Redirects frontend to /auth/success
/// </summary>
[ApiController]
[Route("api/auth/ado")]
public class AuthController(
    AppDbContext db,
    TokenEncryptionService encryption,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<AuthController> logger) : ControllerBase
{
    private string ClientId     => configuration["AzureDevOps:ClientId"]     ?? throw new InvalidOperationException("AzureDevOps:ClientId not set");
    private string ClientSecret => configuration["AzureDevOps:ClientSecret"] ?? throw new InvalidOperationException("AzureDevOps:ClientSecret not set");
    private string CallbackUrl  => configuration["AzureDevOps:CallbackUrl"]  ?? throw new InvalidOperationException("AzureDevOps:CallbackUrl not set");

    /// <summary>Start the OAuth flow for a connection.</summary>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] Guid connectionId)
    {
        // state = connectionId so we can look it up in the callback
        var authUrl = $"https://app.vssps.visualstudio.com/oauth2/authorize" +
            $"?client_id={Uri.EscapeDataString(ClientId)}" +
            $"&response_type=Assertion" +
            $"&state={connectionId}" +
            $"&scope={Uri.EscapeDataString("vso.work_full vso.code_full vso.build_execute")}" +
            $"&redirect_uri={Uri.EscapeDataString(CallbackUrl)}";

        return Redirect(authUrl);
    }

    /// <summary>OAuth callback — exchange code for tokens and store in DB.</summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, CancellationToken ct)
    {
        if (!Guid.TryParse(state, out var connectionId))
            return BadRequest("Invalid state parameter.");

        var connection = await db.Connections.FindAsync([connectionId], ct);
        if (connection is null)
            return NotFound($"Connection {connectionId} not found.");

        // Exchange the authorization code for tokens
        var http = httpClientFactory.CreateClient();
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"]      = ClientSecret,
            ["grant_type"]            = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"]             = code,
            ["redirect_uri"]          = CallbackUrl
        });

        var response = await http.PostAsync("https://app.vssps.visualstudio.com/oauth2/token", tokenRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Token exchange failed: {Error}", error);
            return Redirect("/auth/error?reason=token_exchange_failed");
        }

        var json   = await response.Content.ReadAsStringAsync(ct);
        var tokens = JsonDocument.Parse(json).RootElement;

        connection.AccessToken    = encryption.Encrypt(tokens.GetProperty("access_token").GetString()!);
        connection.RefreshToken   = encryption.Encrypt(tokens.GetProperty("refresh_token").GetString()!);
        connection.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokens.GetProperty("expires_in").GetInt32());
        connection.UpdatedAt      = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("OAuth tokens stored for connection {Id}", connectionId);

        // Redirect frontend to success page with the connection ID
        return Redirect($"/auth/success?connectionId={connectionId}");
    }

    /// <summary>
    /// Refresh an expired access token using the stored refresh token.
    /// Called automatically by services before making ADO API calls.
    /// </summary>
    [HttpPost("refresh/{connectionId:guid}")]
    public async Task<IActionResult> Refresh(Guid connectionId, CancellationToken ct)
    {
        var connection = await db.Connections.FindAsync([connectionId], ct);
        if (connection is null) return NotFound();
        if (connection.RefreshToken is null) return BadRequest("No refresh token stored.");

        var refreshToken = encryption.Decrypt(connection.RefreshToken);

        var http = httpClientFactory.CreateClient();
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"]      = ClientSecret,
            ["grant_type"]            = "refresh_token",
            ["assertion"]             = refreshToken,
            ["redirect_uri"]          = CallbackUrl
        });

        var response = await http.PostAsync("https://app.vssps.visualstudio.com/oauth2/token", tokenRequest, ct);
        if (!response.IsSuccessStatusCode) return StatusCode(502, "Token refresh failed.");

        var json   = await response.Content.ReadAsStringAsync(ct);
        var tokens = JsonDocument.Parse(json).RootElement;

        connection.AccessToken    = encryption.Encrypt(tokens.GetProperty("access_token").GetString()!);
        connection.RefreshToken   = encryption.Encrypt(tokens.GetProperty("refresh_token").GetString()!);
        connection.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokens.GetProperty("expires_in").GetInt32());
        connection.UpdatedAt      = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { expiresAt = connection.TokenExpiresAt });
    }
}
