using System.Diagnostics;

namespace YOU.Api.Contracts;

public static class TraceIds
{
    public static string Get(HttpContext httpContext)
    {
        return Activity.Current?.Id
            ?? httpContext.TraceIdentifier;
    }
}
