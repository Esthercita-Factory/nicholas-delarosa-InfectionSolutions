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

- Panel: `http://62.238.59.126:8080`
- Swagger: `http://62.238.59.126:8081/swagger`
- Portal de clientes: `http://62.238.59.126:8082`

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
| La API no arranca: `Jwt:SigningKey` | Falta `JWT_SIGNING_KEY` en `.env` o tiene menos de 32 caracteres. |
