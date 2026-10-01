-- =============================================================================
-- limpieza_produccion.sql
-- Limpieza de datos operativos / transaccionales / catálogo — PostgreSQL
-- CapitalPos TCG (Trunqi)
-- =============================================================================
-- PROPÓSITO
--   Vaciar ventas, cajas, pagos, movimientos, pedidos, entregas, aperturas,
--   subastas, productos, catálogo TCG, clientes, proveedores, comprobantes y
--   logs de sync, dejando la empresa lista para operar “desde cero”.
--
-- NO SE TOCAN (seguridad / configuración base)
--   - empresas
--   - sedes
--   - usuarios                 (roles viven como enum en esta tabla)
--   - configuracion_fiscal_empresa
--   - series_comprobante
--   - integraciones_woocommerce
--   - integraciones_izipay
--   - ecosistema_conexiones
--   - "__EFMigrationsHistory"  (historial de migraciones EF Core)
--
-- REQUISITOS CUMPLIDOS
--   - TRUNCATE … RESTART IDENTITY CASCADE
--   - Un solo TRUNCATE multi-tabla: PostgreSQL trunca el conjunto de forma
--     atómica y respeta FKs entre las tablas listadas (CASCADE cubre el resto).
--   - El orden listado va de hojas → raíces solo por claridad documental.
--
-- USO
--   1. Backup completo antes de ejecutar.
--   2. Confirmar que la conexión apunta a la BD correcta.
--   3. psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/limpieza_produccion.sql
-- =============================================================================

BEGIN;

-- Abortar si el esquema CapitalPos no está presente.
DO $$
BEGIN
  IF to_regclass('public.empresas') IS NULL
     OR to_regclass('public.usuarios') IS NULL
     OR to_regclass('public.sedes') IS NULL THEN
    RAISE EXCEPTION
      'Esquema CapitalPos no detectado (faltan empresas/usuarios/sedes). Abortando.';
  END IF;
END $$;

TRUNCATE TABLE
  -- -------------------------------------------------------------------------
  -- Logs / pasarela / sync Woo (hojas; NO borra tablas de config de integración)
  -- -------------------------------------------------------------------------
  pasarela_webhook_logs,
  woocommerce_sync_logs,
  woocommerce_mapeos_producto,

  -- -------------------------------------------------------------------------
  -- Comprobantes / CPE emitidos
  -- -------------------------------------------------------------------------
  boletas_consolidadas,
  comprobantes,

  -- -------------------------------------------------------------------------
  -- Caja
  -- -------------------------------------------------------------------------
  caja_movimientos,
  caja_sesiones,

  -- -------------------------------------------------------------------------
  -- Ventas y pagos
  -- -------------------------------------------------------------------------
  venta_pagos,
  venta_detalles,
  ventas,
  pagos,

  -- -------------------------------------------------------------------------
  -- Entregas y pedidos digitales
  -- -------------------------------------------------------------------------
  entregas,
  pedido_digital_historial_estados,
  pedido_digital_detalles,
  pedidos_digitales,

  -- -------------------------------------------------------------------------
  -- Subastas / pujas
  -- -------------------------------------------------------------------------
  pujas,
  subasta_detalles,
  subastas_tcg,

  -- -------------------------------------------------------------------------
  -- Aperturas TCG
  -- -------------------------------------------------------------------------
  apertura_tcg_detalles,
  aperturas_tcg,

  -- -------------------------------------------------------------------------
  -- Inventario
  -- -------------------------------------------------------------------------
  movimientos_inventario,
  stocks_productos,

  -- -------------------------------------------------------------------------
  -- Compras / proveedores
  -- -------------------------------------------------------------------------
  compra_detalles,
  compras,
  proveedores,

  -- -------------------------------------------------------------------------
  -- Clientes
  -- -------------------------------------------------------------------------
  clientes,

  -- -------------------------------------------------------------------------
  -- Productos (TPT: cartas / sellados + contenido fijo)
  -- -------------------------------------------------------------------------
  producto_sellado_contenido_fijo,
  producto_cartas,
  producto_sellados,
  productos,

  -- -------------------------------------------------------------------------
  -- Catálogo TCG oficial (fichas importadas)
  -- -------------------------------------------------------------------------
  tcg_cartas,
  tcg_sets,
  tcg_series
RESTART IDENTITY CASCADE;

COMMIT;

-- =============================================================================
-- Post-chequeo opcional (descomentar para validar)
-- =============================================================================
-- SELECT 'productos' AS tabla, COUNT(*) AS filas FROM productos
-- UNION ALL SELECT 'ventas', COUNT(*) FROM ventas
-- UNION ALL SELECT 'clientes', COUNT(*) FROM clientes
-- UNION ALL SELECT 'pagos', COUNT(*) FROM pagos
-- UNION ALL SELECT 'movimientos_inventario', COUNT(*) FROM movimientos_inventario
-- UNION ALL SELECT 'usuarios (debe conservarse)', COUNT(*) FROM usuarios
-- UNION ALL SELECT 'sedes (debe conservarse)', COUNT(*) FROM sedes
-- UNION ALL SELECT 'empresas (debe conservarse)', COUNT(*) FROM empresas;
--
-- Opcional: reiniciar correlativos de series (config se conserva; solo contadores)
-- UPDATE series_comprobante SET correlativo = 1;
