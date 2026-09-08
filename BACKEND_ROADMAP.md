# Backend roadmap — CapitalPOS / Trunqi TCG

Bitácora de implementación del API ASP.NET Core + EF Core + PostgreSQL.  
Fuente: `docs/PLANIFICACION_INICIAL.md` (sección 8 y modelo de datos). Las reglas de negocio las copia el servidor desde el prototipo Angular y `docs/MANUAL_OPERATIVO.md`.

Leyenda: `- [ ]` pendiente · `- [x]` hecha · **En progreso** = sprint activo.

---

## Sprint 1 — Infraestructura, EF Core, catálogo TCG e inventario

**Estado: Completado** (JWT + tenant + REST carta/sellado + kardex; BOM/compras y seed operativo quedan fuera)

Objetivo: proyecto `net10.0`, PostgreSQL, tenant `EmpresaId`, TPT de productos (carta/sellado) y kardex inmutable.

### Infraestructura

- [x] Crear solución y proyecto `CapitalPos.Tcg.Api` (.NET 10 Web API) en `backend/`
- [x] Paquetes NuGet: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Tools`, `Microsoft.EntityFrameworkCore.Design`
- [x] Cadena de conexión PostgreSQL en `appsettings.json`
- [x] Registrar `UseNpgsql()` y `ApplicationDbContext` en `Program.cs`
- [x] Proveedor de tenant (`EmpresaId`) para el filtro global EF
- [x] Herramienta local `dotnet-ef` (`backend/dotnet-tools.json`)
- [x] JWT Bearer (`POST /api/auth/login`) + claims `sub`, `empresa_id`, `role`
- [x] Middleware `TenantHeaderMiddleware`: lee `X-CapitalPos-EmpresaId`, setea `ITenantProvider` y exige que coincida con el token
- [x] Filtro `[RequiresPermission]` (roles `ADMIN` / `ALMACEN` / `CAJERO` → permisos del blueprint)

### Modelos de datos

- [x] Soporte/tenant: `Empresa`, `Sede`, `Usuario`
- [x] Hash de contraseña en `usuarios.password_hash` (migración `AddUsuarioPasswordHash`)
- [x] Catálogo TCG (TPT): `Producto`, `ProductoCarta`, `ProductoSellado`
- [x] Inventario inmutable: `StockProducto`, `MovimientoInventario`
- [x] Enums del Sprint 1 (`TipoProducto`, rareza, idioma, condición, tipo sellado, tipo movimiento, rol, tipo sede)

### Persistencia

- [x] `ApplicationDbContext` con Fluent API, índices y FKs compuestas `(EmpresaId, PadreId)`
- [x] Filtro global `HasQueryFilter` por `EmpresaId`
- [x] Columna computada `CantidadLibre = CantidadDisponible - CantidadReservada`
- [x] Convención de nombres snake_case (tablas del blueprint)
- [x] Migración EF Core `InitialCreate`
- [x] Aplicar migraciones a PostgreSQL (`dotnet ef database update`)

### Controladores REST (contratos §4.2 y §4.3)

- [x] `ProductosTcgController` `api/productos-tcg`: GET lista (`tipoProducto`, `juego`, `q`, `activo`), GET `/{id}`, POST, PUT, PATCH activar/desactivar
- [x] `InventarioController` `api/inventario`: GET `stock/{productoId}?sedeId=`, PUT `ajustar` (kardex `AJUSTE`, libro append-only), GET `kardex`
- [x] Escritura de almacén exige `OperarAlmacen`; lectura autenticada para cualquier rol
- [ ] BOM `/{id}/componentes*` y `POST /compuestos/{id}/armar|desarmar` — rutas publicadas, responden **501** hasta Sprint 1.b
- [ ] `GET|POST /compras` — rutas publicadas, responden **501** hasta el agregado de compras

### Fuera de este sprint (queda pendiente a propósito)

- [ ] `ProductoAccesorio`, `ProductoComponente` (BOM) — Sprint 1.b / Sprint 2
- [ ] Seed operativo Trunqi (sedes Mira/Surco, catálogo real)
- [x] Bootstrap de Development (`DevelopmentSeed`) para probar JWT: empresa demo + sede Miraflores + `admin@trunqi.local` / `Admin123!` (no sustituye el seed operativo)

---

## Sprint 2 — Aperturas TCG (+ subastas adelantadas)

**Estado: Completado** (migración `AddAperturasYSubastas` aplicada)

- [x] Entidades `AperturaTcg`, `AperturaTcgDetalle`
- [x] Migración `AddAperturasYSubastas` (incluye `subastas_tcg`, `pujas` y `pedidos_digitales` mínimo para adjudicación)
- [x] Confirmar apertura: transacción atómica `APERTURA_SALIDA_SELLADO` + N `APERTURA_INGRESO_CARTA`
- [x] Anular confirmada con asientos inversos (sin editar kardex); no anula si las cartas ya no tienen libre
- [x] Yield / prorrateo por valor de mercado al reemplazar detalles y al confirmar (`costoUnitarioAsignado`)
- [x] `AperturasTcgController` `api/aperturas-tcg`: GET lista (`sedeId`, `estado`, `desde`, `hasta`), GET `/{id}`, POST borrador, PUT `/{id}/detalles`, POST confirmar/anular
- [x] `SubastasTcgController` `api/subastas-tcg` (adelantado desde Sprint 4): activar, pujar, cerrar, adjudicar, cancelar
- [x] Adjudicar: `PUJA_GANADORA_RESERVA` en la sede de la subasta + pedido `PendientePago` **sin** segunda `RESERVA`

---

## Sprint 3 — Pedidos, pagos y entregas → venta

**Estado: Completado** (migración `AddPedidosPagosYVentas` aplicada)

- [x] Entidades `PedidoDigital`, `PedidoDigitalDetalle`, `PedidoDigitalHistorialEstado` + controller Kanban
- [x] Entidad `Pago` (bandeja Yape / Plin / Izipay / tarjeta / efectivo / transferencia)
- [x] Entidad `Entrega`
- [x] Entidades puente `Cliente`, `Venta`, `VentaDetalle`, `VentaPago` + `Comprobante` / `SerieComprobante` (CPE local `SIMULADO`)
- [x] Reserva al crear pedido (`RESERVA` sobre stock libre)
- [x] Asociación/confirmación de pago y tope al total del pedido
- [x] Confirmar entrega / recojo → `Venta` + kardex `VENTA` (confirma reserva) + boleta/factura local
- [x] Cancelar pedido → `LIBERACION_RESERVA`
- [x] Controladores `PedidosDigitales`, `Pagos`, `Entregas`
- [x] Máquina Kanban `PendientePago → Pagado → Empaquetado (EnPreparacion) → PendienteEntrega (ListoEntrega) → Entregado` / `Cancelado`
- [x] `POST /api/pagos/izipay/webhook` (AllowAnonymous; `empresaId` en el body)
- [x] `CapitalPos.Tcg.Api.http` escenarios Sprint 3

---

## Sprint 4 — Sincronización WooCommerce, CPE SUNAT Real y Reportes

**Estado: Completado** (migración `AddWooCommerceYSunatCpe` aplicada)

Las subastas TCG se entregaron junto al Sprint 2 (antes numeradas como Sprint 4 en esta bitácora).

Objetivo: catálogo/stock WooCommerce en tiempo real, webhooks de pedidos al Kanban, emisión CPE (XML UBL + firma + envío SUNAT/OSE) y reportes de kardex, ventas por sede y margen de aperturas.

### WooCommerce

- [x] Entidades `IntegracionWooCommerce`, `WooCommerceMapeoProducto`, `WooCommerceSyncLog`
- [x] Cifrado de consumer key/secret (`WooCredentialProtector` / Data Protection)
- [x] `WooCommerceSyncService`: sync catálogo TPT (carta/sellado) y stock-out = `CantidadLibre` de `SedeOrigenId`
- [x] Stock bidireccional en tiempo real (publicación tras kardex; webhook `product.updated` marca desfase y republica el libre local)
- [x] Import pedidos-in → `PedidoDigital` canal `WOOCOMMERCE` + `RESERVA` (polling `POST /sync/pedidos`)
- [x] Webhook `POST /api/woocommerce/webhooks/pedidos` (AllowAnonymous; `empresaId` query/body) alimenta el Kanban
- [x] Antiduplicado por `ReferenciaExterna` (índice único filtrado)
- [x] `WooCommerceController` `api/woocommerce`: config, probar, sync catálogo/stock/pedidos, mapeos, logs

### Facturación electrónica SUNAT (CPE)

- [x] Entidades `ConfiguracionFiscalEmpresa`, `EcosistemaConexion` (series/comprobantes ya existían en Sprint 3)
- [x] Abstracción `IServicioFiscal` / `ICpeEmisor`: boletas `B001` (`03`), facturas `F001` (`01`), notas de crédito `FC01` (`07`)
- [x] Generación y firma de XML UBL 2.1 + CDR; envío al ejecutable CPE (`CpeApi:BaseUrl` + `X-API-KEY`) o modo Local
- [x] Convertir venta deja de quedar en `SIMULADO` vacío: persiste XML, hash y estado `ACEPTADO` (Local) o el de SUNAT/OSE
- [x] `EcosistemaController` `api/ecosistema`: conexiones/ping, ficha fiscal, series, `POST /cpe/emitir-desde-venta/{ventaId}`, nota de crédito

### Reportes

- [x] `ReportesController` `api/reportes`: `GET /kardex-resumen`, `GET /ventas-por-sede`, `GET /aperturas` (margen/yield de aperturas `CONFIRMADA`)

### Persistencia y pruebas

- [x] Migración EF Core `AddWooCommerceYSunatCpe` aplicada a PostgreSQL
- [x] `CapitalPos.Tcg.Api.http` escenarios Sprint 4
- [x] Seed Development: ficha fiscal Lima, series B001/F001/FC01, integración Woo de demo, conexiones de ecosistema

### Fuera de este sprint (queda pendiente a propósito)

- [ ] `DashboardController` KPIs TCG (`GET /comercial-tcg`)
- [ ] Matriz de permisos configurable en Ecosistema (hoy: filtro por rol `RequiresPermission`)

---

## Sprint 5 — Integración Frontend en Angular

**Estado: Completado** (POS catálogo/inventario, Kanban de pedidos y emisión CPE contra `CapitalPos.Tcg.Api`)

Objetivo: sustituir los stores in-memory del prototipo Angular por `HttpClient` contra `CapitalPos.Tcg.Api`. Angular no conoce la `X-API-KEY` de CPE.

- [x] Login JWT (`POST /api/auth/login`), interceptors Bearer + `X-CapitalPos-EmpresaId`, proxy `/api` → `:5249`
- [x] CORS Development `http://localhost:4200`
- [x] `GET /api/sedes` y `GET /api/inventario/stock?sedeId=`
- [x] Productos TCG e Inventario/Kardex leen y escriben en la API
- [x] Kanban de pedidos digitales (`GET/POST /api/pedidos-digitales`, transiciones y cancelar)
- [x] Emisión CPE: ficha fiscal + `POST /api/ecosistema/cpe/emitir-desde-venta/{ventaId}`
- [x] `ng build` OK; smoke-test del proxy (`/health`, login, sedes, productos, stock, pedidos, fiscal/series)

