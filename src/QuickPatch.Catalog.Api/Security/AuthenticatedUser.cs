using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.JwtBearer;

using QuickPatch.Transversal.Security;

namespace QuickPatch.Catalog.Api.Security;

/// <summary>
/// Contexto autenticado (SDD: <c>AuthenticatedContext</c>) leído de los claims del JWT emitido por Identity:
/// <c>sub</c> (userId), <c>tenant_id</c> (tenantId) y <c>role</c>. El tenant nunca se toma del cuerpo ni de cabeceras.
/// </summary>
public sealed record AuthenticatedUser(Guid UserId, Guid TenantId, string Role)
{
    public const string SubjectClaim = JwtClaims.Subject;
    public const string TenantClaim = JwtClaims.Tenant;
    public const string RoleClaim = JwtClaims.Role;
    public const string AdminTenantRole = "admin_tenant";
    public const string AdminTenantPolicy = "SoloAdminTenant";

    public static bool TryFrom(ClaimsPrincipal principal, out AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(principal);
        user = null!;
        if (!JwtClaims.TryRead(principal, out var userId, out var tenantId, out var role))
        {
            return false;
        }

        user = new AuthenticatedUser(userId, tenantId, role);
        return true;
    }
}

public static class AuthenticationSetup
{
    public static IServiceCollection AddQuickPatchAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(JwtSettings.Section).Get<JwtSettings>() ?? new JwtSettings();

        services.AddQuickPatchJwtBearer(settings, TimeSpan.FromSeconds(30));

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthenticatedUser.AdminTenantPolicy, policy => policy.RequireRole(AuthenticatedUser.AdminTenantRole));
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, ForbiddenAuditHandler>();

        return services;
    }
}