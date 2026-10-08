# Verificación de la inicialización

Fecha: 8 de octubre de 2026. Rama: `feature/idempotent-admin-seed`, creada desde `development` actualizado en `a3a642b5594cea0256e34c41294493d40f6f4bc1`.

## Comprobaciones ejecutadas

Entorno de ejecución: Linux, SDK .NET 8.0.425, runtime 8.0.31, EF Core y `dotnet-ef` 9.0.6. Las contraseñas y la clave JWT usadas fueron sintéticas, generadas fuera del repositorio y suministradas por variables del proceso. No se conservaron sus valores en este informe.

| Comprobación | Resultado |
| --- | --- |
| Compilación de la solución y del proyecto de pruebas | Correcta. La compilación inicial muestra 18 advertencias de nulabilidad del código existente; no se modificaron esas áreas. |
| Suite acotada completa | **39 aprobadas, 0 fallidas, 0 omitidas**. |
| Roles: vacío, solo admin, solo user y ambos; repetición; conservación de Id | 8 casos aprobados, cubriendo rutas síncrona y asíncrona con SQLite relacional en memoria. |
| Creación de un admin sin Customer y conservación de Id, contraseña y RoleId al repetir con otra contraseña configurada | 2 casos aprobados. |
| Administrador deshabilitado con datos incompletos | 2 casos aprobados. |
| Cada campo obligatorio ausente | 6 casos aprobados; no se guardan ni se dejan entidades nuevas pendientes. |
| Campos inválidos o que exceden el esquema | 6 casos aprobados. |
| Conflictos por username, email, rol user, cuentas distintas y correo duplicado | 10 casos aprobados; cuentas intactas, sin elevar roles ni completar roles pendientes. |
| Registro real de callbacks EF, disabled predeterminado/explícito, Enabled inválido y precedencia de conexión | 4 casos aprobados. |
| Comprobación HTTP con callbacks reales y SQLite | 1 caso aprobado: login **200**, producto sin token **401**, producto con JWT admin **201** y producto verificado en base; un único User y ningún Customer. |
| `dotnet-ef migrations has-pending-model-changes` | Código de salida 0: `No changes have been made to the model since the last migration.` |
| `git diff --check` | Correcto. |

Comando de pruebas ejecutado, con las variables externas cargadas:

```bash
dotnet test Dsw2025Tpi.sln --no-restore -m:1 \
  -p:UseSharedCompilation=false \
  --logger 'trx;LogFileName=seed-sqlite.trx'
```

Salida final resumida:

```text
Passed! - Failed: 0, Passed: 39, Skipped: 0, Total: 39
```

Comando de compatibilidad de esquema:

```bash
dotnet-ef migrations has-pending-model-changes \
  --project Dsw2025Tpi.Data --startup-project Dsw2025Tpi.Api --no-build
```

## Limitación pendiente

**No se ejecutó la migración ni el recorrido HTTP sobre SQL Server/Docker en este entorno.** Docker no está instalado ni hay un daemon disponible. Se intentó iniciar una instancia nueva y aislada de SQL Server 2022, sin usar ninguna base existente, pero el proceso terminó antes de crear las bases con un error fatal de inicialización de SQLPAL (`0xc0000142`). No se presenta esa tentativa como una prueba aprobada.

El entorno también expone identificadores de proceso que no coinciden con sus rutas en `/proc`. Para ejecutar .NET se utilizó una adaptación temporal de lectura de esas rutas, externa al repositorio. No es un requisito del proyecto ni se incorpora a los comandos normales del usuario.

La suite contiene una variante optativa que aplica la migración real en SQL Server y repite por `MigrateAsync`, además de comprobar login y creación de producto. Debe ejecutarse en un host con SQL Server disponible siguiendo [las instrucciones](admin-seed.md#pruebas-acotadas). Usa siempre una base nueva `ICSSeedTests_<GUID>` y no elimina bases ni volúmenes.

No se borraron ni recrearon bases o volúmenes existentes. No se implementaron Compose, workflows, despliegue, cambios del frontend, permisos pendientes ni cambios de AuthenticationService. El PR requiere revisión y no se fusionó automáticamente.