### Fuera de este bloque (siguen in-memory o parciales)

- [x] Aperturas TCG, Subastas TCG y WooCommerce/webhooks como clientes HTTP (Sprint 6)
- [ ] Pagos, Entregas, Reportes y Dashboard como clientes HTTP de primer nivel
- [ ] ACCESORIO/COMPUESTO (501 en API)
- [ ] GRE / nota de crédito desde el diálogo CPE

---

## Sprint 6 — Persistencia Aperturas, Subastas y Webhooks

**Estado: Completado** (las pantallas dejan el store in-memory; PostgreSQL ya era la fuente de verdad en el API)

Objetivo: que confirmar una apertura, adjudicar una subasta o recibir un webhook de Woo persistan en PostgreSQL **sin** tocar el flujo POS (Kanban → venta) ni la emisión CPE.

- [x] Frontend Aperturas TCG → `api/aperturas-tcg` (kardex `APERTURA_*` lo escribe el API)
- [x] `PUT /api/aperturas-tcg/{id}` para editar el borrador del wizard
- [x] Frontend Subastas TCG → `api/subastas-tcg` (adjudicar **no** reserva stock en el cliente; evita doble `PUJA_GANADORA_RESERVA`)
- [x] Frontend WooCommerce → config, mapeos, logs, sync catálogo/stock, polling `sync/pedidos`
- [x] `POST /api/woocommerce/webhooks/simular` importa un pedido demo al Kanban
- [x] Webhooks de salida (WhatsApp) en `ecosistema_conexiones` (`WEBHOOK_SALIDA` + `ConfiguracionJson`)
- [x] `GET/PUT /api/ecosistema/webhooks` y `POST /api/ecosistema/webhooks/probar`

