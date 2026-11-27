using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LifeAchievements.Shared.Dtos;

namespace LifeAchievements.Web.Services;

public class ApiException(string message) : Exception(message);

/// <summary>
/// Every call to the API goes through here. It attaches the bearer token, refreshes it when
/// it is about to expire, and turns a ProblemDetails body into a message worth showing.
/// </summary>
public class GameApiClient(HttpClient http, TokenStore tokens, AuthEvents authEvents)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------- auth ----------

    public async Task SignUpAsync(string email, string password, string displayName)
    {
        var response = await http.PostAsJsonAsync("auth/signup", new RegisterRequest(email, password, displayName));
        await ThrowIfFailed(response);
    }

    public async Task SignInAsync(string email, string password, string? displayNameHint = null)
    {
        var response = await http.PostAsJsonAsync("auth/login", new LoginRequest(email, password));
        await ThrowIfFailed(response);

        var token = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(Json)
            ?? throw new ApiException("The server did not hand back a token.");

        await tokens.SaveAsync(token, email, displayNameHint ?? email);
        authEvents.NotifyChanged();
    }

    public async Task SignOutAsync()
    {
        await tokens.ClearAsync();
        authEvents.NotifyChanged();
    }

    // ---------- game ----------

    public Task<MeDto> GetMeAsync() => Get<MeDto>("me");

    public Task<List<AchievementDto>> GetAchievementsAsync(string? category = null) =>
        Get<List<AchievementDto>>(string.IsNullOrWhiteSpace(category) || category == "All"
            ? "achievements"
            : $"achievements?category={Uri.EscapeDataString(category)}");

    public Task<List<RecentUnlockDto>> GetRecentUnlocksAsync(int take = 10) =>
        Get<List<RecentUnlockDto>>($"me/unlocks?take={take}");

    public async Task<LogProgressResultDto> LogProgressAsync(Guid achievementId, int amount = 1)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"achievements/{achievementId}/progress")
        {
            Content = JsonContent.Create(new LogProgressRequest(amount))
        };
        var response = await SendAuthorised(request);
        await ThrowIfFailed(response);

        return await response.Content.ReadFromJsonAsync<LogProgressResultDto>(Json)
               ?? throw new ApiException("The server did not describe the progress.");
    }

    public async Task SetUnlockNoteAsync(Guid userAchievementId, string? note)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"me/unlocks/{userAchievementId}/note")
        {
            Content = JsonContent.Create(new SetUnlockNoteRequest(note))
        };
        var response = await SendAuthorised(request);
        await ThrowIfFailed(response);
    }

    public async Task<UnlockResultDto> ClaimAsync(Guid achievementId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"achievements/{achievementId}/claim");
        var response = await SendAuthorised(request);
        await ThrowIfFailed(response);

        return await response.Content.ReadFromJsonAsync<UnlockResultDto>(Json)
               ?? throw new ApiException("The server did not describe the unlock.");
    }

    // ---------- social (Phase 2) ----------

    public Task<FriendsOverviewDto> GetFriendsAsync() => Get<FriendsOverviewDto>("friends");

    public Task<WeeklyLeaderboardDto> GetLeaderboardAsync() =>
        Get<WeeklyLeaderboardDto>("friends/leaderboard");

    public Task<List<FeedItemDto>> GetFeedAsync(int take = 30) =>
        Get<List<FeedItemDto>>($"feed?take={take}");

    public Task<List<PendingVerificationDto>> GetPendingVerificationsAsync() =>
        Get<List<PendingVerificationDto>>("verifications");

    public Task<NotificationsDto> GetNotificationsAsync() =>
        Get<NotificationsDto>("notifications");

    public async Task MarkNotificationsSeenAsync() =>
        await PostAuthorised("notifications/seen", new { });

    public async Task SendFriendRequestAsync(string email) =>
        await PostAuthorised("friends/requests", new SendFriendRequestRequest(email));

    public async Task RespondToFriendRequestAsync(Guid friendshipId, bool accept) =>
        await PostAuthorised($"friends/requests/{friendshipId}", new RespondToFriendRequestRequest(accept));

    public async Task<ReactionResultDto> ToggleReactionAsync(Guid userAchievementId, string type)
    {
        var response = await PostAuthorised($"feed/{userAchievementId}/reactions", new ToggleReactionRequest(type));
        return await response.Content.ReadFromJsonAsync<ReactionResultDto>(Json)
               ?? throw new ApiException("The server did not describe the reaction.");
    }

    public async Task<VerifyClaimResultDto> VerifyClaimAsync(Guid userAchievementId, bool approve)
    {
        var response = await PostAuthorised($"verifications/{userAchievementId}", new VerifyClaimRequest(approve));
        return await response.Content.ReadFromJsonAsync<VerifyClaimResultDto>(Json)
               ?? throw new ApiException("The server did not describe the verification.");
    }

    private async Task<HttpResponseMessage> PostAuthorised<TBody>(string url, TBody body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        var response = await SendAuthorised(request);
        await ThrowIfFailed(response);
        return response;
    }

    // ---------- plumbing ----------

    private async Task<T> Get<T>(string url)
    {
        var response = await SendAuthorised(new HttpRequestMessage(HttpMethod.Get, url));
        await ThrowIfFailed(response);

        return await response.Content.ReadFromJsonAsync<T>(Json)
               ?? throw new ApiException("The server sent back an empty response.");
    }

    private async Task<HttpResponseMessage> SendAuthorised(HttpRequestMessage request)
    {
        var stored = await EnsureFreshToken();
        if (stored is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stored.AccessToken);

        var response = await http.SendAsync(request);

        // A 401 means the session is genuinely over - drop it and let the UI route to login.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await tokens.ClearAsync();
            authEvents.NotifyChanged();
        }

        return response;
    }

    private async Task<StoredTokens?> EnsureFreshToken()
    {
        var stored = await tokens.GetAsync();
        if (stored is null || !stored.NeedsRefresh) return stored;

        try
        {
            var response = await http.PostAsJsonAsync("auth/refresh", new { refreshToken = stored.RefreshToken });
            if (!response.IsSuccessStatusCode)
            {
                await tokens.ClearAsync();
                authEvents.NotifyChanged();
                return null;
            }

            var token = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(Json);
            if (token is null) return stored;

            await tokens.SaveAsync(token, stored.Email, stored.DisplayName);
            return await tokens.GetAsync();
        }
        catch (HttpRequestException)
        {
            return stored;
        }
    }

    private static async Task ThrowIfFailed(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Those details did not work. Try again.",
            HttpStatusCode.NotFound => "That is not here.",
            _ => "Something went wrong. Try again in a moment."
        };

        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("detail", out var detail)
                    && detail.GetString() is { Length: > 0 } text)
                {
                    message = text;
                }
            }
        }
        catch (JsonException)
        {
            // Not a ProblemDetails body - keep the generic message.
        }

        throw new ApiException(message);
    }
}

/// <summary>Lets the auth state provider know when a sign-in or sign-out happened.</summary>
public class AuthEvents
{
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}
