using System.Text.Json;

using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.Application.Categories;
using QuickPatch.Catalog.Domain.Categories;
using QuickPatch.Catalog.Domain.Common;
using QuickPatch.Catalog.UnitTests.Support;

namespace QuickPatch.Catalog.UnitTests.Application;

public class CategoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
    private readonly InMemoryCatalog store = new();
    private readonly MutableClock clock = new(Now);
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid correlation = Guid.NewGuid();

    private CreateCategoryHandler Create() => new(store, store, store, clock);

    private UpdateCategoryHandler Update() => new(store, store, store, clock);

    private async Task<Category> CreatedAsync(string name = "Plomería", Guid? tenantId = null)
    {
        var result = await Create().HandleAsync(new CreateCategoryCommand(tenantId ?? tenant, correlation, name, "Fugas y tuberías"), CancellationToken.None);
        return Assert.IsType<CreateCategoryResult.Created>(result).Category;
    }

    [Fact]
    public async Task Crear_GuardaActivaYPublicaElEstadoCompleto()
    {
        var category = await CreatedAsync(" Plomería ");

        Assert.Equal("Plomería", category.Name);
        Assert.True(category.Active);
        var message = Assert.Single(store.Outbox);
        Assert.Equal("catalog.category-changed", message.EventType);
        Assert.Equal(category.Id, message.AggregateId);
        using var json = JsonDocument.Parse(message.Payload);
        var root = json.RootElement;
        Assert.Equal(message.Id, root.GetProperty("eventId").GetGuid());
        Assert.Equal(1, root.GetProperty("eventVersion").GetInt32());
        Assert.Equal("catalog-service", root.GetProperty("producer").GetString());
        Assert.Equal(tenant, root.GetProperty("tenantId").GetGuid());
        Assert.Equal(correlation, root.GetProperty("correlationId").GetGuid());
        var data = root.GetProperty("data");
        Assert.Equal(category.Id, data.GetProperty("categoryId").GetGuid());
        Assert.Equal("Plomería", data.GetProperty("name").GetString());
        Assert.True(data.GetProperty("active").GetBoolean());
        Assert.Equal(4, data.EnumerateObject().Count());
    }

    [Fact]
    public async Task Crear_NombreRepetidoSinImportarMayusculas_EsConflicto()
    {
        await CreatedAsync("Plomería");

        var result = await Create().HandleAsync(new CreateCategoryCommand(tenant, correlation, "PLOMERÍA", null), CancellationToken.None);

        Assert.IsType<CreateCategoryResult.NameTaken>(result);
        Assert.Single(store.Outbox);
    }

    [Fact]
    public async Task Crear_MismoNombreEnOtroTenant_EsPermitido_YCarreraConLaBase_EsConflicto()
    {
        await CreatedAsync("Plomería");
        await CreatedAsync("Plomería", Guid.NewGuid());
        store.FailNextSaveWithDuplicate = true;

        var carrera = await Create().HandleAsync(new CreateCategoryCommand(tenant, correlation, "Electricidad", null), CancellationToken.None);

        Assert.IsType<CreateCategoryResult.NameTaken>(carrera);
    }

    [Fact]
    public async Task Crear_DatosInvalidos_Falla()
    {
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            Create().HandleAsync(new CreateCategoryCommand(tenant, correlation, "P", new string('x', 256)), CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("name"));
        Assert.True(ex.Errors.ContainsKey("description"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Create().HandleAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Listar_SoloActivasDelTenantOrdenadas()
    {
        await CreatedAsync("Plomería");
        await CreatedAsync("Cerrajería");
        var electricidad = await CreatedAsync("Electricidad");
        await CreatedAsync("Otra", Guid.NewGuid());
        await Update().HandleAsync(new UpdateCategoryCommand(tenant, correlation, electricidad.Id, null, false), CancellationToken.None);

        var list = await new ListCategoriesHandler(store, store).HandleAsync(tenant, CancellationToken.None);

        Assert.Equal(["Cerrajería", "Plomería"], list.Select(c => c.Name));
    }

    [Fact]
    public async Task Desactivar_PublicaCambio_YRepetirSinCambios_NoPublica()
    {
        var category = await CreatedAsync();
        clock.Now = Now.AddMinutes(1);

        await Update().HandleAsync(new UpdateCategoryCommand(tenant, correlation, category.Id, null, false), CancellationToken.None);
        await Update().HandleAsync(new UpdateCategoryCommand(tenant, correlation, category.Id, null, false), CancellationToken.None);

        Assert.Equal(2, store.Outbox.Count);
        using var json = JsonDocument.Parse(store.Outbox[1].Payload);
        Assert.False(json.RootElement.GetProperty("data").GetProperty("active").GetBoolean());
        Assert.Equal(Now.AddMinutes(1), category.UpdatedAt);
    }

    [Fact]
    public async Task Actualizar_DescripcionVaciaLaBorra_YCategoriaDeOtroTenantNoExiste()
    {
        var category = await CreatedAsync();

        var updated = await Update().HandleAsync(new UpdateCategoryCommand(tenant, correlation, category.Id, "", null), CancellationToken.None);
        var otroTenant = await Update().HandleAsync(new UpdateCategoryCommand(Guid.NewGuid(), correlation, category.Id, null, false), CancellationToken.None);

        Assert.Null(updated!.Description);
        Assert.Null(otroTenant);
        await Assert.ThrowsAsync<ArgumentNullException>(() => Update().HandleAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Actualizar_SinCampos_Falla_YUpdatedAtSiempreAvanza()
    {
        var category = Category.Create(tenant, "Plomería", null, Now);

        Assert.Throws<DomainValidationException>(() => category.Update(null, null, Now));
        Assert.Throws<DomainValidationException>(() => category.Update(new string('x', 256), null, Now));
        Assert.True(category.Update("Nueva", null, Now.AddMinutes(-5)));
        Assert.True(category.UpdatedAt > Now);
    }

    [Fact]
    public void Crear_SinTenant_Falla_YExcepcionesEstandar()
    {
        Assert.Throws<DomainValidationException>(() => Category.Create(Guid.Empty, "Plomería", null, Now));
        Assert.Empty(new DomainValidationException().Errors);
        Assert.Equal("x", new DomainValidationException("x").Message);
        Assert.Empty(new DomainValidationException("x", new InvalidOperationException()).Errors);
        Assert.Equal("x", new DuplicateCategoryNameException("x").Message);
        Assert.NotNull(new DuplicateCategoryNameException("x", new InvalidOperationException()).InnerException);
        Assert.NotEmpty(new DuplicateCategoryNameException().Message);
    }
}