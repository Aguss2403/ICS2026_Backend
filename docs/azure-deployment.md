# Despliegue del backend en Azure

La API se ejecuta en un contenedor Linux de Azure App Service y conserva sus datos en [Azure SQL Database](azure-sql.md).

## Recursos y URL

| Recurso | Configuración |
| --- | --- |
| Grupo | `rg-ics2026-tp3` |
| Región | Canada Central |
| Plan | `asp-ics2026-tp3-free`, Linux, F1 Free |
| Aplicación | `tp3-ics-haurigot-ibanez-quiroga-salvatore-villanueva` |
| Registro de imágenes | `docker.io/ulisesquiroga/ics2026-backend` |
| Puerto interno | 8080 |

- [Swagger público](https://tp3-ics-haurigot-ibanez-quiroga-salvatore-villanueva.azurewebsites.net/swagger/index.html)
- [Salud](https://tp3-ics-haurigot-ibanez-quiroga-salvatore-villanueva.azurewebsites.net/healthcheck)
- [Catálogo](https://tp3-ics-haurigot-ibanez-quiroga-salvatore-villanueva.azurewebsites.net/api/products)

La aplicación exige HTTPS. El plan gratuito no mantiene la aplicación activa permanentemente; el arranque puede demorar tras un período de inactividad. La base también puede pausarse según su configuración gratuita. No cambiar de nivel ni habilitar excedentes para resolver estas pausas.

## Configuración externa de la API

Configurar estos nombres en las variables de entorno de App Service. Las credenciales y claves se suministran por un canal privado y no se incluyen en Git, imágenes, capturas ni registros.

| Nombre | Uso o valor |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Conexión privada a Azure SQL con cifrado y validación del certificado |
| `Jwt__Key` | Clave privada de firma |
| `Jwt__Issuer` | `Dsw2025Tpi.Api` |
| `Jwt__Audience` | `Dsw2025Tpi.Api.Users` |
| `Jwt__ExpireInMinutes` | `60` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://+:8080` |
| `WEBSITES_PORT` | `8080` |
| `WEBSITES_ENABLE_APP_SERVICE_STORAGE` | `false` |
| `Seed__Admin__Enabled` | `false`, tras preparar la base |

La migración y el administrador inicial se preparan antes del despliegue siguiendo [Azure SQL](azure-sql.md). La contraseña del seed no permanece configurada en App Service. Los datos persistentes están en SQL, no en el sistema de archivos del contenedor.

## Autenticación de GitHub Actions

La identidad administrada `ics2026-backend-github-deploy` tiene el rol `Website Contributor` limitado a esta aplicación. Su credencial federada `github-main` utiliza:

- Emisor: `https://token.actions.githubusercontent.com`.
- Sujeto: `repo:Aguss2403@133875830/ICS2026_Backend@1398591078:ref:refs/heads/main`.
- Audiencia: `api://AzureADTokenExchange`.

Este repositorio utiliza el formato de sujeto inmutable de GitHub, que incluye los identificadores permanentes del propietario y del repositorio. Para comprobar el valor vigente, consultar `gh api repos/Aguss2403/ICS2026_Backend/actions/oidc/customization/sub` y agregar `:ref:refs/heads/main` a `sub_claim_prefix`. La confianza de Azure debe coincidir exactamente con ese sujeto; el formato anterior basado solo en nombres produce `AADSTS700213`. La corrección conserva el mismo repositorio, la rama `main`, el emisor y la audiencia, y no amplía el rol de la identidad.

No utiliza contraseña de despliegue ni perfil de publicación. En GitHub, configurar los identificadores como secretos `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` y `AZURE_SUBSCRIPTION_ID`. Configurar las variables `AZURE_WEBAPP_NAME=tp3-ics-haurigot-ibanez-quiroga-salvatore-villanueva` y `AZURE_RESOURCE_GROUP=rg-ics2026-tp3`. Se conserva la configuración de Docker Hub existente.

## Publicar una versión estable

1. Integrar la rama `feature/` en `development` mediante PR revisada por otro integrante. Actions compila, prueba y publica la imagen de ese commit de integración.
2. Revisar la versión integrada y abrir una PR de `development` hacia `main`, con aprobación de otro integrante.
3. Al integrarla, el workflow valida el commit de `main`, publica su imagen `sha-<SHA completo>` y despliega exactamente esa etiqueta. El SHA de este commit puede diferir del publicado previamente desde `development`.
4. El job `deploy` autentica mediante OIDC, actualiza el contenedor y comprueba salud y catálogo. Su resumen registra SHA, imagen y URL. El catálogo debe contar con al menos un producto preparado previamente.
5. Registrar la ejecución exitosa de Actions y comprobar Swagger desde la URL pública.

Las PR, los pushes a `development` y las ejecuciones manuales no despliegan. Los fallos de validación o publicación impiden iniciar `deploy`. Un fallo de las comprobaciones posteriores marca el job como fallido, pero no revierte automáticamente la imagen.

Para recuperar una versión anterior, utilizar la etiqueta SHA verificada de esa versión mediante una operación de despliegue autorizada. Corregir después el código mediante PR para que el siguiente despliegue automático conserve la solución. No usar `latest` como referencia de recuperación y no revertir ni borrar SQL para recuperar el contenedor.

## Verificación inicial

El 25 de octubre de 2026 se desplegó inicialmente la imagen `ulisesquiroga/ics2026-backend:sha-eb41f0f56b815fddb1d98d0b0ceef64db1863296`, publicada previamente por Actions. Se verificaron salud, Swagger, login de administrador, creación y lectura de un producto en Azure SQL, y rechazo de creación anónima. El producto continuó disponible después de reiniciar la aplicación y retirar el acceso SQL temporal del equipo de preparación.

Ese mismo día se trasladó el alojamiento al nombre del equipo indicado en esta guía, dentro del mismo plan gratuito y conservando SQL y la imagen. Se repitieron las comprobaciones de salud, Swagger, login, permisos y catálogo en la nueva URL. Se actualizó el destino de GitHub y se trasladó el permiso de despliegue a la nueva aplicación; la aplicación anterior quedó detenida.

## Verificación del despliegue automático

El 25 de octubre de 2026 se integró la versión estable mediante la PR #13. La [ejecución de Actions](https://github.com/Aguss2403/ICS2026_Backend/actions/runs/40230919028) validó el commit `cc5990fac9257530da87c3fec1e4f5c958fe3815`, publicó su imagen SHA y la desplegó en Azure. La compilación Release y las 130 pruebas pasaron. El primer intento de autenticación evidenció el formato de sujeto anterior; después de corregir la credencial federada, el segundo intento completó el despliegue reutilizando la misma imagen validada.

Azure quedó configurado con `ulisesquiroga/ics2026-backend:sha-cc5990fac9257530da87c3fec1e4f5c958fe3815`. El pipeline comprobó salud y catálogo. Además se ejecutaron 28 solicitudes contra la URL pública: login y consultas 200; altas 201; modificaciones, desactivación y reactivación 200; datos inválidos 400; SKU duplicados 409; producto inexistente 404; operaciones protegidas sin token 401. Las 28 respuestas coincidieron con lo esperado y los tres productos sintéticos de esa prueba quedaron activos.

Referencias: [contenedores y GitHub Actions](https://learn.microsoft.com/azure/app-service/deploy-container-github-action), [OIDC con Azure](https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect), [sujetos inmutables de GitHub](https://github.blog/changelog/2026-04-23-immutable-subject-claims-for-github-actions-oidc-tokens/) y [direcciones de salida de App Service](https://learn.microsoft.com/azure/app-service/overview-inbound-outbound-ips).
