using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace LifeAchievements.Web.Services;

/// <summary>
/// Blazor's &lt;AuthorizeView&gt; and &lt;AuthorizeRouteView&gt; ask this class who is signed in.
/// Here that is simply "is there a token in localStorage", since the API is the real gate -
/// the UI only uses this to decide which screen to show.
/// </summary>
public class TokenAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStore _tokens;

    public TokenAuthStateProvider(TokenStore tokens, AuthEvents authEvents)
    {
        _tokens = tokens;
        authEvents.Changed += () => NotifyAuthenticationStateChanged(BuildState());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => BuildState();

    private async Task<AuthenticationState> BuildState()
    {
        var stored = await _tokens.GetAsync();

        if (stored is null)
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, stored.DisplayName),
            new Claim(ClaimTypes.Email, stored.Email)
        ], authenticationType: "bearer");

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
