# Cómo consumir la API

API REST de InfectionSolutions (`Src/InfectionSolutions.Api`). Usa la misma base de datos y las mismas
reglas de negocio que el panel administrativo, y autentica con **JWT**.

- Local: http://localhost:5120. Swagger UI en http://localhost:5120/swagger (la raíz redirige allí).
- Docker: http://localhost:8081/swagger.
- Salud: `GET /health` responde `{"status":"ok"}` si hay conexión con la base.

## Autenticación

1. `POST /api/auth/login` con correo y contraseña.
2. Copia el `accessToken` de la respuesta.
3. Envíalo en cada petición como `Authorization: Bearer <token>`. En Swagger, pulsa **Authorize** y pega
   solo el token.

El token dura 120 minutos (`Jwt:AccessTokenMinutes`). Cuando vence, la API responde **401** y el cliente
debe volver a iniciar sesión.

El token lleva estos claims:

| Claim | Contenido |
|---|---|
| `sub` | Id del usuario |
| `email` | Correo |
| `name` | Nombre completo |
| `role` | `Administrador` o `Cliente` (uno por rol) |

```bash
curl -X POST http://localhost:5120/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@infection.com","password":"Admin123*"}'
```

```json
{
  "tokenType": "Bearer",
  "accessToken": "eyJhbGciOi...",
  "expiresAt": "2026-10-08T22:10:00+00:00",
  "user": { "id": "…", "email": "admin@infection.com", "fullName": "Administrador InfectionSolutions",
            "roles": ["Administrador"], "customer": null }
}
```

### Registro de clientes

`POST /api/auth/register` (público) hace lo siguiente:

- Crea la cuenta con rol **Cliente** y su ficha de cliente.
- Envía el correo de bienvenida.
- Devuelve el token, así que la sesión queda iniciada.

Si la empresa ya tenía ese cliente con el mismo documento y correo, la cuenta se vincula a esa ficha y
conserva su historial de compras. La contraseña necesita mínimo 8 caracteres, con al menos una mayúscula,
un número y un símbolo.

```bash
curl -X POST http://localhost:5120/api/auth/register -H "Content-Type: application/json" -d '{
  "document": "1020458796", "fullName": "Laura Mendoza", "age": 34,
  "email": "laura@correo.com", "phone": "+57 310 555 0101", "address": "Calle 100 # 15-20",
  "password": "Cliente123*", "confirmPassword": "Cliente123*" }'
```

## Endpoints

| Método y ruta | Quién | Qué hace |
|---|---|---|
| `POST /api/auth/login` | Público | Inicia sesión y devuelve el token |
| `POST /api/auth/register` | Público | Registra un cliente (201) |
| `GET /api/auth/me` | Autenticado | Perfil y ficha de cliente |
| `GET /api/products?q=&category=&active=&page=&pageSize=` | Autenticado | Lista paginada. El cliente solo ve productos activos |
| `GET /api/products/{id}` | Autenticado | Detalle (un producto inactivo da 404 para el cliente) |
| `GET /api/products/categories` | Autenticado | Categorías registradas |
| `POST /api/products` · `PUT /api/products/{id}` · `DELETE /api/products/{id}` | Administrador | CRUD |
| `GET /api/customers?q=&active=` · `GET /api/customers/{id}` | Administrador | Listado y detalle |
| `POST /api/customers` · `PUT /api/customers/{id}` · `DELETE /api/customers/{id}` | Administrador | CRUD |
| `GET /api/sales?q=&status=&customerId=` · `GET /api/sales/{id}` | Administrador | Todas las ventas |
| `POST /api/sales` | Administrador | Registra una venta para un cliente y le envía el comprobante |
| `DELETE /api/sales/{id}` | Administrador | **Anula** la venta (no la borra) y devuelve el stock |
| `GET /api/sales/{id}/receipt` | Administrador | Recibo PDF |
| `GET /api/me/sales` · `GET /api/me/sales/{id}` | Cliente | Compras propias |
| `POST /api/me/sales` | Cliente | Compra el carrito y envía el comprobante por correo |
| `GET /api/me/sales/{id}/receipt` | Cliente | Comprobante PDF de una compra propia |

