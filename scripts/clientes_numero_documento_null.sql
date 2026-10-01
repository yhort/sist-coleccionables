-- =============================================================================
-- clientes_numero_documento_null.sql
-- Normaliza números placeholder de «sin documento» a NULL — PostgreSQL
-- CapitalPos TCG (Trunqi)
-- =============================================================================
-- PROPÓSITO
--   Actualizar clientes existentes cuyo numero_documento es '00000000' o
--   '0000000' (placeholders históricos de Cliente varios / Sin documento)
--   para guardar NULL de forma explícita.
--
-- USO
--   1. Backup completo antes de ejecutar.
--   2. Confirmar que la conexión apunta a la BD correcta.
--   3. psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/clientes_numero_documento_null.sql
-- =============================================================================

BEGIN;

-- Vista previa (opcional): descomentar para inspeccionar antes del UPDATE.
-- SELECT id, nombre, tipo_documento, numero_documento
-- FROM clientes
-- WHERE numero_documento IN ('00000000', '0000000');

UPDATE clientes
SET numero_documento = NULL
WHERE numero_documento IN ('00000000', '0000000');

COMMIT;
