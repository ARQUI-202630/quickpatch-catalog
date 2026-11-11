using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Api.Endpoints;

/// <summary>Esquema <c>Category</c> del contrato (listado público del tenant).</summary>
public sealed record CategoryResponse(Guid Id, string Name, string? Description)
{
    public static CategoryResponse From(Category c) => new(c.Id, c.Name, c.Description);
}

/// <summary>Esquema <c>AdminCategory</c> del contrato.</summary>
public sealed record AdminCategoryResponse(Guid Id, string Name, string? Description, bool Active, DateTimeOffset UpdatedAt)
{
    public static AdminCategoryResponse From(Category c) => new(c.Id, c.Name, c.Description, c.Active, c.UpdatedAt);
}

public sealed record CreateCategoryRequest(string? Name, string? Description);

public sealed record UpdateCategoryRequest(string? Description, bool? Active);