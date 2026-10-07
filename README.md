# catalog

**Tecnología:** ASP.NET Core

## Responsabilidad

Catálogo de servicios, categorías y especialidades.

## Reglas

- Mantener el ownership definido en DD/SDD.
- No escribir directamente en tablas de otros servicios.
- Publicar/consumir eventos únicamente mediante contratos versionados.
- Mantener aislamiento multi-tenant cuando corresponda.

## Estructura

Cuatro capas, según el SDD (secciones 6.3 y 6.5):

```text
QuickPatch.Catalog.slnx
src/
  QuickPatch.Catalog.Api/              Endpoints, validación de entrada y raíz de composición
  QuickPatch.Catalog.Application/      Casos de uso, comandos y consultas
  QuickPatch.Catalog.Domain/           Entidades, value objects y reglas de negocio (sin dependencias externas)
  QuickPatch.Catalog.Infrastructure/   PostgreSQL, Kafka, Redis y adaptadores externos
tests/
  unit/QuickPatch.Catalog.UnitTests/
  integration/QuickPatch.Catalog.IntegrationTests/
```

Dependencias permitidas: `Api → Application → Domain`; `Infrastructure → Application, Domain`. `Api` referencia `Infrastructure` solo para registrar sus servicios.

## Desarrollo local

Requiere el SDK de .NET indicado en `global.json`.

```bash
dotnet restore
dotnet format --verify-no-changes   # lint, igual que el CI
dotnet build -c Release
dotnet test tests/unit/QuickPatch.Catalog.UnitTests
dotnet test tests/integration/QuickPatch.Catalog.IntegrationTests
dotnet run --project src/QuickPatch.Catalog.Api
```

## Capacidades implementadas

|Capacidad|Contrato|Roles|
|---|---|---|
|`GET /v1/catalog/categories`: categorías activas del tenant, por nombre|`catalog.v1.yaml` 1.1.0|Cualquier usuario autenticado|
|`POST /v1/catalog/admin/categories`: crear categoría (nombre único por tenant, sin distinguir mayúsculas)|`catalog.v1.yaml` 1.1.0|`admin_tenant`|
|`PATCH /v1/catalog/admin/categories/{id}`: cambiar descripción o activar/desactivar|`catalog.v1.yaml` 1.1.0|`admin_tenant`|

Cada alta o cambio publica **`catalog.category-changed`** v1 con el estado completo de la categoría, mediante Outbox (ADR-007). ServiceRequest y Matching mantienen con él su réplica local (DD 5.20), así que nadie llama a Catalog de forma síncrona (SAD 4.3).

## Seguridad y datos

- JWT RS256 de Identity (`Jwt__PublicKeyPem`); el tenant sale del token. Cada 403 queda en un log WARNING (RNF-04).
- RLS forzado en `service_categories` y `outbox_events` (DD 10.2); roles en `db/roles.sql` (`catalog_app` y `catalog_outbox`).
- `updated_at` marca la versión de cada categoría y viaja en el evento para que las réplicas descarten cambios atrasados.

## Configuración

|Clave|Para qué|
|---|---|
|`ConnectionStrings__Catalog`|PostgreSQL con el usuario `catalog_app`|
|`Jwt__PublicKeyPem`|Validación del token de Identity|
|`Kafka__BootstrapServers`|Kafka (VM6); vacío deshabilita el publicador|
|`Outbox__PublisherRole`|Rol `BYPASSRLS` del publicador|

## Pruebas

- `tests/unit`: dominio, casos de uso y API en memoria (cobertura de Domain, Application y Api).
- `tests/integration`: PostgreSQL 16 y Kafka reales (Testcontainers): eventos publicados, aislamiento por tenant, unicidad y RLS.

## Contenedor

- Imagen: `Dockerfile` en la raíz (multi-stage, usuario sin privilegios).
- Puerto: `8080`.
- Probes para k3s: `GET /health/live` (el proceso responde) y `GET /health/ready` (el servicio y sus dependencias están listos).

## Despliegue

- `deploy/k8s/catalog.yaml`: ConfigMap, Deployment, Service e Ingress (`C:/Program Files/Git/v1/catalog`) para k3s. Las migraciones se aplican con un *migration bundle* de EF Core (`/app/efbundle`, incluido en la imagen) como init container, con el rol `catalog_migrator`.
- Secretos y primer despliegue: `deploy/k8s/README.md`.
