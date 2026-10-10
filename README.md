# InfectionSolutions

Sistema para la **venta y distribución de materiales de construcción**. Incluye el **panel
administrativo** en ASP.NET Core MVC (vistas Razor) con PostgreSQL, Identity con roles y la gestión de
productos, clientes y ventas (Semana 1), además de la carga masiva desde Excel, las exportaciones a
Excel/PDF y los recibos PDF de cada venta (Semana 2), una **API REST** con JWT, Swagger y correo SMTP
(Semana 3), el **portal de clientes** en Angular, donde los clientes se registran, compran y reciben el
comprobante (Semana 4), y el **despliegue con Docker** que corre las pruebas antes de levantar todo (Semana 5).

| | |
|---|---|
| Stack | .NET 10 · ASP.NET Core MVC y Web API · EF Core 10 + Npgsql · ASP.NET Core Identity · Bootstrap 5 |
| API | JWT Bearer · AutoMapper · Swagger (Swashbuckle) · MailKit (SMTP de Gmail) |
| Base de datos | PostgreSQL, creada **solo con migraciones** de EF Core |
| Pruebas | xUnit + NSubstitute |
| Documentos | EPPlus (lectura y escritura de Excel) y QuestPDF (recibos y reportes PDF) |

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

### Semana 2: Excel y PDF

- **Carga masiva desde Excel** (*Importar Excel* en el menú): acepta archivos `.xlsx` **desnormalizados**,
  es decir, con productos, clientes y ventas mezclados en las mismas filas y en varias hojas.
  - Busca la fila de encabezados de cada hoja (puede haber título y fecha antes) y reconoce las columnas
    por sinónimos: *Código/Ref/SKU*, *Cédula/NIT/Documento*, *Valor/Precio*, *Cant/Cantidad*… "Nombre" a
    secas se asigna al producto o al cliente según las demás columnas.
  - **Normaliza en memoria**: cada fila se separa en producto (por código), cliente (por documento) y
    línea de venta (si trae cantidad). Las filas que repiten el mismo producto o cliente se fusionan, y si
    un dato cambia entre filas se avisa.
  - **Valida** formatos (números como `$32.500` o `1.250,50`, fechas `dd/mm/aaaa`, correo, teléfono, edad)
    y los datos obligatorios para crear: producto (código, nombre, precio) y cliente (documento, nombre,
    correo, teléfono, edad).
  - **Inserta o actualiza**: si el producto o el cliente existe, cambia solo los datos que trae el
    archivo. Las líneas con la misma factura forman una venta; sin número, se agrupan por cliente y día.
    Una venta con alguna línea inválida no se registra a medias, y las facturas ya registradas se omiten.
    Las ventas importadas son **históricas**: guardan su fecha y precio, y no descuentan stock.
  - Todo se guarda en **una transacción**. Las filas con error se omiten y quedan en un **log** (nivel,
    hoja, fila, campo y detalle), que se ve en pantalla con filtros y se descarga como CSV.
  - Desde la misma pantalla se puede descargar una **plantilla de ejemplo** con datos mezclados.
- **Exportaciones**: Productos, Clientes y Ventas tienen un botón *Exportar* a **Excel** (tabla con
  filtros, formatos de moneda y fecha) o **PDF** (reporte paginado), con los filtros aplicados en el
  listado. Un Excel exportado se puede volver a importar.
- **Recibo PDF de cada venta**: al registrar una venta se genera el recibo con los datos de la empresa y
  del cliente, la fecha y el número de venta, los productos, el subtotal, el IVA y el total. Se guarda en
  `wwwroot/recibos/<número>.pdf` y se ve o se descarga desde el detalle y el listado de ventas. Al anular
  la venta, el recibo se regenera con la marca **ANULADA**. Para las ventas importadas, el recibo se genera
  la primera vez que se pide.
- Los datos de la empresa que aparecen en los documentos se configuran en la sección `Company` de
  `appsettings.json`.

### Semana 3: API REST

La guía para consumirla, con ejemplos, está en [docs/api.md](docs/api.md).

- **Proyecto `InfectionSolutions.Api`** en la misma solución. Se conecta a la **misma base de datos** que
  el panel y reutiliza sus servicios, así que las reglas (stock, IVA, duplicados, búsquedas sin tildes)
  son idénticas.
