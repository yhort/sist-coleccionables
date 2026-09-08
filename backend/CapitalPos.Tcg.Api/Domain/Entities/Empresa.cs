namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Empresa
{
    public Guid Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public DateTimeOffset FechaCreacion { get; set; }

    public ICollection<Sede> Sedes { get; set; } = new List<Sede>();
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
