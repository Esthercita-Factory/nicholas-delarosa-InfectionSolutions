using InfectionSolutions.Application.Common;

namespace InfectionSolutions.Application.Services.BulkImport;

public sealed record ImportOrigin(string Sheet, int Row)
{
    public override string ToString() => $"{Sheet}, fila {Row}";
}

/// <summary>Producto tal como lo describe el archivo; null = el archivo no trae ese dato.</summary>
public sealed class ProductDraft
{
    public required string Sku { get; init; }
    public required ImportOrigin Origin { get; init; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public int? Stock { get; set; }
}

public sealed class CustomerDraft
{
    public required string Document { get; init; }
    public required ImportOrigin Origin { get; init; }
    public string? FullName { get; set; }
    public int? Age { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public sealed record SaleLineDraft(
    ImportOrigin Origin,
    string? SaleNumber,
    DateTimeOffset? Date,
    string Document,
    string Sku,
    int Quantity,
    decimal? UnitPrice);

public sealed class NormalizedImport
{
    public Dictionary<string, ProductDraft> Products { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, CustomerDraft> Customers { get; } = new(StringComparer.Ordinal);

    public List<SaleLineDraft> SaleLines { get; } = [];
}

/// <summary>
/// Separa cada fila desnormalizada en producto, cliente y linea de venta,
/// valida el formato de cada dato y junta las filas que hablan del mismo
/// producto (por codigo) o cliente (por documento). No consulta la base.
/// </summary>
public static class ImportNormalizer
{
    private static readonly ImportField[] CamposDeProducto =
        [ImportField.Sku, ImportField.ProductName, ImportField.Category, ImportField.Unit, ImportField.Price, ImportField.Stock, ImportField.Description];

    private static readonly ImportField[] CamposDeCliente =
        [ImportField.Document, ImportField.CustomerName, ImportField.Age, ImportField.Email, ImportField.Phone, ImportField.Address];

    private static readonly ImportField[] CamposDeVenta =
        [ImportField.SaleNumber, ImportField.SaleDate, ImportField.Quantity, ImportField.UnitPrice];

    public static NormalizedImport Normalize(IReadOnlyList<RawSheet> sheets, ImportResult result)
    {
        var datos = new NormalizedImport();

        foreach (var hoja in sheets)
        {
            foreach (var fila in hoja.Rows)
            {
                result.RowsRead++;
                var origen = new ImportOrigin(hoja.Name, fila.RowNumber);

                var sku = ProcesarProducto(fila, origen, datos, result);
                var documento = ProcesarCliente(fila, origen, datos, result);
                ProcesarLineaDeVenta(fila, origen, sku, documento, datos, result);
            }
        }

        return datos;
    }

    /// <summary>Devuelve el codigo del producto de la fila (aunque sus otros datos tengan errores).</summary>
    private static string? ProcesarProducto(RawRow fila, ImportOrigin origen, NormalizedImport datos, ImportResult result)
    {
        if (!TieneAlguno(fila, CamposDeProducto))
        {
            return null;
        }

        var skuTexto = Texto(fila, ImportField.Sku);
        if (skuTexto is null)
        {
            result.Error(origen.Sheet, origen.Row, "La fila trae datos de producto pero no su código: no se puede identificar.", "Código");
            return null;
        }

        var sku = FieldRules.NormalizeSku(skuTexto);
        if (!FieldRules.IsValidSku(sku))
        {
            result.Error(origen.Sheet, origen.Row, $"Código '{skuTexto}' inválido: solo letras, números, guiones y puntos, máximo {FieldRules.SkuMaxLength}.", "Código");
            return null;
        }

        var leido = new ProductDraft { Sku = sku, Origin = origen };
        var valido = true;

        leido.Name = TextoConLimite(fila, ImportField.ProductName, FieldRules.ProductNameMaxLength, origen, result, ref valido);
        leido.Category = TextoConLimite(fila, ImportField.Category, FieldRules.CategoryMaxLength, origen, result, ref valido);
        leido.Unit = TextoConLimite(fila, ImportField.Unit, FieldRules.UnitMaxLength, origen, result, ref valido);
        leido.Description = TextoConLimite(fila, ImportField.Description, FieldRules.DescriptionMaxLength, origen, result, ref valido);

        if (fila.Values.TryGetValue(ImportField.Price, out var precio))
        {
            if (!ImportValueParser.TryDecimal(precio, out var valor, out var error))
            {
                valido = Fallar(result, origen, ImportField.Price, $"Precio de {sku}: {error}.");
            }
            else if (valor <= 0)
            {
                valido = Fallar(result, origen, ImportField.Price, $"Precio de {sku}: debe ser mayor que cero.");
            }
            else
            {
                leido.Price = valor;
            }
        }

        if (fila.Values.TryGetValue(ImportField.Stock, out var stock))
        {
            if (!ImportValueParser.TryInteger(stock, out var valor, out var error))
            {
                valido = Fallar(result, origen, ImportField.Stock, $"Stock de {sku}: {error}.");
            }
            else if (valor < 0)
            {
                valido = Fallar(result, origen, ImportField.Stock, $"Stock de {sku}: no puede ser negativo.");
            }
            else
            {
                leido.Stock = valor;
            }
        }

        // Una fila con datos mal escritos no actualiza el producto, pero el
        // codigo sigue sirviendo para la linea de venta de esa misma fila.
        if (!valido)
        {
            return sku;
        }

        if (!datos.Products.TryGetValue(sku, out var producto))
        {
            datos.Products[sku] = leido;
            return sku;
        }

        producto.Name = Fusionar(producto.Name, leido.Name, $"nombre del producto {sku}", origen, result);
        producto.Category = Fusionar(producto.Category, leido.Category, $"categoría del producto {sku}", origen, result);
        producto.Unit = Fusionar(producto.Unit, leido.Unit, $"unidad del producto {sku}", origen, result);
        producto.Description = Fusionar(producto.Description, leido.Description, $"descripción del producto {sku}", origen, result);
        producto.Price = Fusionar(producto.Price, leido.Price, $"precio del producto {sku}", origen, result);
        producto.Stock = Fusionar(producto.Stock, leido.Stock, $"stock del producto {sku}", origen, result);
        return sku;
    }

    private static string? ProcesarCliente(RawRow fila, ImportOrigin origen, NormalizedImport datos, ImportResult result)
    {
        if (!TieneAlguno(fila, CamposDeCliente))
        {
            return null;
        }

        var documentoTexto = Texto(fila, ImportField.Document);
        if (documentoTexto is null)
        {
            result.Error(origen.Sheet, origen.Row, "La fila trae datos de cliente pero no su documento: no se puede identificar.", "Documento");
            return null;
        }

        var documento = FieldRules.NormalizeDocument(documentoTexto);
        if (!FieldRules.IsValidDocument(documento))
        {
            result.Error(origen.Sheet, origen.Row,
                $"Documento '{documentoTexto}' inválido: entre {FieldRules.DocumentMinLength} y {FieldRules.DocumentMaxLength} letras o números.", "Documento");
            return null;
        }

        var leido = new CustomerDraft { Document = documento, Origin = origen };
        var valido = true;

        leido.FullName = TextoConLimite(fila, ImportField.CustomerName, FieldRules.FullNameMaxLength, origen, result, ref valido);
        leido.Address = TextoConLimite(fila, ImportField.Address, FieldRules.AddressMaxLength, origen, result, ref valido);

        if (Texto(fila, ImportField.Email) is { } correo)
        {
            if (FieldRules.IsValidEmail(correo))
            {
                leido.Email = FieldRules.NormalizeEmail(correo);
            }
            else
            {
                valido = Fallar(result, origen, ImportField.Email, $"Correo '{correo}' del cliente {documento} no tiene un formato válido.");
            }
        }

        if (Texto(fila, ImportField.Phone) is { } telefono)
        {
            if (FieldRules.IsValidPhone(telefono))
            {
                leido.Phone = telefono;
            }
            else
            {
                valido = Fallar(result, origen, ImportField.Phone, $"Teléfono '{telefono}' del cliente {documento} no es válido.");
            }
        }

        if (fila.Values.TryGetValue(ImportField.Age, out var edad))
        {
            if (!ImportValueParser.TryInteger(edad, out var valor, out var error))
            {
                valido = Fallar(result, origen, ImportField.Age, $"Edad del cliente {documento}: {error}.");
            }
            else if (valor is < InputParser.MinAge or > InputParser.MaxAge)
            {
                valido = Fallar(result, origen, ImportField.Age, $"Edad del cliente {documento}: debe estar entre {InputParser.MinAge} y {InputParser.MaxAge}.");
            }
            else
            {
                leido.Age = valor;
            }
        }

        if (!valido)
        {
            return documento;
        }

        if (!datos.Customers.TryGetValue(documento, out var cliente))
        {
            datos.Customers[documento] = leido;
            return documento;
        }

        cliente.FullName = Fusionar(cliente.FullName, leido.FullName, $"nombre del cliente {documento}", origen, result);
        cliente.Age = Fusionar(cliente.Age, leido.Age, $"edad del cliente {documento}", origen, result);
        cliente.Email = Fusionar(cliente.Email, leido.Email, $"correo del cliente {documento}", origen, result);
        cliente.Phone = Fusionar(cliente.Phone, leido.Phone, $"teléfono del cliente {documento}", origen, result);
        cliente.Address = Fusionar(cliente.Address, leido.Address, $"dirección del cliente {documento}", origen, result);
        return documento;
    }

    private static void ProcesarLineaDeVenta(RawRow fila, ImportOrigin origen, string? sku, string? documento, NormalizedImport datos, ImportResult result)
    {
        if (!TieneAlguno(fila, CamposDeVenta))
        {
            return;
        }

        if (!fila.Values.TryGetValue(ImportField.Quantity, out var cantidadCelda))
        {
            result.Warning(origen.Sheet, origen.Row, "La fila trae datos de venta pero no la cantidad: no se registra como venta.", "Cantidad");
            return;
        }

        if (!ImportValueParser.TryInteger(cantidadCelda, out var cantidad, out var errorCantidad) || cantidad <= 0)
        {
            Fallar(result, origen, ImportField.Quantity, $"Cantidad de la venta: {errorCantidad ?? "debe ser mayor que cero"}.");
            return;
        }

        if (sku is null)
        {
            result.Error(origen.Sheet, origen.Row, "Línea de venta sin código de producto: no se puede saber qué se vendió.", "Código");
            return;
        }

        if (documento is null)
        {
            result.Error(origen.Sheet, origen.Row, "Línea de venta sin documento del cliente: no se puede saber a quién se vendió.", "Documento");
            return;
        }

        decimal? precioUnitario = null;
        if (fila.Values.TryGetValue(ImportField.UnitPrice, out var precioCelda))
        {
            if (!ImportValueParser.TryDecimal(precioCelda, out var precio, out var error) || precio <= 0)
            {
                Fallar(result, origen, ImportField.UnitPrice, $"Precio unitario de la venta: {error ?? "debe ser mayor que cero"}.");
                return;
            }

            precioUnitario = precio;
        }

        DateTimeOffset? fecha = null;
        if (fila.Values.TryGetValue(ImportField.SaleDate, out var fechaCelda))
        {
            if (!ImportValueParser.TryDate(fechaCelda, out var valor, out var error))
            {
                Fallar(result, origen, ImportField.SaleDate, $"Fecha de la venta: {error}.");
                return;
            }

            fecha = valor;
        }

        var numero = Texto(fila, ImportField.SaleNumber)?.ToUpperInvariant();
        if (numero is { Length: > FieldRules.SaleNumberMaxLength })
        {
            Fallar(result, origen, ImportField.SaleNumber, $"Número de venta '{numero}' supera los {FieldRules.SaleNumberMaxLength} caracteres.");
            return;
        }

        datos.SaleLines.Add(new SaleLineDraft(origen, numero, fecha, documento, sku, cantidad, precioUnitario));
    }

    private static T? Fusionar<T>(T? actual, T? nuevo, string descripcion, ImportOrigin origen, ImportResult result)
    {
        if (nuevo is null)
        {
            return actual;
        }

        if (actual is not null && !Equals(actual, nuevo))
        {
            result.Warning(origen.Sheet, origen.Row, $"El {descripcion} cambia de '{actual}' a '{nuevo}' en filas distintas: se usa el último.");
        }

        return nuevo;
    }

    private static bool TieneAlguno(RawRow fila, ImportField[] campos) => campos.Any(fila.Values.ContainsKey);

    private static string? Texto(RawRow fila, ImportField campo)
        => fila.Values.TryGetValue(campo, out var valor) ? ImportValueParser.Text(valor) : null;

    private static string? TextoConLimite(RawRow fila, ImportField campo, int maximo, ImportOrigin origen, ImportResult result, ref bool valido)
    {
        var texto = Texto(fila, campo);
        if (texto is not null && texto.Length > maximo)
        {
            valido = Fallar(result, origen, campo, $"{ColumnAliases.Label(campo)} supera los {maximo} caracteres.");
            return null;
        }

        return texto;
    }

    /// <summary>Registra el error y devuelve false para marcar la fila como invalida.</summary>
    private static bool Fallar(ImportResult result, ImportOrigin origen, ImportField campo, string mensaje)
    {
        result.Error(origen.Sheet, origen.Row, mensaje, ColumnAliases.Label(campo));
        return false;
    }
}
