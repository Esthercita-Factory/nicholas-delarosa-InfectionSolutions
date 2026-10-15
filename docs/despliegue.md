# Despliegue con Docker

El sistema completo son cinco contenedores orquestados por `docker-compose.yml`:

| Servicio | Imagen | Qué es | Puerto |
|---|---|---|---|
| `tests` | se construye desde el código | Pruebas xUnit: si fallan, no se levanta nada | — |
| `db` | `postgres:17-alpine` | Base de datos (solo visible dentro de Docker) | — |
| `api` | `ghcr.io/esthercita-factory/nicholas-delarosa-infectionsolutions-api` | API REST + Swagger | 8081 |
| `admin` | `ghcr.io/esthercita-factory/nicholas-delarosa-infectionsolutions-admin` | Panel administrativo | 8080 |
| `client` | `ghcr.io/esthercita-factory/nicholas-delarosa-infectionsolutions.client` | Portal de clientes (Angular + Nginx) | 8082 |

Backend (`admin` y `api`) y frontend (`client`) son imágenes separadas. El portal vive en su propio
repositorio: [InfectionSolutions.Client](https://github.com/Esthercita-Factory/nicholas-delarosa-InfectionSolutions.Client).

## Orden de arranque

```
tests ──(código 0)──▶ db ──(lista)──▶ api ──▶ client
                              └────────▶ admin
```

Todos los servicios dependen de `tests` con `condition: service_completed_successfully`: si una prueba
falla, compose termina con error y no crea ningún otro contenedor. La API y el panel esperan a que
PostgreSQL acepte conexiones (`healthcheck`). Los dos aplican las migraciones al arrancar; un candado de
PostgreSQL (`pg_advisory_lock`) hace que lo hagan de a uno.

El portal no llama a la API por otro puerto: su Nginx reenvía `/api/*` al contenedor `api`, así que el
navegador ve un solo origen y no hace falta CORS.

## Levantarlo en cualquier equipo

Requisito: Docker Desktop (o Docker Engine con el plugin compose).

Los dos repositorios van uno al lado del otro: el compose busca el portal en `../InfectionSolutions.Client`
(se puede cambiar con `CLIENT_BUILD_CONTEXT` en `.env`).

```bash
git clone https://github.com/Esthercita-Factory/nicholas-delarosa-InfectionSolutions.git InfectionSolutions
git clone https://github.com/Esthercita-Factory/nicholas-delarosa-InfectionSolutions.Client.git InfectionSolutions.Client
cd InfectionSolutions
docker compose up --build
```

Construye las cuatro imágenes, corre las pruebas y, si pasan, levanta todo:

- Panel: http://localhost:8080 (`admin@infection.com` / `Admin123*`)
- Swagger: http://localhost:8081/swagger
- Portal de clientes: http://localhost:8082

Para detenerlo: `docker compose down` (los datos quedan en los volúmenes; `docker compose down -v` los borra).

## Publicación de imágenes (GitHub Actions)

Cada push a `main` ejecuta `.github/workflows/docker.yml` en los dos repositorios:

1. **Pruebas**: en este repo, la misma imagen del servicio `tests`; en el del portal, `npm test`.
2. **Si pasan**, construye y publica las imágenes en GitHub Container Registry con las etiquetas `latest`
   y `sha-<commit>`. Si fallan, no se publica nada.

Las imágenes quedan en la pestaña **Packages** de la organización. Si alguna aparece como privada:
*Package settings → Change visibility → Public* (o el servidor tendría que hacer `docker login ghcr.io`).

## Despliegue en el servidor del clan

En el servidor solo se puede usar Docker con `sudo` (`sudo docker ...`) y los puertos asignados son del
**8000 al 8999** (el 80 y el 443 son del Nginx del servidor).

### Primera vez

```bash
ssh esthercita
git clone https://github.com/Esthercita-Factory/nicholas-delarosa-InfectionSolutions.git ~/InfectionSolutions
git clone https://github.com/Esthercita-Factory/nicholas-delarosa-InfectionSolutions.Client.git ~/InfectionSolutions.Client
cd ~/InfectionSolutions
cp .env.example .env
nano .env        # contraseñas reales (ver abajo); Ctrl+O, Enter, Ctrl+X para guardar
```

En `.env` hay que cambiar como mínimo:

- `POSTGRES_PASSWORD`: una contraseña larga.
- `JWT_SIGNING_KEY`: 32 caracteres o más. Se puede generar con `openssl rand -base64 48`.
- `ADMIN_PASSWORD`: la contraseña del administrador inicial.
- `SMTP_USERNAME` y `SMTP_PASSWORD`: la cuenta de Gmail y su contraseña de aplicación, para que salgan los
  correos.

Luego:

```bash
sudo docker compose pull      # descarga api, admin y client desde GHCR
sudo docker compose up -d     # construye y corre las pruebas; si pasan, levanta todo
sudo docker compose ps        # estado de los contenedores
```

Queda disponible en:

- Portal de clientes: https://infectionsolutions.clandev.com
- API (Swagger): https://services.infectionsolutions.clandev.com/swagger
- Panel: `http://62.238.59.126:8080`

### Dominios (Nginx del servidor)

| Dominio | Servicio | Contenedor |
|---|---|---|
| https://infectionsolutions.clandev.com | Portal de clientes | `client` (127.0.0.1:8082) |
| https://services.infectionsolutions.clandev.com | API + Swagger | `api` (127.0.0.1:8081) |

**Cómo llega una visita al contenedor:**

```
navegador ──▶ Cloudflare (solo DNS) ──▶ 62.238.59.126:443 ──▶ Nginx del servidor ──▶ 127.0.0.1:8082 ──▶ client
                                                                  (según el dominio)    127.0.0.1:8081 ──▶ api
```

El TL crea los registros DNS en Cloudflare: los dos dominios apuntan a la IP del servidor en modo
**solo DNS** (nube gris). Ese modo es necesario para que certbot pueda validar el dominio. Lo demás lo hace
cada estudiante en el servidor.

#### Permisos en el servidor

`sudo -l` y `groups` muestran lo que se puede hacer:

- El grupo `deployers` puede escribir en `/etc/nginx/sites-available` y `/etc/nginx/sites-enabled`.
  Esos archivos se crean **sin `sudo`**: `sudo tee ...` responde *"I'm afraid I can't do that"*.
- Con `sudo` solo se permite `nginx -t`, `systemctl reload|restart|start nginx`, `certbot` y los comandos de
  `docker`.
- `conf.d` es solo de root: no sirve.

#### Paso 1: confirmar que los contenedores responden en el servidor

```bash
cd ~/InfectionSolutions
sudo docker compose ps
curl -s -o /dev/null -w "portal %{http_code}\n" http://127.0.0.1:8082/
curl -s -o /dev/null -w "api %{http_code}\n"    http://127.0.0.1:8081/swagger/index.html
```

Las dos deben responder 200. Si la API da 404, falta `SWAGGER_ENABLED=true` en `.env`.

#### Paso 2: crear el sitio de Nginx (sin `sudo`)

```bash
tee /etc/nginx/sites-available/infectionsolutions > /dev/null <<'EOF'
# Portal de clientes (contenedor client, puerto 8082)
server {
    listen 80;
    server_name infectionsolutions.clandev.com;

    location / {
        proxy_pass http://127.0.0.1:8082;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

# API REST + Swagger (contenedor api, puerto 8081)
server {
    listen 80;
    server_name services.infectionsolutions.clandev.com;

    location / {
        proxy_pass http://127.0.0.1:8081;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOF
ln -s /etc/nginx/sites-available/infectionsolutions /etc/nginx/sites-enabled/
```

El `'EOF'` va entre comillas para que la terminal no reemplace `$host`, `$scheme`, etc.: esas variables
son de Nginx.

#### Paso 3: validar y recargar Nginx

```bash
sudo nginx -t                    # debe decir "syntax is ok" y "test is successful"
sudo systemctl reload nginx      # solo si el paso anterior salió bien
```

Ese Nginx atiende a todos los proyectos del servidor: si `nginx -t` falla, **no se recarga**. Hay que
corregir el archivo o borrarlo. Si aparece `conflicting server name`, otro sitio usa el mismo dominio.

En este punto ya funciona por `http://`.

#### Paso 4: certificado HTTPS

```bash
sudo certbot --nginx -d infectionsolutions.clandev.com -d services.infectionsolutions.clandev.com
```

Certbot obtiene un certificado de Let's Encrypt para los dos dominios y agrega el bloque `listen 443 ssl` al
mismo archivo, junto con la redirección de HTTP a HTTPS. También deja programada la renovación automática.
Los certificados quedan en `/etc/letsencrypt/live/infectionsolutions.clandev.com/` y vencen cada 90 días,
pero se renuevan solos.

#### Paso 5: verificar desde afuera

```bash
curl -I https://infectionsolutions.clandev.com                    # 200
curl -I http://infectionsolutions.clandev.com                     # 301 hacia https
curl -I https://infectionsolutions.clandev.com/api/my/sales       # 401: el portal llega a la API
curl -I https://services.infectionsolutions.clandev.com/swagger/index.html   # 200
```

El portal llama a `/api` en su mismo dominio (su Nginx interno lo pasa al contenedor `api`), así que no
necesita CORS ni cambios en `.env`.

#### Opcional: cerrar los puertos directos

Con los dominios activos, los puertos 8081 y 8082 pueden quedar solo para el servidor. Así la única
entrada es por HTTPS. En `.env`:

```
API_PORT=127.0.0.1:8081
CLIENT_PORT=127.0.0.1:8082
```

Después: `sudo docker compose up -d`. Nginx sigue llegando porque está en el mismo servidor.

#### Cuidados (servidor compartido)

- Todos los `deployers` pueden escribir en `sites-available` y `sites-enabled`. Por eso hay que dejar el
  archivo solo editable por uno mismo: `chmod 644 /etc/nginx/sites-available/infectionsolutions`.
- Si el sitio deja de responder o muestra algo ajeno, revisar que el archivo siga ahí y que `sudo nginx -t`
  no muestre `conflicting server name`. La configuración de este documento sirve para restaurarlo.

### Recuperar contraseña e inicio con Google y GitHub

En el `.env` del servidor:

```
PORTAL_URL=https://infectionsolutions.clandev.com
SMTP_USERNAME=i.s.infectionsolutions@gmail.com
SMTP_PASSWORD=<contraseña de aplicación>
GOOGLE_CLIENT_ID=<…>.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=<…>
GITHUB_CLIENT_ID=<…>
GITHUB_CLIENT_SECRET=<…>
```

- `PORTAL_URL` es la dirección pública del portal. Con ella se arman los enlaces de "olvidé mi contraseña" y
  la vuelta de Google y GitHub; si quedara en `http://localhost:8082`, los enlaces de los correos no
  servirían fuera del servidor.
- Un proveedor sin credenciales no muestra su botón. El resto del portal funciona igual.
- La API guarda sus llaves de cifrado en el volumen `api-keys`: los enlaces ya enviados siguen sirviendo
  aunque se recree el contenedor.

#### Crear la app de Google

1. Entra a https://console.cloud.google.com/ y crea un proyecto (por ejemplo `InfectionSolutions`).
2. Menú ☰ → **APIs y servicios → Pantalla de consentimiento de OAuth** (o **Google Auth Platform**) →
   *Comenzar*:
   - Nombre de la app: `InfectionSolutions`, correo de asistencia y correo de contacto.
   - Público: **Externo**.
3. En **Público**, mientras la app está "En prueba" solo pueden entrar los correos agregados en *Usuarios de
   prueba*. Para que entre cualquier persona, pulsa **Publicar app**. Con los permisos básicos
   (`openid`, `email`, `profile`) no hace falta verificación de Google.
4. **Clientes → Crear cliente**:
   - Tipo: **Aplicación web**. Nombre: `InfectionSolutions portal`.
   - **URIs de redireccionamiento autorizados** (todas las que vayas a usar):
     - `https://infectionsolutions.clandev.com/api/auth/external/google/callback` (servidor)
     - `http://localhost:8082/api/auth/external/google/callback` (Docker en tu PC)
     - `http://localhost:5120/api/auth/external/google/callback` (Rider + `npm start`)
5. Copia el **ID de cliente** y el **Secreto del cliente**.

#### Crear la app de GitHub

GitHub solo admite **una** URL de retorno por app, así que se crea una para el servidor y otra para
desarrollo.

1. GitHub → foto de perfil → **Settings → Developer settings → OAuth Apps → New OAuth App**. Si quieres que
   sea de la organización: *Settings* de Esthercita-Factory → *Developer settings*.
2. Para el servidor:
   - Application name: `InfectionSolutions`
   - Homepage URL: `https://infectionsolutions.clandev.com`
   - Authorization callback URL: `https://infectionsolutions.clandev.com/api/auth/external/github/callback`
3. **Register application** → copia el **Client ID** → **Generate a new client secret** y cópialo (solo se
   muestra una vez).
4. Repite con otra app para tu PC, por ejemplo `InfectionSolutions (local)`, con la callback
   `http://localhost:8082/api/auth/external/github/callback` (Docker) o
   `http://localhost:5120/api/auth/external/github/callback` (Rider).

#### Dónde van las claves

| Dónde corre | Dónde se ponen |
|---|---|
| Servidor | `.env` del servidor (arriba). |
| Docker en tu PC | Tu `.env` local, con `PORTAL_URL=http://localhost:8082` y las credenciales de las apps de desarrollo. |
| Rider (`dotnet run`) + `npm start` | `user-secrets` de la API (ver abajo). `Portal:BaseUrl` ya es `http://localhost:4200`. |

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "<…>" --project Src/InfectionSolutions.Api
dotnet user-secrets set "Authentication:Google:ClientSecret" "<…>" --project Src/InfectionSolutions.Api
dotnet user-secrets set "Authentication:GitHub:ClientId" "<…>" --project Src/InfectionSolutions.Api
dotnet user-secrets set "Authentication:GitHub:ClientSecret" "<…>" --project Src/InfectionSolutions.Api
```

Para que el panel envíe el correo de recuperación a los administradores al correrlo desde Rider, también
necesita la cuenta de Gmail en **sus** user-secrets (`Email:Smtp:UserName` y `Email:Smtp:Password` con
`--project Src/InfectionSolutions.Admin`). En Docker la toma del mismo `.env`.

El Nginx del servidor no cambia: las vueltas de Google y GitHub entran por `/api` del dominio del portal.

### Actualizar a la última versión

Después de un push (y de que GitHub Actions termine en verde):

```bash
cd ~/InfectionSolutions
git pull
sudo docker compose pull
sudo docker compose up -d
```

Compose recrea solo los contenedores cuya imagen cambió. Los datos (base, recibos, sesiones) se
conservan en los volúmenes.

### Comandos útiles

```bash
sudo docker compose logs -f api          # logs de la API en vivo (Ctrl+C para salir)
sudo docker compose logs tests           # resultado de las pruebas del último arranque
sudo docker compose restart admin        # reiniciar un servicio
sudo docker compose down                 # detener todo (los datos se conservan)
```

### Si algo falla

| Síntoma | Causa probable |
|---|---|
| `service "tests" didn't complete successfully` | Falló una prueba: `sudo docker compose logs tests` muestra cuál. |
| `pull access denied` / `denied` al hacer pull | La imagen quedó privada en GHCR: cambiar la visibilidad a pública. |
| `port is already allocated` | Otro proyecto usa ese puerto: cambiar `ADMIN_PORT`, `API_PORT` o `CLIENT_PORT` en `.env`. |
| La página no abre desde el navegador | El firewall del servidor no deja pasar el puerto: pedirle al TL que lo abra. |
| Google responde `redirect_uri_mismatch` | La URL de retorno no está registrada tal cual en Google (revisar `https`, dominio y `/api/auth/external/google/callback`). |
| El enlace del correo apunta a `localhost` | Falta `PORTAL_URL` en el `.env` del servidor. |
| `502 Bad Gateway` en el dominio | Nginx no alcanza el contenedor: `sudo docker compose ps` y revisar que el puerto del `.env` coincida con el de `proxy_pass`. |
| La API no arranca: `Jwt:SigningKey` | Falta `JWT_SIGNING_KEY` en `.env` o tiene menos de 32 caracteres. |
