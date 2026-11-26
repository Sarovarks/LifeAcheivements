using System.Text.Json;
using LifeAchievements.Shared.Dtos;
using Microsoft.JSInterop;

namespace LifeAchievements.Web.Services;

/// <summary>
/// Keeps the bearer tokens in the browser's localStorage so a refresh (or reopening the
/// installed PWA) does not throw you back to the login screen. Blazor WebAssembly has no
/// server-side session, so this is where "being signed in" actually lives.
/// </summary>
public class TokenStore(IJSRuntime js)
{
    private const string Key = "la.tokens";
    private StoredTokens? _cached;
    private bool _loaded;

    public async Task<StoredTokens?> GetAsync()
    {
        if (_loaded) return _cached;

        var raw = await js.InvokeAsync<string?>("localStorage.getItem", Key);
        _cached = string.IsNullOrWhiteSpace(raw) ? null : JsonSerializer.Deserialize<StoredTokens>(raw);
        _loaded = true;
        return _cached;
    }

    public async Task SaveAsync(AuthTokenResponse response, string email, string displayName)
    {
        _cached = new StoredTokens(
            response.AccessToken,
            response.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn),
            email,
            displayName);
        _loaded = true;

        await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(_cached));
    }

    public async Task UpdateDisplayNameAsync(string displayName)
    {
        var current = await GetAsync();
        if (current is null || current.DisplayName == displayName) return;

        _cached = current with { DisplayName = displayName };
        await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(_cached));
    }

    public async Task ClearAsync()
    {
        _cached = null;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.removeItem", Key);
    }
}

public record StoredTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    string Email,
    string DisplayName)
{
    /// <summary>Refresh a minute early so a request never goes out on a token about to die.</summary>
    public bool NeedsRefresh => DateTimeOffset.UtcNow >= ExpiresAtUtc.AddMinutes(-1);
}
