using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Domain.Enums;
using InfectionSolutions.Domain.Services;
using Microsoft.Extensions.Logging;

namespace InfectionSolutions.Application.Services.BulkImport;

public interface IBulkImportService
{
    /// <summary>
    /// Importa un .xlsx desnormalizado: identifica productos, clientes y
    /// ventas, los valida, inserta o actualiza en una sola transaccion y
    /// devuelve el resumen con el log de errores e inconsistencias.
    /// </summary>
    Task<ImportResult> ImportAsync(Stream excel, string fileName, string? userId, CancellationToken cancellationToken = default);

    /// <summary>Libro de ejemplo con datos mezclados para descargar desde el panel.</summary>
    byte[] BuildTemplate();
}

/// <summary>
/// Reglas de la carga:
/// - Producto por codigo y cliente por documento: si existe se actualizan solo
///   los datos que trae el archivo; si no, se crea y debe traer los obligatorios.
/// - Las ventas son historicas: se registran con su fecha y precio y no
///   descuentan stock (el stock vigente lo da la columna Stock).
/// - Una venta con alguna linea invalida no se registra (nunca a medias).
/// - Las filas con error se reportan y se omiten; el resto se guarda.
/// </summary>
public sealed class BulkImportService : IBulkImportService
{
    private const string CategoriaPorDefecto = "General";
    private const string UnidadPorDefecto = "Unidad";
    private const string Hoja = "Importación";

    private readonly IProductRepository _products;
    private readonly ICustomerRepository _customers;
    private readonly ISaleRepository _sales;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<BulkImportService> _logger;

