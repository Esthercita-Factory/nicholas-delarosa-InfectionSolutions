# InfectionSolutions

Sistema para la **venta y distribución de materiales de construcción**. Esta entrega (Semana 1) incluye
el **panel administrativo** en ASP.NET Core MVC (vistas Razor) con PostgreSQL, Identity con roles y la
gestión de productos, clientes y ventas.

| | |
|---|---|
| Stack | .NET 10 · ASP.NET Core MVC · EF Core 10 + Npgsql · ASP.NET Core Identity · Bootstrap 5 |
| Base de datos | PostgreSQL, creada **solo con migraciones** de EF Core |
| Pruebas | xUnit + NSubstitute |
| Paquetes listos para la Semana 2 | EPPlus (Excel) y QuestPDF (PDF): instalados en `Application`, aún sin uso |

## Funcionalidades

- **Autenticación con roles** (`Administrador` y `Cliente`). Solo el administrador entra al panel; si un
  usuario con rol `Cliente` intenta iniciar sesión, se le bloquea con un mensaje. Además, una política
  de autorización global exige el rol `Administrador` en todas las páginas.
- **Dashboard** con el total de productos, clientes y ventas, los ingresos del mes, las últimas ventas
  y los productos con stock bajo.
- **Productos**: CRUD con ViewModels y validaciones, búsqueda por nombre, código o categoría, filtro por
  categoría y por estado, y paginación.
- **Clientes**: CRUD con validación de documento, correo y teléfono, y búsqueda por nombre o documento.
  La **edad** se convierte con `int.Parse` dentro de un `try-catch` (`InputParser`), que captura
  `FormatException` y `OverflowException` y muestra un mensaje amigable.
- **Ventas**: registro con varias líneas, cálculo de subtotal, IVA (19 %) y total, descuento de stock
  en una transacción con `SELECT ... FOR UPDATE`, consecutivo `V-yyyyMMdd-0001` y anulación con
  devolución del stock.
- **Manejo de errores**: resultados `Result<T>` en los servicios, mensajes junto al campo del formulario,
  un filtro para las excepciones de dominio y una página de error amigable.

## Arquitectura

Arquitectura limpia en capas, basada en la estructura de
[nuz777-Firmeza](https://github.com/Esthercita-Factory/nuz777-Firmeza), con los controladores,
ViewModels y vistas del panel al estilo de [RRHH](https://github.com/jcomte23/RRHH).

```
InfectionSolutions.slnx
├── Src/
│   ├── InfectionSolutions.Domain/          Entidades, enums, roles y reglas puras (IVA, consecutivo)
│   ├── InfectionSolutions.Application/     Casos de uso (Services), DTOs, interfaces de repositorio, Result<T>
│   ├── InfectionSolutions.Infrastructure/  EF Core (DbContext, configuraciones, migraciones), repositorios, Identity
│   ├── InfectionSolutions.Admin/           Panel MVC: Controllers, ViewModels, Views, wwwroot
│   └── InfectionSolutions.Tests/           Pruebas xUnit de Domain y Application
├── docs/diagrams.md                        Modelo entidad-relación, diagrama de clases y flujo de venta
├── docker-compose.yml                      PostgreSQL + Admin (borrador)
└── dotnet-tools.json                       dotnet-ef como herramienta local
```

Flujo: `Controller → Service (Application) → Repository (Infrastructure) → ApplicationDbContext → PostgreSQL`.

- Los **controladores** trabajan con ViewModels y DTOs; nunca tocan entidades ni el `DbContext`.
- Los **servicios** son el único lugar donde una entidad se convierte en DTO. Validan las reglas de
  negocio y devuelven `Result<T>`.
- Los **repositorios** hacen las consultas, con `AsNoTracking` en los listados y `ILIKE` en las búsquedas.
- `Infrastructure` registra Identity sin esquema de autenticación: el Admin usa **cookies** y la API
  (Semana 3) agregará **JWT** sobre la misma base.

Los diagramas están en [docs/diagrams.md](docs/diagrams.md).

## Ejecución local

**Requisitos:** .NET SDK 10 y PostgreSQL 14 o superior.

1. Restaurar la herramienta de EF Core:

   ```bash
   dotnet tool restore
   ```

2. Configurar la cadena de conexión. `appsettings.json` trae `Username=postgres;Password=postgres`.
   Si tu contraseña es otra, guárdala en *user-secrets* para que no termine en el repositorio:

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
     "Host=localhost;Port=5432;Database=infection_solutions;Username=postgres;Password=TU_CLAVE" \
     --project Src/InfectionSolutions.Admin
   ```

3. Ejecutar el panel. **Al arrancar aplica las migraciones** (si la base no existe, la crea) y siembra
   los roles y el administrador:

   ```bash
   dotnet run --project Src/InfectionSolutions.Admin
   ```

   Abre http://localhost:5100 e ingresa con:

   | Usuario | Contraseña | Rol |
   |---|---|---|
   | `admin@infection.com` | `Admin123*` | Administrador |

   Puedes cambiar estas credenciales en la sección `SeedAdmin` de `appsettings.json`.

### Migraciones

```bash
# Aplicar migraciones manualmente (opcional, la app ya lo hace al iniciar)
dotnet ef database update --project Src/InfectionSolutions.Infrastructure --startup-project Src/InfectionSolutions.Admin

# Crear una migración nueva después de cambiar una entidad o configuración
dotnet ef migrations add NombreDelCambio --project Src/InfectionSolutions.Infrastructure --startup-project Src/InfectionSolutions.Admin --output-dir Persistence/Migrations
```

### Pruebas

```bash
dotnet test InfectionSolutions.slnx
```

Las pruebas cubren el cálculo de IVA y totales, el consecutivo de venta, la conversión de la edad con
`try-catch`, las reglas de `ProductService` y el flujo completo de `SaleService` (stock insuficiente,
líneas repetidas y anulación).

## Ejecución con Docker (borrador)

```bash
docker compose up --build
```

Levanta PostgreSQL (expuesto en el puerto **5433** del host) y el panel en http://localhost:8080. Las
credenciales se pueden cambiar copiando `.env.example` como `.env`. En las próximas semanas se
agregarán los servicios `tests`, `api` y `client`.

## Convenciones

- Código en inglés; textos de la interfaz, comentarios y métodos privados de apoyo en español.
- Tablas y columnas en `snake_case`, mapeadas con Fluent API en `Persistence/Configurations`.
- Dinero como `numeric(14,2)`; fechas como `timestamptz` en UTC.
- Toda acción POST lleva `[ValidateAntiForgeryToken]`; los mensajes de éxito van por `TempData`.
- Commits en español con prefijo de *conventional commits* (`feat:`, `fix:`, `docs:`, `chore:`…).