POS (productos/inventario/Kanban) y CPE (`emitir-desde-venta`) no se modificaron.

---

## Sprint 7 — Cierre de Caja y Arqueo Diario

**Estado: Completado** (2026-09-03) — migración `AddCajaSesionYArqueo` aplicada

Objetivo: un turno de caja por sede (fondo inicial, ventas del turno, caja chica y conteo físico) antes de confirmar ventas POS.

### Modelos y persistencia

- [x] Entidad `CajaSesion`: `MontoApertura`, `FechaApertura`, `FechaCierre`, `Estado` (`ABIERTA`/`CERRADA`), `MontoEfectivoTeorico`, `MontoEfectivoReal`, `Diferencia` (sobrante/faltante), `UsuarioId`, `SedeId`
- [x] Entidad `CajaMovimiento` (ingresos/egresos de caja chica del turno)
- [x] `Venta.CajaSesionId` — las ventas/comprobantes del turno cuelgan de la sesión activa
- [x] Índice único filtrado: una sola caja `ABIERTA` por sede
- [x] Migración EF Core `AddCajaSesionYArqueo` aplicada a PostgreSQL

### Endpoints (`CajaController` `api/caja`)

- [x] `POST /api/caja/apertura` — iniciar turno con saldo inicial de caja chica
- [x] `GET /api/caja/resumen-actual?sedeId=` — ventas desglosadas por medio (Efectivo, Yape/Plin, Tarjeta) y tipo de documento
- [x] `POST /api/caja/cierre` — conteo físico, diferencia teórico vs real y cierre
- [x] `GET /api/caja/{id}/reporte-ticket` — payload estructurado para ticket de arqueo 80mm
- [x] Extra: `GET /sesion-actual`, `GET /` historial de turnos, `POST /movimientos` (caja chica)

