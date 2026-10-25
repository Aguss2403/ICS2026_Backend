# Base de datos de la API en Azure

La API usa Azure SQL Database con el proveedor SQL Server de Entity Framework Core. La infraestructura se prepara antes de desplegar la imagen de la API.

## Recursos

| Recurso | Configuración |
| --- | --- |
| Grupo de recursos | `rg-ics2026-tp3` |
| Región | Canada Central |
| Servidor lógico | `sql-ics2026-tp3-matias.database.windows.net` |
| Base | `ics2026-tp3` |
| Nivel | Free - General Purpose - Serverless, Gen5, 2 vCores |
| Almacenamiento máximo | 32 GB |
| Copias de seguridad | Redundancia local |
| Cargos por excedente | Deshabilitados; se pausa al alcanzar el límite gratuito |

El 25 de octubre de 2026 se aplicó la migración y se repitió la inicialización sin errores. La API pública permitió iniciar sesión, crear un producto sintético y consultarlo en el catálogo, comprobando el acceso real a esta base.

## Conexión y secretos

La única configuración de conexión de la aplicación es `ConnectionStrings__DefaultConnection`. La conexión de Azure debe usar TCP 1433, indicar esta base y activar `Encrypt=True;TrustServerCertificate=False`. El usuario y su contraseña se suministran mediante configuración externa; no se incluyen en esta guía ni en Git.

La clave `Jwt__Key` también se configura externamente. No se reutilizan las credenciales del entorno local de Docker como credenciales de Azure.

La configuración SQL habilita `EnableRetryOnFailure` de EF Core. Una base Serverless pausada puede devolver el error transitorio 40613 durante su reactivación; la estrategia reintenta los errores transitorios y deja propagarse los errores permanentes. La conexión de Azure utiliza un tiempo de conexión de 60 segundos. Esto no evita la pausa al agotar la cuota gratuita: en ese caso hay que esperar a la renovación del cupo.

Se retiró la regla general de acceso desde servicios Azure. El firewall permite reglas individuales para las 43 direcciones declaradas en `possibleOutboundIpAddresses` de la aplicación; durante la verificación, una conexión utilizó una dirección de esta lista adicional. El permiso temporal del equipo que aplicó las migraciones se retiró al finalizar. La conexión sigue requiriendo autenticación y cifrado.

Si cambia el alojamiento o Azure modifica sus direcciones de salida, comparar nuevamente la lista de la aplicación con las reglas SQL. Para futuras migraciones desde un equipo, habilitar únicamente su dirección durante la operación y retirarla después.

## Migración e inicialización

1. Utilizar el SDK .NET 8 y `dotnet-ef` 9.0.6 desde un entorno autorizado para conectarse al servidor.
2. Cargar la conexión SQL y la configuración JWT por un mecanismo privado. Habilitar y configurar el administrador inicial siguiendo [la guía de inicialización](admin-seed.md).
3. Ejecutar desde la raíz del repositorio:

   ```powershell
   dotnet ef database update --project Dsw2025Tpi.Data --startup-project Dsw2025Tpi.Api --configuration Release
   ```

4. Verificar que la migración `20251125215438_InitialCreate` esté aplicada y que existan los roles esperados. Repetir el procedimiento para comprobar que la inicialización conserva los identificadores y no duplica roles ni administrador.
5. Deshabilitar el seed y retirar su contraseña del entorno habitual de la API. Conservar las credenciales de la cuenta inicial por el mecanismo privado acordado para la entrega.

No aplicar migraciones hacia atrás, borrar la base ni usar `EnsureDeleted` para preparar este entorno. Un script SQL de migración crea el esquema, pero por sí solo no ejecuta los callbacks de inicialización de EF Core.

## Verificación de la aplicación desplegada

- `/healthcheck` comprueba que la aplicación responde; no verifica por sí solo la conexión SQL.
- Crear un producto sintético con una cuenta administradora y comprobar que `GET /api/products` lo devuelve desde la URL pública.
- Confirmar que los permisos de las operaciones protegidas se conservan.
- Registrar el commit, la versión de imagen, la URL, los estados HTTP y el resultado, sin incluir contraseñas, tokens ni la conexión completa.

La comprobación inicial devolvió `200` en salud, Swagger, login y catálogo; `201` al crear el producto sintético `TP3-AZURE-CHECK`; y `401` al intentar crearlo sin autenticación. Se utilizó la imagen del commit `eb41f0f56b815fddb1d98d0b0ceef64db1863296`. Esta imagen inicial precede al cambio de reintentos; la imagen que lo incorpora se publicará al integrar esta PR.

Referencia: [oferta gratuita de Azure SQL Database](https://learn.microsoft.com/azure/azure-sql/database/free-tier).
