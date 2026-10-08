# Trabajo Práctico Integrador
## Desarrollo de Software
### Backend

## Introducción
Se desea desarrollar una plataforma de comercio electrónico (E-commerce). 
En esta primera etapa el objetivo es construir el módulo de Órdenes, permitiendo la gestión completa de éstas.

## Visión General del Producto
Del relevamiento preliminar se identificaron los siguientes requisitos:
- Los visitantes pueden consultar los productos sin necesidad de estar registrados o iniciar sesión.
- Para realizar un pedido se requiere el inicio de sesión.
- Una orden, para ser aceptada, debe incluir la información básica del cliente, envío y facturación.
- Antes de registrar la orden se debe verificar la disponibilidad de stock (o existencias) de los productos.
- Si la orden es exitosa hay que actualizar el stock de cada producto.
- Se deben poder consultar órdenes individuales o listar varias con posibilidad de filtrado.
- Será necesario el cambio de estado de una orden a medida que avanza en su ciclo de vida.
- Los administradores solo pueden gestionar los productos (alta, modificación y baja) y actualizar el estado de la orden.
- Los clientes pueden crear y consultar órdenes.

[Documento completo](https://frtutneduar.sharepoint.com/:b:/s/DSW2025/ETueAd4rTe1Gilj_Yfi64RYB5oz9s2dOamxKSfMFPREbiA?e=azZcwg) 

## Alcance para el Primer Parcial
> [!IMPORTANT]
> Del apartado `IMPLEMENTACIÓN` (Pag. 7), completo hasta el punto `6` (inclusive)


### Características de la Solución

- Lenguaje: C# 12.0
- Plataforma: .NET 8

## Estrategia de ramas

- `main` contiene la versión estable de la API.
- `development` reúne los cambios aprobados para integración.
- `feature/nombre-funcionalidad` se crea desde `development` para desarrollar una funcionalidad o una tarea del sprint.
- `hotfix/descripcion` se crea desde `main` para corregir un problema urgente de la versión estable.

### Integración de cambios

1. Crear una rama `feature/` desde la última versión de `development` y realizar allí los commits.
2. Abrir un Pull Request de la rama `feature/` hacia `development`.
3. Solicitar la revisión de otro integrante. El Pull Request debe recibir al menos una aprobación antes de fusionarse.
4. Para publicar una versión estable, abrir un Pull Request de `development` hacia `main` y obtener una aprobación antes de fusionarlo.

Los cambios en `main` y `development` se integran únicamente mediante Pull Requests; no se realizan commits directos en esas ramas.

### Correcciones urgentes

1. Crear una rama `hotfix/` desde `main` y realizar la corrección allí.
2. Abrir un Pull Request hacia `main` y obtener la aprobación de otro integrante antes de fusionarlo.
3. Incorporar la misma corrección en `development` mediante otro Pull Request para que las ramas no diverjan.

## Ejecución Local con Docker Compose

Para levantar el entorno completo localmente, debes seguir dos fases. Esto asegura que la base de datos esté lista e inicializada con los datos por defecto antes de que arranque la API en Producción.

### Fase 1: Inicialización de la Base de Datos SQL Server
1. Iniciar únicamente el contenedor de SQL Server:
   ```bash
   docker compose up -d sqlserver
   ```
2. Esperar a que el contenedor esté _healthy_.
3. Aplicar las migraciones desde el host utilizando la herramienta `dotnet ef` (v9.0.6):
   ```bash
   dotnet ef database update --project Dsw2025Tpi.Data --startup-project Dsw2025Tpi.Api
   ```

### Fase 2: Ejecución de la API
1. Una vez que la base de datos tiene el esquema correcto, levanta el contenedor de la API:
   ```bash
   docker compose up -d api
   ```
2. La API estará disponible en [http://localhost:5142](http://localhost:5142) y consultará a la base de datos a través de la red interna de Docker. 
3. La interfaz de Swagger estará accesible en `http://localhost:5142/swagger/index.html`.

### Variables de Entorno
Copia el archivo `.env.example` a `.env` y ajusta los valores (por ejemplo, `DB_PASSWORD` y los puertos si están ocupados):
```bash
cp .env.example .env
```
*(Nota: El archivo `.env` ya se encuentra excluido de Git para proteger los secretos).*

### Operaciones de Mantenimiento
- **Detener los servicios conservando el volumen de datos:**
  ```bash
  docker compose down
  ```
- **Borrado INTENCIONAL de la base de prueba:**
  Si deseas reiniciar la base de datos desde cero (perdiendo todos los datos), debes eliminar el volumen asociado al bajar los contenedores:
  ```bash
  docker compose down -v
  ```

