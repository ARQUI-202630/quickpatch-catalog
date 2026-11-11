namespace QuickPatch.Catalog.Api.Security;

/// <summary>
/// Tenant de una petición que admite token o canal (listado de categorías, contrato 1.2.0). Con token, el tenant sale
/// del token, y un token inválido nunca cae al canal. Sin token, sale del canal configurado (<see cref="ChannelOptions"/>);
/// nunca de un <c>tenant_id</c> enviado por el cliente (RN-U5).
/// </summary>
public static class RequestTenant
{
    public enum Result
    {
        Resolved,
        InvalidToken,
        ChannelWithoutTenant,
    }

    public readonly record struct Resolution(Result Outcome, Guid TenantId);

    public static Resolution Resolve(HttpContext context, ChannelOptions channel)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(channel);
        if (context.Request.Headers.Authorization.Count > 0)
        {
            return AuthenticatedUser.TryFrom(context.User, out var user)
                ? new Resolution(Result.Resolved, user.TenantId)
                : new Resolution(Result.InvalidToken, Guid.Empty);
        }

        return channel.TryResolve(context.Request.Headers["X-Channel-Id"].ToString(), out var channelTenant)
            ? new Resolution(Result.Resolved, channelTenant)
            : new Resolution(Result.ChannelWithoutTenant, Guid.Empty);
    }
}