- **Identity + JWT**: `POST /api/auth/login` emite el token para administradores y clientes. El rol
  **Cliente** se usa en el registro público (`POST /api/auth/register`), que crea la cuenta y la ficha de
  cliente. Las políticas `AdminOnly` y `CustomerOnly` separan lo que puede hacer cada rol, y todo lo demás
  exige un token válido.
- **DTOs y AutoMapper**: los perfiles `ProductProfile`, `CustomerProfile` y `SaleProfile` (en
  `Application/Mappings`) convierten las entidades en DTOs, también para el panel. En la API,
  `ApiMappingProfile` convierte los ViewModels de entrada (con DataAnnotations) en DTOs.
- **Controladores REST**: `Products` y `Customers` con GET, POST, PUT y DELETE. `Sales` permite listar,
  ver, registrar y anular: el DELETE anula la venta, porque las ventas no se borran. `MySales` tiene las
  compras del cliente autenticado y la descarga de su comprobante. Todos responden DTOs, y los errores
  salen como ProblemDetails con el campo que falló.
- **Swagger** con los comentarios XML de cada endpoint y el botón *Authorize* para el JWT.
- **Correo SMTP real** (Gmail con contraseña de aplicación) detrás de la interfaz `IEmailSender`. Se envía
  la bienvenida al registrarse y el **comprobante PDF adjunto** después de cada compra. Pasar a un SMTP
  empresarial es solo cambiar la configuración. Si el correo falla, la compra no se deshace y la respuesta
  lo indica (`receiptEmailSent`).

### Semana 4: portal de clientes (Angular)

