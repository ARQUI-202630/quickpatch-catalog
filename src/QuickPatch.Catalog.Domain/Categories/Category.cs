using QuickPatch.Catalog.Domain.Common;

namespace QuickPatch.Catalog.Domain.Categories;

/// <summary>Categoría de servicio de un tenant (DD, tabla <c>service_categories</c>).</summary>
public sealed class Category
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 255;

    private Category(Guid id, Guid tenantId, string name, string? description, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Description = description;
        Active = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid TenantId { get; }

    public string Name { get; }

    public string? Description { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Momento del último cambio. Viaja como <c>data.updatedAt</c> en <c>catalog.category-changed</c> y define
    /// qué versión es la más reciente en las réplicas.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Nombre normalizado para comparar la unicidad por tenant sin distinguir mayúsculas.</summary>
    public static string NormalizeName(string? name) => (name ?? string.Empty).Trim().ToLowerInvariant();

    public static Category Create(Guid tenantId, string? name, string? description, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();
        var nombre = name?.Trim() ?? string.Empty;
        if (nombre.Length is < NameMinLength or > NameMaxLength)
        {
            errors["name"] = [$"El nombre debe tener entre {NameMinLength} y {NameMaxLength} caracteres."];
        }

        var descripcion = NormalizeDescription(description, errors);
        if (tenantId == Guid.Empty)
        {
            errors["tenantId"] = ["El tenant es obligatorio."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new Category(Guid.CreateVersion7(now), tenantId, nombre, descripcion, now);
    }

    /// <summary>
    /// Aplica un cambio parcial. <paramref name="description"/> nulo no cambia la descripción y vacío la borra.
    /// </summary>
    /// <returns><c>true</c> si algo cambió (y hay que publicar el evento).</returns>
    public bool Update(string? description, bool? active, DateTimeOffset now)
    {
        if (description is null && active is null)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["body"] = ["Envía al menos 'description' o 'active'."],
            });
        }

        var errors = new Dictionary<string, string[]>();
        var nuevaDescripcion = description is null ? Description : NormalizeDescription(description, errors);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        var nuevoEstado = active ?? Active;
        if (nuevaDescripcion == Description && nuevoEstado == Active)
        {
            return false;
        }

        Description = nuevaDescripcion;
        Active = nuevoEstado;
        UpdatedAt = now > UpdatedAt ? now : UpdatedAt.AddTicks(1);
        return true;
    }

    private static string? NormalizeDescription(string? description, Dictionary<string, string[]> errors)
    {
        var descripcion = description?.Trim();
        if (descripcion is { Length: > DescriptionMaxLength })
        {
            errors["description"] = [$"La descripción admite hasta {DescriptionMaxLength} caracteres."];
        }

        return string.IsNullOrEmpty(descripcion) ? null : descripcion;
    }
}