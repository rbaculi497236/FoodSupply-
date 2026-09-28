using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FoodSupply.Tests;

public class CookieConsentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalCookiesRequireConsentWhileEssentialCookiesRemainAvailable(bool allowOptional)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var middleware = new CookiePolicyMiddleware(http =>
        {
            var consent = http.Features.Get<ITrackingConsentFeature>()!;
            if (allowOptional) consent.GrantConsent();
            http.Response.Cookies.Append("Essential", "yes", new CookieOptions { IsEssential = true });
            http.Response.Cookies.Append("Optional", "yes");
            return Task.CompletedTask;
        }, Options.Create(new CookiePolicyOptions { CheckConsentNeeded = _ => true }), NullLoggerFactory.Instance);
        await middleware.Invoke(context);
        var headers = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("Essential=yes", headers);
        Assert.Equal(allowOptional, headers.Contains("Optional=yes"));
    }
}
