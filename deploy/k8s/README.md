# Despliegue de Catalog en k3s

`catalog.yaml` crea el ConfigMap, el Deployment (con las migraciones como init container), el Service y el Ingress en el namespace `quickpatch`. Es igual en QA y en producción; solo cambian los secretos y la dirección de Kafka.

## Secreto `catalog-secretos`

Lo crea DevOps desde Ansible Vault. Nunca se guarda en el repositorio.

|Clave|Contenido|
|---|---|
|`migrator-connection`|Cadena de conexión con `catalog_migrator` (dueño de las tablas; solo para las migraciones)|
|`app-connection`|Cadena de conexión con `catalog_app` (sin `BYPASSRLS`)|
|`jwt-public-key`|Llave pública RSA de Identity en PEM, para validar los tokens (ADR-018)|

```bash
kubectl -n quickpatch create secret generic catalog-secretos \
  --from-literal=migrator-connection='Host=<vm-datos>;Port=5432;Database=db_catalog;Username=catalog_migrator;Password=<...>' \
  --from-literal=app-connection='Host=<vm-datos>;Port=5432;Database=db_catalog;Username=catalog_app;Password=<...>' \
  --from-file=jwt-public-key=identity-public.pem
```

## Primer despliegue

1. Base: `db_catalog` con dueño `catalog_migrator` y el rol `catalog_app` (Ansible).
2. Reemplazar `<vm-mensajeria>` en el ConfigMap por la IP de Kafka del ambiente y `kubectl apply -f deploy/k8s/catalog.yaml`. El pod queda esperando la imagen (`:pendiente`).
3. Push a `release/*`: el pipeline publica la imagen y la fija en el Deployment; el init container aplica las migraciones.
4. Solo la primera vez, un administrador de la base ejecuta `db/roles.sql` y reinicia el servicio con `kubectl -n quickpatch rollout restart deployment/catalog`.
