using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Domain.Enums;
using InfectionSolutions.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfectionSolutions.Infrastructure.Persistence;

/// <summary>
/// Datos de demostracion para que el panel no arranque vacio: productos,
/// clientes y ventas de las ultimas semanas. Solo corre si todavia no hay
/// clientes ni ventas (base recien creada), y no toca productos ya existentes
/// con el mismo codigo. Se activa con SeedDemoData=true.
/// </summary>
public static class DemoDataSeeder
{
    private const int DiasDeHistoria = 20;

    private static readonly (string Sku, string Nombre, string Categoria, string Unidad, decimal Precio, int Stock, string Descripcion)[] Productos =
    [
        ("CEM-ARG-50", "Cemento gris uso general 50 kg", "Cementos", "Bulto", 32_500m, 640, "Cemento Portland tipo UG para obra gris."),
        ("CEM-BLA-25", "Cemento blanco 25 kg", "Cementos", "Bulto", 41_900m, 220, "Para acabados, boquillas y prefabricados."),
        ("VAR-12-6M", "Varilla corrugada 1/2\" de 6 m", "Aceros", "Unidad", 15_500.50m, 900, "Acero de refuerzo grado 60."),
        ("MAL-ELE-15", "Malla electrosoldada 15×15 cm", "Aceros", "Unidad", 186_000m, 120, "Panel de 6 × 2,35 m, calibre 4 mm."),
        ("ARE-RIO-M3", "Arena de río lavada", "Agregados", "Metro cúbico", 68_000m, 240, "Arena fina para pañete y concreto."),
        ("GRA-34-M3", "Gravilla triturada 3/4\"", "Agregados", "Metro cúbico", 79_000m, 210, "Agregado grueso para concreto estructural."),
        ("LAD-TOL", "Ladrillo tolete común", "Mampostería", "Unidad", 950m, 18_000, "Ladrillo macizo de arcilla cocida."),
        ("BLO-N5", "Bloque de arcilla N.º 5", "Mampostería", "Unidad", 1_480m, 9_500, "Bloque estructural para muros divisorios."),
        ("TEJ-FIB-6", "Teja de fibrocemento N.º 6", "Cubiertas", "Unidad", 54_900m, 9, "Teja ondulada de 1,83 m."),
        ("TUB-PVC-4", "Tubo PVC sanitario 4\" × 6 m", "Hidráulicos", "Unidad", 63_400m, 300, "Para desagües y bajantes."),
        ("PIN-VIN-GL", "Pintura vinílica blanca", "Pinturas", "Galón", 52_000m, 6, "Tipo 1, interior y exterior."),
        ("IMP-ACR-GL", "Impermeabilizante acrílico", "Pinturas", "Galón", 89_900m, 0, "Para cubiertas y terrazas.")
    ];

    private static readonly (string Documento, string Nombre, int Edad, string Correo, string Telefono, string Direccion)[] Clientes =
    [
        ("1020458796", "Laura Mendoza Pérez", 35, "laura.mendoza@correo.com", "300 123 4567", "Calle 100 # 15-20, Bogotá"),
        ("900123456", "Constructora Andina SAS", 30, "compras@andina.com.co", "601 745 9000", "Av. El Dorado # 68-20, Bogotá"),
        ("52345678", "Pedro José Ramírez", 41, "pedro.ramirez@correo.com", "310 999 8877", "Carrera 7 # 45-10, Bogotá"),
        ("79811223", "Obras Civiles del Norte Ltda.", 38, "obras@civilesnorte.co", "320 555 1122", "Calle 170 # 54-30, Bogotá"),
        ("1015778899", "María Fernanda Gómez", 29, "mafe.gomez@correo.com", "315 222 3344", "Carrera 15 # 85-40, Bogotá"),
        ("80456123", "Jesús Antonio Peña", 56, "jesus.pena@correo.com", "312 456 7788", "Calle 13 # 30-15, Soacha"),
        ("901555888", "Ingeniería y Acabados Ñandú SAS", 32, "proyectos@nandu.com.co", "601 321 4455", "Zona Franca, Bogotá"),
        ("1032456987", "Andrés Felipe Martínez", 27, "andres.martinez@correo.com", "301 777 8899", "Calle 26 # 69-76, Bogotá"),
        ("39654321", "Rosa Elvira Cárdenas", 63, "rosa.cardenas@correo.com", "317 888 9900", "Carrera 50 # 12-34, Funza"),
        ("1098765432", "Sebastián Rodríguez", 24, "sebas.rodriguez@correo.com", "318 111 2233", "Calle 80 # 20-10, Bogotá"),
        ("830987654", "Ferretería El Maestro", 45, "ventas@elmaestro.co", "601 654 3210", "Carrera 30 # 6-50, Bogotá"),
        ("1023987456", "Valentina Ospina Núñez", 31, "vale.ospina@correo.com", "304 567 8901", "Calle 53 # 24-60, Bogotá")
    ];

