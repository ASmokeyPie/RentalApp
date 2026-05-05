using System.Net;
using System.Net.Http.Headers;

namespace RentalApp.Services;

/// <summary>
/// HTTP pipeline handler that:
///   (1) attaches the stored Bearer token to outbound requests when one is
///       present and not expired;
///   (2) detects server-side rejection of that token (401) and raises
///       <see cref="AuthenticationExpired"/> so the auth service can sign
///       the user out and redirect to Login.
///
/// Deliberately does not fire AuthenticationExpired for 401s that happen
/// without a token attached — those are just "bad credentials" on the
/// /auth/token login endpoint.
/// </summary>
public class AuthDelegatingHandler : DelegatingHandler
{
    private readonly ITokenStorage _tokenStorage;

    /// <summary>
    /// Raised when the server rejects a request that was authenticated with
    /// a stored token. The handler has already cleared the token by this
    /// point; subscribers should clear any in-memory session and redirect
    /// to the login page.
    /// </summary>
    public event EventHandler? AuthenticationExpired;

    public AuthDelegatingHandler(ITokenStorage tokenStorage)
    {
        // Storage is the single source of truth for the persisted JWT.
        _tokenStorage = tokenStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Tracks whether this request was authenticated (either by attaching
        // a stored token, or because the caller provided Authorisation).
        // We only treat 401 as "session expired" when the request was authenticated.
        var attachedToken = false;

        if (request.Headers.Authorization is null)
        {
            // No Authorisation header yet — opportunistically attach a valid stored JWT.
            // (GetValidTokenAsync applies a proactive expiry check.)
            var token = await _tokenStorage.GetValidTokenAsync();
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                attachedToken = true;
            }
        }
        else
        {
            // Caller supplied their own Authorisation header — treat that as
            // "this request is authenticated" so a 401 gets the session-expired
            // treatment. (In practice the app doesn't set this explicitly,
            // but this keeps behaviour predictable.)
            attachedToken = true;
        }

        // Continue the pipeline and send the request.
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && attachedToken)
        {
            // The server rejected an authenticated request: clear persisted token
            // and notify subscribers so the app can redirect to Login.
            await _tokenStorage.ClearAsync();
            AuthenticationExpired?.Invoke(this, EventArgs.Empty);
        }

        return response;
    }
}
