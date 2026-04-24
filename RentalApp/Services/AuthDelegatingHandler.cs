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
/// We deliberately do NOT fire AuthenticationExpired for 401s that happen
/// *without* a token attached — those are just "bad credentials" on the
/// /auth/token login endpoint.
/// </summary>
public class AuthDelegatingHandler : DelegatingHandler
{
    private readonly ITokenStorage _tokenStorage;

    /// <summary>
    /// Raised when the server rejects a request that we authenticated with
    /// a stored token. The handler has already cleared the token by this
    /// point; subscribers should clear any in-memory session and redirect
    /// to the login page.
    /// </summary>
    public event EventHandler? AuthenticationExpired;

    public AuthDelegatingHandler(ITokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var attachedToken = false;

        if (request.Headers.Authorization is null)
        {
            var token = await _tokenStorage.GetValidTokenAsync();
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                attachedToken = true;
            }
        }
        else
        {
            // Caller supplied their own Authorization header — treat that as
            // "this request is authenticated" so a 401 gets the session-expired
            // treatment. (In practice the app doesn't set this explicitly,
            // but this keeps behaviour predictable.)
            attachedToken = true;
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && attachedToken)
        {
            await _tokenStorage.ClearAsync();
            AuthenticationExpired?.Invoke(this, EventArgs.Empty);
        }

        return response;
    }
}