### Reglas de negocio

- [x] Confirmar entrega / convertir a venta exige caja abierta en la sede; si no, 400
- [x] Efectivo teórico = fondo + ventas en efectivo + ingresos − egresos
- [x] Diferencia = real − teórico (`SOBRANTE` / `FALTANTE` / `CUADRADO`)

### UI Angular

- [x] Módulo Caja (`/app/caja`): KPIs, tablas de medios/documentos/ventas, historial de turnos
- [x] Banner de turno en Pedidos Digitales y Entregas: si la caja está cerrada, no se confirman ventas
- [x] Modal de arqueo con diferencia en tiempo real e impresión de ticket 80mm

---

## Notas de avance

| Fecha | Sprint | Nota |
|-------|--------|------|
| 2026-08-27 | 1 | Hoja de ruta creada. Sprint 1 marcado En progreso. |
| 2026-08-27 | 1 | Proyecto `net10.0` `backend/CapitalPos.Tcg.Api`. Paquetes Npgsql 10.0.3 + EF Core Tools/Design 10.0.11. |
| 2026-08-27 | 1 | Entidades tenant + TPT carta/sellado + stock/kardex. `ApplicationDbContext` con filtro `EmpresaId`, FKs `(EmpresaId, PadreId)` y `cantidad_libre` computada. |
| 2026-08-27 | 1 | Migración `InitialCreate` generada (`Infrastructure/Persistence/Migrations`). PostgreSQL local no estaba disponible: no se ejecutó `database update`. |
| 2026-08-28 | 1 | Cadena `Username=yhortcruz;Password=;`. `database update` OK: base `capitalpos_tcg` + `20260827213722_InitialCreate`. |
| 2026-08-28 | 1 | JWT Bearer + middleware `X-CapitalPos-EmpresaId`. `ProductosTcgController` e `InventarioController` (carta/sellado, ajuste, kardex). Migración `AddUsuarioPasswordHash`. BOM/compras → 501. |
| 2026-08-28 | 2 | `AperturasTcgController` + yield/prorrateo. `SubastasTcgController` con reserva omnicanal `PUJA_GANADORA_RESERVA` y pedido mínimo sin segunda reserva. Migración `AddAperturasYSubastas` aplicada. `CapitalPos.Tcg.Api.http` con login, productos y ajuste/kardex. |
| 2026-08-28 | 3 | `PedidosDigitalesController` + Kanban, `PagosController` (Yape/Plin/Izipay/tarjeta/efectivo/transferencia) y `EntregasController`. Conversión atómica Entregado → `Venta` + kardex `VENTA` + CPE local `SIMULADO`. Migración `AddPedidosPagosYVentas` aplicada. |
| 2026-08-30 | 5 | Frontend Angular: login JWT, proxy `/api`, Productos/Inventario/Kanban/CPE contra `CapitalPos.Tcg.Api`. CORS Development + `GET /api/sedes` + stock por sede. `ng build` OK. |
| 2026-09-01 | 6 | Aperturas, Subastas y Webhooks (Woo inbound + WhatsApp outbound) dejan el store in-memory. PUT borrador de apertura + `webhooks/simular`. POS y CPE intactos. |
| 2026-09-03 | 7 | Caja/arqueo: `CajaSesion` + movimientos de caja chica, ventas del turno, gate POS y ticket 80mm. Migración `AddCajaSesionYArqueo`. |

### Cómo aplicar el schema y levantar el API

```bash
cd backend
dotnet ef database update --project CapitalPos.Tcg.Api
dotnet run --project CapitalPos.Tcg.Api --launch-profile http
```

Cadena: `Host=localhost;Port=5432;Database=capitalpos_tcg;Username=yhortcruz;Password=;`.

Auth de Development (solo si la base no tiene empresas):

- `POST /api/auth/login` `{ "email": "admin@trunqi.local", "password": "Admin123!", "empresaId": "c0a1e001-0000-4000-8000-000000000001" }`
- Header en el resto de llamadas: `Authorization: Bearer …` y `X-CapitalPos-EmpresaId: c0a1e001-0000-4000-8000-000000000001`

Ejemplos HTTP: `backend/CapitalPos.Tcg.Api/CapitalPos.Tcg.Api.http` (login, productos, ajuste/kardex, wizard de apertura, subasta, Kanban de pedidos, pagos, entregas, WooCommerce, CPE SUNAT, reportes y caja/arqueo).
