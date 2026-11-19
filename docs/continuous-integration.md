# Integración continua del backend

El workflow [Backend CI](../.github/workflows/backend.yml) se ejecuta automáticamente en pull requests cuyo destino sea \`development\` o \`main\`, y ante pushes a esas ramas. También puede iniciarse manualmente desde la pestaña Actions mediante \`workflow_dispatch\`.

## Jobs y dependencias

El job obligatorio \`tests\` (**Test backend**) obtiene el código, configura el SDK .NET 8, restaura dependencias, compila la solución en Release con \`--warnaserror\` y ejecuta dos veces toda la suite. Cualquier error de restore, compilación o prueba falla este job. Las dos pasadas generan resultados TRX que se adjuntan como artifact \`test-results\`.

Durante el job, un paso genera valores aleatorios y sintéticos para \`ICS_TEST_ADMIN_PASSWORD\` e \`ICS_TEST_JWT_KEY\`, los enmascara y los expone solo al entorno del runner. No se guardan como secretos ni se imprimen como credenciales. La infraestructura TP4 usa SQLite en memoria y no requiere SQL Server, Azure SQL, Docker Compose ni credenciales externas.

El job \`docker-build\` (**Build Docker image**) depende de \`tests\` mediante \`needs: tests\`. Solo si la compilación y todas las pruebas pasan, descarga el mismo commit de la ejecución y construye una imagen local con el Dockerfile raíz. No inicia sesión ni publica.

El job \`publish\` depende de \`tests\` y \`docker-build\`; solo corre ante un \`push\` a \`development\` o \`main\`. En pull requests y ejecuciones manuales queda omitido. La publicación usa la variable de repositorio \`DOCKER_IMAGE\`, cuyo valor esperado es \`ulisesquiroga/ics2026-backend\`, y los secretos \`DOCKERHUB_USERNAME\` y \`DOCKERHUB_TOKEN\`. Un paso previo comprueba que estén configurados sin imprimir sus valores.

Luego del chequeo, publica el mismo SHA de la ejecución con estas etiquetas:

- \`ulisesquiroga/ics2026-backend:latest\`, referencia a la publicación más reciente.
- \`ulisesquiroga/ics2026-backend:sha-<SHA completo>\`, referencia inmutable al commit validado.

El resumen de Actions muestra el SHA validado y ambas referencias. Para descargar una versión concreta sin depender de \`latest\`, usar el SHA completo indicado en el resumen:

\`\`\`powershell
docker pull ulisesquiroga/ics2026-backend:sha-<SHA completo del commit>
\`\`\`

El job \`deploy\` depende de \`tests\`, \`docker-build\` y \`publish\`, y corre únicamente ante un \`push\` a \`main\`. Usa Azure OIDC y despliega la etiqueta \`sha-<SHA de la ejecución>\` que acaba de publicarse. Verifica \`/healthcheck\` y el catálogo público con al menos un producto. No vuelve a construir la imagen ni aplica migraciones.

Solo el job \`deploy\` recibe \`id-token: write\`, necesario para autenticarse en Azure mediante OIDC. La identidad federada debe aceptar únicamente este repositorio y la rama \`main\`, con permisos limitados a la aplicación. Los despliegues se serializan mediante el grupo \`azure-production\`. En PR, \`development\` y ejecuciones manuales el despliegue se omite. La infraestructura, los secretos de ejecución y la base deben estar preparados previamente. Ver [configuración y operación del despliegue](azure-deployment.md) y [preparación de Azure SQL](azure-sql.md).

## Consultar los resultados

En un pull request, abrí la sección **Checks** y seleccioná **Backend CI** para ver los jobs. También podés ir a **Actions → Backend CI → Validate backend** para abrir la ejecución de la validación. La ejecución presenta los jobs separados **Test backend** y **Build Docker image**; las tareas de publicación y despliegue aparecen omitidas cuando el evento no cumple sus condiciones.

Para inspeccionar una falla, abrí el job que aparece en rojo y después seleccioná el paso que falló para desplegarlo y consultar su registro. El artifact \`test-results\` contiene los resultados TRX de ambas pasadas cuando el job pudo generarlos.

La publicación real ocurre después de integrar un cambio en \`development\` o \`main\`. El despliegue a Azure ocurre únicamente tras integrar en \`main\`.
