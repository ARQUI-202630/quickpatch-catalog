namespace QuickPatch.Catalog.Application.Abstractions;

/// <summary>El nombre ya existe en el tenant (unicidad de DD 5.4 en la base de datos).</summary>
public sealed class DuplicateCategoryNameException : Exception
{
    public DuplicateCategoryNameException()
        : base("Ya existe una categoría con ese nombre en el tenant.")
    {
    }

    public DuplicateCategoryNameException(string message)
        : base(message)
    {
    }

    public DuplicateCategoryNameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}