    public static async Task SeedAsync(ApplicationDbContext db, IClock clock, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await db.Customers.AnyAsync(cancellationToken) || await db.Sales.AnyAsync(cancellationToken))
        {
            return;
        }

        var ahora = clock.UtcNow;

        // Productos: los codigos que ya existen se reutilizan tal como estan.
        var skus = Productos.Select(p => p.Sku).ToList();
        var existentes = await db.Products.Where(p => skus.Contains(p.Sku)).ToDictionaryAsync(p => p.Sku, cancellationToken);
        var productos = new List<Product>();
        foreach (var p in Productos)
        {
            if (!existentes.TryGetValue(p.Sku, out var producto))
            {
                producto = new Product
                {
                    Sku = p.Sku,
                    Name = p.Nombre,
                    Category = p.Categoria,
                    Unit = p.Unidad,
                    Price = p.Precio,
                    Stock = p.Stock,
                    Description = p.Descripcion,
                    IsActive = true,
                    CreatedAt = ahora.AddDays(-DiasDeHistoria - 5)
                };
                db.Products.Add(producto);
            }

            productos.Add(producto);
        }

        var clientes = Clientes.Select(c => new Customer
        {
            Document = c.Documento,
            FullName = c.Nombre,
            Age = c.Edad,
            Email = c.Correo,
            Phone = c.Telefono,
            Address = c.Direccion,
            IsActive = true,
            CreatedAt = ahora.AddDays(-DiasDeHistoria - 3)
        }).ToList();
        db.Customers.AddRange(clientes);

        // Ventas deterministas (misma semilla, mismos datos) repartidas en los ultimos dias.
        var azar = new Random(2026);
        var hoy = DisplayFormats.ToBusinessTime(ahora).Date;
        var consecutivos = new Dictionary<DateTime, int>();
        var vendibles = productos.Where(p => p.Stock > 0).ToList();
        var ventas = 0;

        for (var dia = DiasDeHistoria; dia >= 0; dia--)
        {
            var fecha = hoy.AddDays(-dia);
            if (fecha.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            // Uno o dos despachos por dia laboral: una historia continua para la grafica.
            var ventasDelDia = dia == 0 ? 2 : azar.Next(1, 3);
            for (var v = 0; v < ventasDelDia; v++)
            {
                var hora = new DateTimeOffset(fecha.AddHours(8 + azar.Next(0, 10)).AddMinutes(azar.Next(0, 60)), DisplayFormats.BusinessOffset);
                if (hora > ahora)
                {
                    hora = ahora.ToOffset(DisplayFormats.BusinessOffset).AddMinutes(-5 * (v + 1));
                }

                var venta = new Sale
                {
                    Customer = clientes[azar.Next(clientes.Count)],
                    SaleDate = hora.ToUniversalTime(),
                    Status = SaleStatus.Completed,
                    Notes = azar.Next(4) == 0 ? "Entrega en obra" : null
                };

                foreach (var producto in vendibles.OrderBy(_ => azar.Next()).Take(azar.Next(1, 4)))
                {
                    // Ladrillo y bloque se venden por cientos; lo demas por pocas unidades.
                    var tope = producto.Price < 5_000m ? 1_200 : 12;
                    var cantidad = Math.Min(azar.Next(1, tope), producto.Stock);
                    if (cantidad <= 0)
                    {
                        continue;
                    }

                    producto.Stock -= cantidad;
                    venta.Details.Add(new SaleDetail
                    {
                        Product = producto,
                        Quantity = cantidad,
                        UnitPrice = producto.Price,
                        LineTotal = InventoryCalculator.CalculateLineTotal(cantidad, producto.Price)
                    });
                }

                if (venta.Details.Count == 0)
                {
                    continue;
                }

                var totales = InventoryCalculator.CalculateTotals(venta.Details.Select(d => (d.Quantity, d.UnitPrice)));
                venta.Subtotal = totales.Subtotal;
                venta.Tax = totales.Tax;
                venta.Total = totales.Total;

                consecutivos[fecha] = consecutivos.GetValueOrDefault(fecha) + 1;
                venta.SaleNumber = SaleNumberGenerator.Generate(hora, consecutivos[fecha]);

                db.Sales.Add(venta);
                ventas++;
            }
        }

        // Una venta anulada para ver ese estado en el panel; su stock vuelve al inventario.
        var anulada = db.ChangeTracker.Entries<Sale>().Select(e => e.Entity).OrderBy(s => s.SaleDate).Skip(3).FirstOrDefault();
        if (anulada is not null)
        {
            anulada.Status = SaleStatus.Cancelled;
            anulada.CancelledAt = anulada.SaleDate.AddHours(2);
            foreach (var detalle in anulada.Details)
            {
                detalle.Product.Stock += detalle.Quantity;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Datos de demostración: {Productos} productos, {Clientes} clientes y {Ventas} ventas.",
            productos.Count - existentes.Count, clientes.Count, ventas);
    }
}