El portal de clientes es un **repositorio aparte**, [InfectionSolutions.Client](https://github.com/Esthercita-Factory/nicholas-delarosa-InfectionSolutions.Client) (Angular 21), con su propia
imagen. Consume esta API: registro, inicio de sesión con JWT, catálogo, carrito, compra con comprobante por
correo y *Mis compras*. Solo usa endpoints del rol Cliente (`/api/auth/*`, `GET /api/products*` y
`/api/me/sales/*`); la API ya permite su origen (`http://localhost:4200`) en `Cors:AllowedOrigins`.

## Arquitectura

Arquitectura limpia en capas: el dominio no depende de nada, los casos de uso viven en Application y la
persistencia, la seguridad y el correo se conectan desde Infrastructure. Así el panel MVC y la API
comparten las mismas reglas de negocio sin duplicarlas.

```
InfectionSolutions.slnx
├── Src/
│   ├── InfectionSolutions.Domain/          Entidades, enums, roles y reglas puras (IVA, consecutivo)
│   ├── InfectionSolutions.Application/     Casos de uso, DTOs, interfaces, Result<T>, importación/exportación/recibos
│   ├── InfectionSolutions.Infrastructure/  EF Core (DbContext, configuraciones, migraciones), repositorios, Identity, recibos en disco
│   ├── InfectionSolutions.Admin/           Panel MVC: Controllers, ViewModels, Views, wwwroot
│   ├── InfectionSolutions.Api/             API REST: Controllers, ViewModels, Mappings (AutoMapper), JWT, Swagger
│   └── InfectionSolutions.Tests/           Pruebas xUnit (y su Dockerfile: la puerta del despliegue)
├── docs/diagrams.md                        Modelo entidad-relación, diagrama de clases y flujo de venta
├── docs/api.md                             Cómo consumir la API (autenticación, endpoints, errores, correo)
├── docs/despliegue.md                      Docker, GitHub Actions y despliegue en el servidor
├── docker-compose.yml                      tests → PostgreSQL + API + Admin + portal de clientes
├── .github/workflows/docker.yml            Pruebas y publicación de imágenes en GHCR
└── dotnet-tools.json                       dotnet-ef como herramienta local
```

Flujo: `Controller → Service (Application) → Repository (Infrastructure) → ApplicationDbContext → PostgreSQL`.

- Los **controladores** trabajan con ViewModels y DTOs; nunca tocan entidades ni el `DbContext`.
- Los **servicios** son el único lugar donde una entidad se convierte en DTO (con los perfiles de
  AutoMapper). Validan las reglas de negocio y devuelven `Result<T>`.
- Los **repositorios** hacen las consultas, con `AsNoTracking` en los listados y `ILIKE` en las búsquedas.
- `Infrastructure` registra Identity sin esquema de autenticación: el Admin usa **cookies** y la API usa
  **JWT** sobre la misma base. También implementa el envío de correo (`SmtpEmailSender`, MailKit) y la
  emisión de tokens (`JwtTokenService`).

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

4. Ejecutar la API (puede correr a la vez que el panel). Usa la misma cadena de conexión, así que hay
   que guardarla también en sus *user-secrets* (`--project Src/InfectionSolutions.Api`). Para que los
   correos salgan de verdad, configura las credenciales de Gmail como se explica en
   [docs/api.md](docs/api.md#correo-con-gmail).

   ```bash
   dotnet run --project Src/InfectionSolutions.Api
   ```

   Swagger queda en http://localhost:5120/swagger.

5. El portal de clientes (Angular) está en su propio repositorio:
   [InfectionSolutions.Client](https://github.com/Esthercita-Factory/nicholas-delarosa-InfectionSolutions.Client). Con esta API corriendo, se levanta con
   `npm install` y `npm start` en http://localhost:4200.

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

Las pruebas cubren:

- El cálculo de IVA y totales, el consecutivo de venta y la conversión de la edad con `try-catch`.
- Las reglas de `ProductService` y el flujo de `SaleService`: stock insuficiente, líneas repetidas,
  anulación y generación del recibo.
- La carga masiva: formatos de números y fechas, sinónimos de columnas, fusión de filas, validaciones e
  importación completa de un `.xlsx` en memoria.
- Los recibos PDF y las exportaciones, incluido exportar un Excel y volver a importarlo.
- La API: el registro y el login (`AuthService`), las compras del portal (solo las propias, catálogo de
  activos), el correo con el comprobante adjunto (y que un fallo de SMTP no rompa la compra), los perfiles
  de AutoMapper, la firma y los claims del JWT, y los códigos HTTP de `ProductsController`.

## Ejecución con Docker (Semana 5)

```bash
docker compose up --build
```

Un solo comando, sin abrir ningún IDE:

1. **`tests`** compila la solución y corre las pruebas xUnit dentro de un contenedor. Si alguna falla, el
   contenedor sale con código distinto de 0 y compose **no levanta nada más**: todos los servicios dependen
   de él con `condition: service_completed_successfully`.
2. Si pasan, arranca **PostgreSQL** y, cuando acepta conexiones, la **API** y el **panel** (las migraciones se
   aplican solas, de a una gracias a un candado de PostgreSQL), y después el **portal de clientes**.

| Servicio | Dirección | Imagen |
|---|---|---|
| Panel administrativo | http://localhost:8080 (`admin@infection.com` / `Admin123*`) | `...-infectionsolutions-admin` |
| API (Swagger) | http://localhost:8081/swagger | `...-infectionsolutions-api` |
| Portal de clientes | http://localhost:8082 | `...-infectionsolutions.client` |

Backend y frontend son imágenes separadas. El portal se construye desde
[su repositorio](https://github.com/Esthercita-Factory/nicholas-delarosa-InfectionSolutions.Client), clonado al
lado de este (`../InfectionSolutions.Client`), y su
Nginx reenvía `/api` a la API, así que el navegador ve un solo origen. La base de datos no se publica fuera
de Docker. Puertos y secretos (contraseña de la base, clave JWT, Gmail) se configuran copiando
`.env.example` como `.env`. Base, recibos, logs de importación y sesiones del panel viven en volúmenes.

Cada push a `main` ejecuta GitHub Actions (`.github/workflows/docker.yml`): corre las pruebas y, solo si
pasan, publica las imágenes en GitHub Container Registry. El despliegue en el servidor (`docker compose
pull` + `docker compose up -d`) está en [docs/despliegue.md](docs/despliegue.md).

## Convenciones

- Código en inglés; textos de la interfaz, comentarios y métodos privados de apoyo en español.
- Tablas y columnas en `snake_case`, mapeadas con Fluent API en `Persistence/Configurations`.
- Dinero como `numeric(14,2)`; fechas como `timestamptz` en UTC.
- Toda acción POST lleva `[ValidateAntiForgeryToken]`; los mensajes de éxito van por `TempData`.
- Commits en español con prefijo de *conventional commits* (`feat:`, `fix:`, `docs:`, `chore:`…).
