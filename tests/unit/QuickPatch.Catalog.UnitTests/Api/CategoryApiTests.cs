using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.UnitTests.Support;

namespace QuickPatch.Catalog.UnitTests.Api;

public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    public InMemoryCatalog Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:PublicKeyPem", TestTokens.PublicKeyPem);
        builder.UseSetting("ConnectionStrings:Catalog", "Host=sin-base-en-pruebas-unitarias");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ITenantUnitOfWork>(Store);
            services.AddSingleton<ICategoryRepository>(Store);
            services.AddSingleton<IOutbox>(Store);
            services.Configure<HealthCheckServiceOptions>(o => o.Registrations.Clear());
        });
    }
}

public class CategoryApiTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private static readonly Uri Admin = new("/v1/catalog/admin/categories", UriKind.Relative);
    private static readonly Uri List = new("/v1/catalog/categories", UriKind.Relative);

    private HttpClient Client(string role, Guid tenant)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(Guid.NewGuid(), tenant, role));
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task AdminCrea_YCualquierRolDelTenantLista()
    {
        var tenant = Guid.NewGuid();
        using var admin = Client("admin_tenant", tenant);
        using var cliente = Client("cliente", tenant);

        using var created = await admin.PostAsJsonAsync(Admin, new { name = "Plomería", description = "Fugas" });
        using var list = await cliente.GetAsync(List);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await Json(created);
        Assert.True(body.GetProperty("active").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var item = Assert.Single((await Json(list)).EnumerateArray());
        Assert.Equal("Plomería", item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task NombreRepetido_409_YRolNoAdmin_403_YSinToken_401()
    {
        var tenant = Guid.NewGuid();
        using var admin = Client("admin_tenant", tenant);
        using var cliente = Client("cliente", tenant);
        using var anonimo = factory.CreateClient();
        using (await admin.PostAsJsonAsync(Admin, new { name = "Cerrajería" }))
        {
        }

        using var repetido = await admin.PostAsJsonAsync(Admin, new { name = "CERRAJERÍA" });
        using var prohibido = await cliente.PostAsJsonAsync(Admin, new { name = "Otra" });
        using var sinToken = await anonimo.GetAsync(List);

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal("https://quickpatch.internal/problems/categoria-registrada", (await Json(repetido)).GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, prohibido.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);
    }

    [Fact]
    public async Task Desactivar_200_DeOtroTenant_404_YSinCampos_400()
    {
        var tenant = Guid.NewGuid();
        using var admin = Client("admin_tenant", tenant);
        using var otroAdmin = Client("admin_tenant", Guid.NewGuid());
        using var created = await admin.PostAsJsonAsync(Admin, new { name = "Electricidad" });
        var id = (await Json(created)).GetProperty("id").GetGuid();
        var ruta = new Uri($"/v1/catalog/admin/categories/{id}", UriKind.Relative);

        using var desactivada = await admin.PatchAsJsonAsync(ruta, new { active = false });
        using var deOtro = await otroAdmin.PatchAsJsonAsync(ruta, new { active = false });
        using var vacio = await admin.PatchAsJsonAsync(ruta, new { });
        using var list = await admin.GetAsync(List);

        Assert.Equal(HttpStatusCode.OK, desactivada.StatusCode);
        Assert.False((await Json(desactivada)).GetProperty("active").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, deOtro.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, vacio.StatusCode);
        Assert.Empty((await Json(list)).EnumerateArray());
    }

    [Fact]
    public async Task DatosInvalidos_400_YCampoDesconocido_400()
    {
        using var admin = Client("admin_tenant", Guid.NewGuid());
        using var content = new StringContent($$"""{"name":"Gas","tenantId":"{{Guid.NewGuid()}}"}""", Encoding.UTF8, "application/json");

        using var invalido = await admin.PostAsJsonAsync(Admin, new { name = "G" });
        using var desconocido = await admin.PostAsync(Admin, content);

        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.True((await Json(invalido)).GetProperty("errors").TryGetProperty("name", out _));
        Assert.Equal(HttpStatusCode.BadRequest, desconocido.StatusCode);
    }

    [Fact]
    public async Task TokenSinTenant_401_YHealth_200()
    {
        using var client = Client("admin_tenant", Guid.Empty);

        using var list = await client.GetAsync(List);
        using var create = await client.PostAsJsonAsync(Admin, new { name = "Gas" });
        using var patch = await client.PatchAsJsonAsync(new Uri($"/v1/catalog/admin/categories/{Guid.NewGuid()}", UriKind.Relative), new { active = true });
        using var live = await factory.CreateClient().GetAsync(new Uri("/health/live", UriKind.Relative));
        using var ready = await factory.CreateClient().GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, patch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }
}