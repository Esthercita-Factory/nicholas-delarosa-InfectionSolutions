namespace InfectionSolutions.Application.Services.BulkImport;

public enum ImportEntity
{
    Product,
    Customer,
    Sale
}

/// <summary>Cada columna reconocida del Excel termina en uno de estos campos.</summary>
public enum ImportField
{
    Sku,
    ProductName,
    Category,
    Unit,
    Price,
    Stock,
    Description,

    Document,
    CustomerName,
    Age,
    Email,
    Phone,
    Address,

    SaleNumber,
    SaleDate,
    Quantity,
    UnitPrice
}

public static class ColumnAliases
{
    /// <summary>
    /// Sinonimos ya normalizados (minusculas, sin tildes, sin espacios ni
    /// simbolos): "Código (SKU)" llega como "codigosku".
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ImportField> Map = Construir(new()
    {
        [ImportField.Sku] = ["sku", "codigo", "cod", "codigosku", "codigoproducto", "codproducto", "codigodelproducto", "referencia", "ref", "refproducto", "idproducto"],
        [ImportField.ProductName] = ["producto", "nombreproducto", "nombredelproducto", "articulo", "material", "item", "descripcionproducto"],
        [ImportField.Category] = ["categoria", "categoriaproducto", "rubro", "linea", "familia", "grupo", "tipoproducto"],
        [ImportField.Unit] = ["unidad", "um", "unidadmedida", "unidaddemedida", "unidadventa", "unidaddeventa", "presentacion"],
        [ImportField.Price] = ["precio", "precioproducto", "preciolista", "preciosiniva", "valor", "precioventapublico"],
        [ImportField.Stock] = ["stock", "existencia", "existencias", "inventario", "stockactual", "disponible"],
        [ImportField.Description] = ["descripcion", "detalle", "detalleproducto"],

        [ImportField.Document] = ["documento", "doc", "cedula", "cc", "nit", "dni", "identificacion", "numerodocumento", "documentocliente", "cedulacliente", "nitcliente", "idcliente"],
        [ImportField.CustomerName] = ["cliente", "nombrecliente", "nombredelcliente", "razonsocial", "comprador", "nombrecompleto"],
        [ImportField.Age] = ["edad", "edadcliente"],
        [ImportField.Email] = ["email", "correo", "correoelectronico", "mail", "emailcliente", "correocliente"],
        [ImportField.Phone] = ["telefono", "tel", "celular", "movil", "telefonocliente", "celularcliente"],
        [ImportField.Address] = ["direccion", "domicilio", "direccioncliente"],

        [ImportField.SaleNumber] = ["numeroventa", "nroventa", "noventa", "venta", "factura", "nfactura", "nrofactura", "numfactura", "numerofactura", "nofactura"],
        [ImportField.SaleDate] = ["fecha", "fechaventa", "fechafactura", "fechaemision", "fechadeventa"],
        [ImportField.Quantity] = ["cantidad", "cant", "cantidadvendida", "unidadesvendidas", "qty"],
        [ImportField.UnitPrice] = ["preciounitario", "preciounit", "precioventa", "valorunitario"]
    });

    public static ImportEntity EntityOf(ImportField field) => field switch
    {
        <= ImportField.Description => ImportEntity.Product,
        <= ImportField.Address => ImportEntity.Customer,
        _ => ImportEntity.Sale
    };

    /// <summary>Nombre amigable del campo para el log.</summary>
    public static string Label(ImportField field) => field switch
    {
        ImportField.Sku => "Código",
        ImportField.ProductName => "Producto",
        ImportField.Category => "Categoría",
        ImportField.Unit => "Unidad",
        ImportField.Price => "Precio",
        ImportField.Stock => "Stock",
        ImportField.Description => "Descripción",
        ImportField.Document => "Documento",
        ImportField.CustomerName => "Cliente",
        ImportField.Age => "Edad",
        ImportField.Email => "Correo",
        ImportField.Phone => "Teléfono",
        ImportField.Address => "Dirección",
        ImportField.SaleNumber => "Número de venta",
        ImportField.SaleDate => "Fecha",
        ImportField.Quantity => "Cantidad",
        ImportField.UnitPrice => "Precio unitario",
        _ => field.ToString()
    };

    private static Dictionary<string, ImportField> Construir(Dictionary<ImportField, string[]> porCampo)
    {
        var mapa = new Dictionary<string, ImportField>(StringComparer.Ordinal);
        foreach (var (campo, alias) in porCampo)
        {
            foreach (var nombre in alias)
            {
                mapa.Add(nombre, campo);
            }
        }

        return mapa;
    }
}
