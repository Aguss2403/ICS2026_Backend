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

El job tiene permisos de solo lectura sobre el contenido del repositorio. Cualquier error en restauración, compilación, pruebas o construcción Docker hace fallar `validate`. Los valores sintéticos duran solo durante la ejecución del job y no se guardan como secretos del repositorio.

## Consultar los resultados

En un pull request, abrí la sección **Checks** y seleccioná la comprobación de **Backend CI** para ver la ejecución. También podés ir a **Actions → Backend CI** y abrir la ejecución correspondiente; allí seleccioná el job **Validate backend**.

Si todos los pasos aparecen como exitosos, la validación terminó correctamente. Si falla, abrí el paso que figura con error para desplegarlo y consultar su registro, donde se muestra el detalle de la comprobación que falló.

Esta etapa no contiene publicación de imágenes, credenciales de Docker Hub/Azure ni despliegue. Esos pasos se incorporarán en trabajos separados cuando se asignen.