Las búsquedas (`q`) no distinguen mayúsculas ni tildes. Los listados devuelven `items`, `page`,
`pageSize`, `totalCount`, `totalPages`, `hasPrevious` y `hasNext`. La página máxima es de 100 registros.

### Compra desde el portal

El cliente no envía su id: la API lo saca del token. Los precios se toman del catálogo (sin IVA) y la
API calcula el subtotal, el IVA del 19 % y el total.

```bash
curl -X POST http://localhost:5120/api/me/sales \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"lines":[{"productId":"<id>","quantity":2}],"notes":"Entregar en portería"}'
```

```json
{
  "sale": { "saleNumber": "V-20261008-0004", "subtotal": 72440.00, "tax": 13763.60, "total": 86203.60, "...": "..." },
  "receiptEmailSent": true,
  "sentTo": "laura@correo.com"
}
```

Si `receiptEmailSent` es `false`, el correo no salió, pero la compra igual quedó registrada. El
comprobante se puede descargar desde `GET /api/me/sales/{id}/receipt`.

## Errores

Todos los errores son **ProblemDetails** (`application/problem+json`). Los de validación traen
`errors` con el nombre del campo en camelCase, listo para mostrarlo junto al input:

```json
{
  "title": "Datos inválidos",
  "status": 400,
  "detail": "Uno o más campos no son válidos.",
  "errors": { "email": ["El correo no tiene un formato válido."] }
}
```

| Código | Cuándo |
|---|---|
| 400 | Datos inválidos (DataAnnotations de los ViewModels o reglas del servicio) |
| 401 | Sin token, token vencido o inválido, o credenciales incorrectas |
| 403 | El rol no tiene permiso (p. ej. un cliente en `/api/customers`), o el cliente está inactivo |
| 404 | No existe, o no es tuyo (una compra de otro cliente responde igual que una inexistente) |
| 409 | Duplicado (código, documento o correo), o eliminar algo que tiene ventas |
| 422 | Regla de negocio: stock insuficiente, producto inactivo o venta ya anulada |

## Configuración

| Clave | Para qué |
|---|---|
| `ConnectionStrings:DefaultConnection` | La misma base del panel (en local, con `user-secrets`) |
| `Jwt:SigningKey` | Clave de firma HMAC de 32 caracteres o más. En Development trae una de prueba; en producción es obligatoria |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes` | Emisor, audiencia y duración del token |
| `Email:Smtp:*` | Servidor SMTP. Por defecto Gmail (`smtp.gmail.com:587`, StartTls) |
| `Email:Provider` | `Smtp` (por defecto) o `Log`, que solo escribe los correos en el log |
| `Cors:AllowedOrigins` | Orígenes del cliente web (por defecto `http://localhost:4200`) |
| `Receipts:Path` | Carpeta de los recibos PDF (por defecto `App_Data/recibos`) |
| `AutoMapper:LicenseKey` | Opcional. Sin licencia AutoMapper funciona igual y deja un aviso en el log |

### Correo con Gmail

1. Activa la verificación en dos pasos en la cuenta de Google.
2. Crea una **contraseña de aplicación** en https://myaccount.google.com/apppasswords.
3. Guárdala en `user-secrets`, nunca en el repositorio:

   ```bash
   dotnet user-secrets set "Email:Smtp:UserName" "tu.cuenta@gmail.com" --project Src/InfectionSolutions.Api
   dotnet user-secrets set "Email:Smtp:Password" "abcd efgh ijkl mnop" --project Src/InfectionSolutions.Api
   ```

Sin credenciales la API arranca igual, pero los correos no salen: quedan en el log y
`receiptEmailSent` llega en `false`.

Para pasar a un **SMTP empresarial** solo cambia `Email:Smtp:Host`, `Port`, `Security` (`StartTls`,
`SslOnConnect` o `None`) y las credenciales. El código depende de la interfaz `IEmailSender`, así que otro
proveedor (por ejemplo una API de correo) se agrega con una implementación nueva en Infrastructure, sin
tocar los casos de uso.
