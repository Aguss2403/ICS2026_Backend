# Integración continua del backend

El workflow [Backend CI](../.github/workflows/backend.yml) valida el backend automáticamente en pull requests cuyo destino sea `development` o `main`, y ante pushes a esas ramas. También puede iniciarse manualmente desde la pestaña Actions mediante `workflow_dispatch`.

El único job, `validate`, corre en un runner Ubuntu nuevo y realiza estas comprobaciones:

1. Descarga el código y configura el SDK .NET 8.
2. Restaura los paquetes de la solución.
3. Compila todos los proyectos en Release con `--warnaserror`; los warnings de compilación hacen fallar el job.
4. Construye localmente la imagen con el Dockerfile raíz. No inicia sesión ni publica la imagen.
5. Genera una contraseña de administrador de prueba y una clave JWT aleatorias. GitHub Actions las enmascara en los logs y las entrega solo al paso de pruebas.
6. Ejecuta todos los proyectos de prueba de la solución en Release.

Las pruebas de seeding usan SQLite en memoria por defecto. Como el workflow no configura `ICS_TEST_SQL_CONNECTION`, no necesita SQL Server ni accede a una base personal. Las variables sintéticas cumplen la documentación de [inicialización y pruebas](admin-seed.md): la contraseña tiene menos de 90 caracteres y la clave JWT supera el mínimo de 32 bytes.

El workflow tiene permisos de solo lectura sobre el contenido del repositorio. Cualquier error en restauración, compilación, pruebas o construcción Docker hace fallar `validate`. Los valores sintéticos duran solo durante la ejecución del job y no se guardan como secretos del repositorio.

## Publicación en Docker Hub

El job `publish` depende del éxito de `validate` y se ejecuta únicamente en eventos `push` sobre `development` o `main`. En pull requests y ejecuciones manuales se omite por completo, por lo que esos eventos no inician sesión en Docker Hub ni publican imágenes.

La publicación usa la variable de repositorio `DOCKER_IMAGE`, cuyo valor esperado es `ulisesquiroga/ics2026-backend`, y los secretos `DOCKERHUB_USERNAME` y `DOCKERHUB_TOKEN`. Un paso previo comprueba que existan sin imprimir sus valores. Si el inicio de sesión falla, el job se detiene en **Log in to Docker Hub** y debe revisarse la vigencia del token, sus permisos y que corresponda al usuario configurado; las credenciales no deben copiarse a los registros ni a comentarios.

Después de validar el workflow, el job vuelve a descargar exactamente `${{ github.sha }}` y construye una sola imagen desde ese commit. La publica con estas etiquetas:

- `ulisesquiroga/ics2026-backend:latest`, que identifica la publicación más reciente.
- `ulisesquiroga/ics2026-backend:sha-<SHA completo>`, que conserva una referencia inmutable al commit validado.

El resumen de la ejecución de Actions muestra el SHA validado y ambas referencias. Para descargar una versión concreta sin depender de `latest`, usar el SHA completo que aparece en ese resumen:

```powershell
docker pull ulisesquiroga/ics2026-backend:sha-<SHA completo del commit>
```

## Consultar los resultados

En un pull request, abrí la sección **Checks** y seleccioná la comprobación de **Backend CI** para ver la ejecución. También podés ir a **Actions → Backend CI** y abrir la ejecución correspondiente; allí seleccioná el job **Validate backend**.

Si todos los pasos aparecen como exitosos, la validación terminó correctamente. Si falla, abrí el paso que figura con error para desplegarlo y consultar su registro, donde se muestra el detalle de la comprobación que falló.

La publicación real ocurre después de integrar un cambio en `development` o `main`. El workflow no configura Azure, no despliega la aplicación y no modifica la base de datos.
