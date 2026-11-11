using QuickPatch.Catalog.Api.Http;
using QuickPatch.Catalog.Api.Security;
using QuickPatch.Catalog.Application.Categories;
using QuickPatch.Catalog.Domain.Common;
using QuickPatch.Transversal.Http;

namespace QuickPatch.Catalog.Api.Endpoints;

/// <summary>Endpoints del contrato <c>catalog.v1.yaml</c> 1.2.0.</summary>
public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        // Con o sin token (contrato 1.2.0): sin token, la app del registro de técnicos ve las del tenant del canal.
        app.MapGet("/v1/catalog/categories", ListAsync).WithName("listCategories").AllowAnonymous();

        var admin = app.MapGroup("/v1/catalog/admin/categories").RequireAuthorization(AuthenticatedUser.AdminTenantPolicy);
        admin.MapPost("/", CreateAsync).WithName("createCategory");
        admin.MapPatch("/{id:guid}", UpdateAsync).WithName("updateCategory");
        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        ListCategoriesHandler handler,
        RequestTenant requestTenant,
        CancellationToken cancellationToken)
    {
        var tenant = requestTenant.Resolve(context);
        return tenant.Outcome switch
        {
            RequestTenant.Result.InvalidToken => Unauthorized(),
            RequestTenant.Result.ChannelWithoutTenant => Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable, title: "El canal no tiene tenant configurado."),
            _ => Results.Ok((await handler.HandleAsync(tenant.TenantId, cancellationToken)).Select(CategoryResponse.From)),
        };
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