    public BulkImportService(
        IProductRepository products,
        ICustomerRepository customers,
        ISaleRepository sales,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<BulkImportService> logger)
    {
        _products = products;
        _customers = customers;
        _sales = sales;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(Stream excel, string fileName, string? userId, CancellationToken cancellationToken = default)
    {
        var result = new ImportResult { FileName = fileName };

        var hojas = ExcelImportReader.Read(excel, result);
        if (hojas.Count == 0)
        {
            return result;
        }

        var datos = ImportNormalizer.Normalize(hojas, result);
        if (datos.Products.Count == 0 && datos.Customers.Count == 0 && datos.SaleLines.Count == 0)
        {
            result.Warning(Hoja, null, "No se encontraron datos válidos para importar.");
            return result;
        }

        await using var transaccion = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var productos = await GuardarProductosAsync(datos, result, cancellationToken);
            var clientes = await GuardarClientesAsync(datos, result, cancellationToken);
            await GuardarVentasAsync(datos, productos, clientes, userId, result, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
            result.Saved = true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await transaccion.RollbackAsync(cancellationToken);
            _logger.LogError(exception, "Falló la importación de {Archivo}", fileName);

            result.ResetCounters();
            result.Error(Hoja, null, $"No se guardó ningún cambio: la base de datos rechazó la importación ({exception.Message}).");
        }

        return result;
    }

    public byte[] BuildTemplate() => ImportTemplate.Build(_clock.UtcNow);

    /// <summary>Productos validos por codigo, ya rastreados (existentes o nuevos).</summary>
    private async Task<Dictionary<string, Product>> GuardarProductosAsync(NormalizedImport datos, ImportResult result, CancellationToken cancellationToken)
    {
        var ahora = _clock.UtcNow;
        var existentes = (await _products.FindBySkusAsync(datos.Products.Keys.ToList(), cancellationToken)).ToDictionary(p => p.Sku);
        var listos = new Dictionary<string, Product>(existentes);

        // Precio de respaldo para productos nuevos que solo aparecen en ventas.
        // Categorias sin mayusculas ni tildes: "mamposteria" reutiliza "Mampostería".
        var categorias = new Dictionary<string, string>();
        foreach (var registrada in await _products.GetCategoriesAsync(cancellationToken))
        {
            categorias.TryAdd(FieldRules.ComparisonKey(registrada), registrada);
        }

        string? Categoria(string? valor)
        {
            if (valor is null)
            {
                return null;
            }

            var llave = FieldRules.ComparisonKey(valor);
            if (categorias.TryGetValue(llave, out var existente))
            {
                return existente;
            }

            categorias[llave] = valor;
            return valor;
        }

        var precioDeVenta = datos.SaleLines
            .Where(l => l.UnitPrice.HasValue)
            .GroupBy(l => l.Sku)
            .ToDictionary(g => g.Key, g => g.Last().UnitPrice!.Value);

        foreach (var borrador in datos.Products.Values)
        {
            var origen = borrador.Origin;

            if (existentes.TryGetValue(borrador.Sku, out var producto))
            {
                var cambio = false;
                cambio |= AsignarTexto(borrador.Name, producto.Name, v => producto.Name = v);
                cambio |= AsignarTexto(Categoria(borrador.Category), producto.Category, v => producto.Category = v);
                cambio |= AsignarTexto(borrador.Unit, producto.Unit, v => producto.Unit = v);
                cambio |= AsignarTexto(borrador.Description, producto.Description, v => producto.Description = v);
                cambio |= AsignarValor(borrador.Price, producto.Price, v => producto.Price = v);
                cambio |= AsignarValor(borrador.Stock, producto.Stock, v => producto.Stock = v);

                if (cambio)
                {
                    producto.UpdatedAt = ahora;
                    result.ProductsUpdated++;
                }

                continue;
            }

            if (borrador.Name is null)
            {
                result.Error(origen.Sheet, origen.Row, $"Producto nuevo {borrador.Sku} sin nombre: no se crea.", "Producto");
                continue;
            }

            var precio = borrador.Price;
            if (precio is null && precioDeVenta.TryGetValue(borrador.Sku, out var precioVenta))
            {
                precio = precioVenta;
                result.Warning(origen.Sheet, origen.Row, $"Producto nuevo {borrador.Sku} sin precio: se usa el precio unitario de la venta ({DisplayFormats.Money(precioVenta)}).", "Precio");
            }

            if (precio is null)
            {
                result.Error(origen.Sheet, origen.Row, $"Producto nuevo {borrador.Sku} sin precio: no se crea.", "Precio");
                continue;
            }

            if (borrador.Category is null)
            {
                result.Warning(origen.Sheet, origen.Row, $"Producto nuevo {borrador.Sku} sin categoría: se asigna '{CategoriaPorDefecto}'.", "Categoría");
            }

            if (borrador.Stock is null)
            {
                result.Warning(origen.Sheet, origen.Row, $"Producto nuevo {borrador.Sku} sin stock: se crea con 0.", "Stock");
            }

            var nuevo = new Product
            {
                Sku = borrador.Sku,
                Name = borrador.Name,
                Category = Categoria(borrador.Category) ?? Categoria(CategoriaPorDefecto)!,
                Unit = borrador.Unit ?? UnidadPorDefecto,
                Description = borrador.Description,
                Price = precio.Value,
                Stock = borrador.Stock ?? 0,
                IsActive = true,
                CreatedAt = ahora
            };

            await _products.AddAsync(nuevo, cancellationToken);
            listos[nuevo.Sku] = nuevo;
            result.ProductsCreated++;
        }

        return listos;
    }

    private async Task<Dictionary<string, Customer>> GuardarClientesAsync(NormalizedImport datos, ImportResult result, CancellationToken cancellationToken)
    {
        var ahora = _clock.UtcNow;
        var existentes = (await _customers.FindByDocumentsAsync(datos.Customers.Keys.ToList(), cancellationToken)).ToDictionary(c => c.Document);
        var listos = new Dictionary<string, Customer>(existentes);

        // email es unique: quien lo usa hoy en la base, y quien lo reclama en el archivo.
        var correos = datos.Customers.Values.Where(c => c.Email is not null).Select(c => c.Email!).Distinct().ToList();
        var duenoDelCorreo = (await _customers.FindByEmailsAsync(correos, cancellationToken)).ToDictionary(c => c.Email, c => c.Document);

        foreach (var borrador in datos.Customers.Values)
        {
            var origen = borrador.Origin;

            if (borrador.Email is not null
                && duenoDelCorreo.TryGetValue(borrador.Email, out var dueno)
                && dueno != borrador.Document)
            {
                result.Error(origen.Sheet, origen.Row, $"El correo {borrador.Email} ya pertenece al cliente {dueno}: el cliente {borrador.Document} no se guarda.", "Correo");
                continue;
            }

            if (existentes.TryGetValue(borrador.Document, out var cliente))
            {
                var cambio = false;
                cambio |= AsignarTexto(borrador.FullName, cliente.FullName, v => cliente.FullName = v);
                cambio |= AsignarValor(borrador.Age, cliente.Age, v => cliente.Age = v);
                cambio |= AsignarTexto(borrador.Email, cliente.Email, v => cliente.Email = v);
                cambio |= AsignarTexto(borrador.Phone, cliente.Phone, v => cliente.Phone = v);
                cambio |= AsignarTexto(borrador.Address, cliente.Address, v => cliente.Address = v);

                if (cambio)
                {
                    cliente.UpdatedAt = ahora;
                    result.CustomersUpdated++;
                }

                if (borrador.Email is not null)
                {
                    duenoDelCorreo[borrador.Email] = borrador.Document;
                }

                continue;
            }

            var faltantes = new List<string>();
            if (borrador.FullName is null) faltantes.Add("nombre");
            if (borrador.Email is null) faltantes.Add("correo");
            if (borrador.Phone is null) faltantes.Add("teléfono");
            if (borrador.Age is null) faltantes.Add("edad");

            if (faltantes.Count > 0)
            {
                result.Error(origen.Sheet, origen.Row, $"Cliente nuevo {borrador.Document} sin {string.Join(", ", faltantes)}: no se crea.", "Cliente");
                continue;
            }

            var nuevo = new Customer
            {
                Document = borrador.Document,
                FullName = borrador.FullName!,
                Age = borrador.Age!.Value,
                Email = borrador.Email!,
                Phone = borrador.Phone!,
                Address = borrador.Address,
                IsActive = true,
                CreatedAt = ahora
            };

            await _customers.AddAsync(nuevo, cancellationToken);
            duenoDelCorreo[nuevo.Email] = nuevo.Document;
            listos[nuevo.Document] = nuevo;
            result.CustomersCreated++;
        }

        // Clientes que el archivo solo menciona en ventas (sin otros datos) pero ya existen.
        var soloEnVentas = datos.SaleLines.Select(l => l.Document).Distinct().Where(d => !listos.ContainsKey(d) && !datos.Customers.ContainsKey(d)).ToList();
        foreach (var cliente in await _customers.FindByDocumentsAsync(soloEnVentas, cancellationToken))
        {
            listos[cliente.Document] = cliente;
        }

        return listos;
    }

    private async Task GuardarVentasAsync(
        NormalizedImport datos,
        Dictionary<string, Product> productos,
        Dictionary<string, Customer> clientes,
        string? userId,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        if (datos.SaleLines.Count == 0)
        {
            return;
        }

        // Productos que solo aparecen en ventas: pueden existir ya en la base.
        var faltantes = datos.SaleLines.Select(l => l.Sku).Distinct().Where(s => !productos.ContainsKey(s) && !datos.Products.ContainsKey(s)).ToList();
        foreach (var producto in await _products.FindBySkusAsync(faltantes, cancellationToken))
        {
            productos[producto.Sku] = producto;
        }

        var ahora = _clock.UtcNow;

        // Sin numero de venta, las lineas del mismo cliente y el mismo dia son una venta.
        var grupos = datos.SaleLines
            .GroupBy(l => l.SaleNumber ?? $"{l.Document}|{(l.Date.HasValue ? DisplayFormats.Date(l.Date.Value) : "sin-fecha")}")
            .ToList();

        var numerosDados = grupos.Select(g => g.First().SaleNumber).OfType<string>().ToList();
        var yaRegistrados = (await _sales.FindExistingNumbersAsync(numerosDados, cancellationToken)).ToHashSet();
        var consecutivosPorDia = new Dictionary<string, int>();

        foreach (var grupo in grupos)
        {
            var lineas = grupo.ToList();
            var primera = lineas[0];
            var etiqueta = primera.SaleNumber ?? $"del cliente {primera.Document} ({primera.Origin})";

            if (primera.SaleNumber is not null && yaRegistrados.Contains(primera.SaleNumber))
            {
                result.Warning(primera.Origin.Sheet, primera.Origin.Row, $"La venta {primera.SaleNumber} ya estaba registrada: se omite.", "Número de venta");
                continue;
            }

            var errores = ValidarVenta(lineas, productos, clientes);
            if (errores.Count > 0)
            {
                foreach (var (origen, mensaje) in errores)
                {
                    result.Error(origen.Sheet, origen.Row, $"Venta {etiqueta} no registrada: {mensaje}");
                }

                continue;
            }

            var fecha = lineas.Select(l => l.Date).FirstOrDefault(f => f.HasValue);
            if (fecha is null)
            {
                result.Warning(primera.Origin.Sheet, primera.Origin.Row, $"Venta {etiqueta} sin fecha: se registra con la fecha de hoy.", "Fecha");
            }

            var fechaVenta = fecha ?? ahora;
            var cliente = clientes[primera.Document];
            var venta = new Sale
            {
                Customer = cliente,
                SaleDate = fechaVenta,
                Status = SaleStatus.Completed,
                Notes = "Importada desde Excel",
                CreatedByUserId = userId
            };

            // Lineas repetidas del mismo producto y precio se suman.
            foreach (var linea in lineas.GroupBy(l => (l.Sku, Precio: l.UnitPrice ?? productos[l.Sku].Price)))
            {
                var producto = productos[linea.Key.Sku];
                var cantidad = linea.Sum(l => l.Quantity);
                venta.Details.Add(new SaleDetail
                {
                    Product = producto,
                    Quantity = cantidad,
                    UnitPrice = linea.Key.Precio,
                    LineTotal = InventoryCalculator.CalculateLineTotal(cantidad, linea.Key.Precio)
                });
            }

            var totales = InventoryCalculator.CalculateTotals(venta.Details.Select(d => (d.Quantity, d.UnitPrice)));
            venta.Subtotal = totales.Subtotal;
            venta.Tax = totales.Tax;
            venta.Total = totales.Total;
            venta.SaleNumber = primera.SaleNumber ?? await GenerarNumeroAsync(fechaVenta, consecutivosPorDia, cancellationToken);

            await _sales.AddAsync(venta, cancellationToken);
            yaRegistrados.Add(venta.SaleNumber);
            result.SalesCreated++;
        }

        if (result.SalesCreated > 0)
        {
            result.Info(Hoja, null, "Las ventas importadas son históricas: se guardan con su fecha y precio, y no descuentan stock.");
        }
    }

    private List<(ImportOrigin Origen, string Mensaje)> ValidarVenta(
        List<SaleLineDraft> lineas,
        Dictionary<string, Product> productos,
        Dictionary<string, Customer> clientes)
    {
        var errores = new List<(ImportOrigin, string)>();
        var documentos = lineas.Select(l => l.Document).Distinct().ToList();

        if (documentos.Count > 1)
        {
            errores.Add((lineas[0].Origin, $"sus líneas tienen clientes distintos ({string.Join(", ", documentos)})."));
        }
        else if (!clientes.ContainsKey(documentos[0]))
        {
            errores.Add((lineas[0].Origin, $"el cliente {documentos[0]} no existe y no se pudo crear con los datos del archivo."));
        }

        foreach (var linea in lineas.Where(l => !productos.ContainsKey(l.Sku)))
        {
            errores.Add((linea.Origin, $"el producto {linea.Sku} no existe y no se pudo crear con los datos del archivo."));
        }

        var futuras = lineas.FirstOrDefault(l => l.Date > _clock.UtcNow.AddDays(1));
        if (futuras is not null)
        {
            errores.Add((futuras.Origin, "la fecha está en el futuro."));
        }

        return errores;
    }

    /// <summary>Consecutivo del dia (hora de Colombia) contando las ventas guardadas y las de esta misma importacion.</summary>
    private async Task<string> GenerarNumeroAsync(DateTimeOffset fecha, Dictionary<string, int> consecutivosPorDia, CancellationToken cancellationToken)
    {
        var diaDeNegocio = DisplayFormats.ToBusinessTime(fecha);
        var prefijo = SaleNumberGenerator.DayPrefix(diaDeNegocio);
        if (!consecutivosPorDia.TryGetValue(prefijo, out var ultimo))
        {
            ultimo = await _sales.CountByNumberPrefixAsync(prefijo, cancellationToken);
        }

        consecutivosPorDia[prefijo] = ++ultimo;
        return SaleNumberGenerator.Generate(diaDeNegocio, ultimo);
    }

    /// <summary>Aplica el texto del archivo solo si viene y es distinto; devuelve si hubo cambio.</summary>
    private static bool AsignarTexto(string? nuevo, string? actual, Action<string> aplicar)
    {
        if (nuevo is null || nuevo == actual)
        {
            return false;
        }

        aplicar(nuevo);
        return true;
    }

    private static bool AsignarValor<T>(T? nuevo, T actual, Action<T> aplicar) where T : struct
    {
        if (nuevo is null || nuevo.Value.Equals(actual))
        {
            return false;
        }

        aplicar(nuevo.Value);
        return true;
    }
}
