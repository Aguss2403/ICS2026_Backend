# Verificación del entorno Docker Compose

Ejecución realizada el 19 de octubre de 2026 desde un clon nuevo de `feature/docker-setup`, con Docker Engine 28.4.0, Docker Compose 2.39.4, SDK .NET 8.0.426 en el host y dotnet-ef 9.0.6 como herramienta local. DB_PASSWORD, JWT_KEY y las credenciales del administrador opcional fueron valores sintéticos generados localmente en `.env` (excluido de Git y del contexto Docker); no se incluyen en este informe ni se imprimieron durante la ejecución.

| Paso | Comando | Resultado |
| --- | --- | --- |
| Configuración | `docker compose config --quiet` | Código 0, sin mostrar valores interpolados. |
| SQL | `docker compose up -d --wait sqlserver` | sqlserver `healthy`, publicado en 127.0.0.1:14333. |
| Fase 1: migración desde el host | Preparación de [docker-compose.md](docker-compose.md) con Seed__Admin__* cargadas en el entorno del host | Compilación Release correcta; migración `InitialCreate` aplicada sobre la base Dsw2025Tpi, roles admin/user y administrador opcional creados. Código 0. |
| Fase 2: API en Production | `docker compose up -d --build api` + espera de /healthcheck | Imagen construida con el Dockerfile integrado; API en 127.0.0.1:5142 con `ASPNETCORE_ENVIRONMENT=Production`. |
| Healthcheck | `GET /healthcheck` | 200 `Healthy`. |
| Swagger UI | `GET /swagger/index.html` | 200 `text/html`. |
| OpenAPI | `GET /swagger/v1/swagger.json` | 200 `application/json`, OpenAPI 3.0.1 con 8 rutas (Auth, orders, products). |
| Operación autenticada | `POST /api/Auth/login` y `POST /api/products` con JWT admin | Login 200 con rol admin; producto creado (201). |
| Reinicio conservando el volumen | `docker compose down` y `docker compose up -d api` | Volumen `sqlserver_data` conservado; el administrador mantiene el mismo Id y `GET /api/products/{id}` devuelve el producto creado antes del reinicio. |
| Pruebas | `dotnet test Dsw2025Tpi.sln` con ICS_TEST_ADMIN_PASSWORD y ICS_TEST_JWT_KEY sintéticas | 64 aprobadas: 25 de configuración y 39 de inicialización. |
| Pruebas con SQL Server de Compose | `ApiSeedTests` con ICS_TEST_SQL_CONNECTION apuntando a 127.0.0.1:14333 | 1 aprobada sobre una base nueva ICSSeedTests_<GUID>; la base Dsw2025Tpi no se modificó. |

## Corrección aplicada

La consulta a /healthcheck inmediatamente después de `docker compose up -d api` cerró la conexión: el contenedor figura iniciado antes de que Kestrel acepte conexiones y el servicio api no define healthcheck de Docker (la imagen runtime no incluye curl). La fase 2 y el reinicio ahora reintentan /healthcheck hasta obtener `Healthy`; en la verificación respondió tras 6 intentos (unos 12 segundos). No se modificó el Dockerfile.

## Observaciones sin cambios en esta PR

- La API registra al iniciar `Failed to determine the https port for redirect`: `UseHttpsRedirection` está activo y el contenedor solo publica HTTP. No afecta las comprobaciones locales.
- Las claves de Data Protection se guardan dentro del contenedor y se regeneran al recrearlo. No afecta los JWT, que usan JWT_KEY.
- Si las pruebas de inicialización se ejecutan sin ICS_TEST_ADMIN_PASSWORD e ICS_TEST_JWT_KEY, 25 fallan por configuración faltante, como indica [admin-seed.md](admin-seed.md).
- La respuesta HTTP 500 del catálogo vacío corresponde a la tarea de validación de la PR #4.
