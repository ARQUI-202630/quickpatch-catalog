using QuickPatch.Catalog.Api.Http;
using QuickPatch.Catalog.Api.Security;
using QuickPatch.Catalog.Application.Categories;
using QuickPatch.Catalog.Domain.Categories;
using QuickPatch.Catalog.Domain.Common;

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

/// <summary>Endpoints del contrato <c>catalog.v1.yaml</c> 1.1.0.</summary>
public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/catalog/categories", ListAsync).WithName("listCategories").RequireAuthorization();

        var admin = app.MapGroup("/v1/catalog/admin/categories").RequireAuthorization(AuthenticatedUser.AdminTenantPolicy);
        admin.MapPost("/", CreateAsync).WithName("createCategory");
        admin.MapPatch("/{id:guid}", UpdateAsync).WithName("updateCategory");
        return app;
    }

    private static async Task<IResult> ListAsync(HttpContext context, ListCategoriesHandler handler, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryFrom(context.User, out var user))
        {
            return Unauthorized();
        }

        var categories = await handler.HandleAsync(user.TenantId, cancellationToken);
        return Results.Ok(categories.Select(CategoryResponse.From));
    }

    private static async Task<IResult> CreateAsync(
        CreateCategoryRequest body,
        HttpContext context,
        CreateCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryFrom(context.User, out var user))
        {
            return Unauthorized();
        }

        try
        {
            var result = await handler.HandleAsync(
                new CreateCategoryCommand(user.TenantId, CorrelationId.Get(context), body.Name, body.Description), cancellationToken);
            return result switch
            {
                CreateCategoryResult.Created created => Results.Json(AdminCategoryResponse.From(created.Category), statusCode: StatusCodes.Status201Created),
                _ => Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    type: Problems.CategoryNameTaken,
                    title: "Ya existe una categoría con ese nombre"),
            };
        }
        catch (DomainValidationException ex)
        {
            return ValidationProblem(ex.Errors);
        }
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCategoryRequest body,
        HttpContext context,
        UpdateCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryFrom(context.User, out var user))
        {
            return Unauthorized();
        }

        try
        {
            var category = await handler.HandleAsync(
                new UpdateCategoryCommand(user.TenantId, CorrelationId.Get(context), id, body.Description, body.Active), cancellationToken);
            return category is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "La categoría no existe.")
                : Results.Ok(AdminCategoryResponse.From(category));
        }
        catch (DomainValidationException ex)
        {
            return ValidationProblem(ex.Errors);
        }
    }

    private static IResult Unauthorized() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "El token no trae usuario ni tenant válidos.");

    private static IResult ValidationProblem(IReadOnlyDictionary<string, string[]> errors) =>
        Results.ValidationProblem(
            errors.ToDictionary(e => e.Key, e => e.Value),
            title: "La solicitud tiene datos inválidos",
            type: Problems.Validation);
}