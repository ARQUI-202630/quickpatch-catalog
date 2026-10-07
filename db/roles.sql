-- Roles y permisos de Catalog Service (DD, sección 10.2).
-- Lo ejecuta un administrador de la base DESPUÉS de las migraciones (que crean las tablas y las políticas RLS).
-- Las contraseñas no van aquí: se asignan aparte con ALTER ROLE ... LOGIN PASSWORD, desde el vault.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'catalog_app') THEN
        CREATE ROLE catalog_app NOLOGIN NOBYPASSRLS;
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'catalog_outbox') THEN
        CREATE ROLE catalog_outbox NOLOGIN BYPASSRLS;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA public TO catalog_app, catalog_outbox;

-- Rol del servicio: todo bajo RLS.
GRANT SELECT, INSERT, UPDATE ON service_categories TO catalog_app;
GRANT INSERT ON outbox_events TO catalog_app;

-- Rol del publicador del Outbox: solo lee y marca eventos; lo adopta el servicio con SET LOCAL ROLE.
GRANT SELECT, UPDATE ON outbox_events TO catalog_outbox;
GRANT catalog_outbox TO catalog_app WITH INHERIT FALSE, SET TRUE;
