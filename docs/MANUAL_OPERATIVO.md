# Manual operativo — CapitalPOS Web (Trunqi TCG)

Documento técnico y operativo de la especialización TCG de CapitalPOS para **Trunqi**. Describe cómo fluye la mercadería y el dinero desde el catálogo hasta SUNAT, el diccionario de entidades de los 11 módulos, las reglas de negocio que no se pueden romper (Yield, Kardex inmutable, reservas omnicanal) y la rutina diaria por rol.

La implementación actual del frontend Angular (`frontend/src/app/features/`) materializa estas reglas en stores y servicios in-memory. El contrato REST y el modelo EF Core viven en `docs/PLANIFICACION_INICIAL.md`. Donde el prototipo Angular y el blueprint difieren en un nombre de campo, este manual declara **ambos** y indica cuál gobierna la operación.

---

## Índice

1. [Alcance, stack y convenciones](#1-alcance-stack-y-convenciones)
2. [Arquitectura operativa end-to-end](#2-arquitectura-operativa-end-to-end)
3. [Diccionario técnico y entidades (módulos 1–11)](#3-diccionario-técnico-y-entidades-módulos-111)
4. [Reglas de negocio clave](#4-reglas-de-negocio-clave)
5. [Guía de uso por roles](#5-guía-de-uso-por-roles)
6. [Permisos, rutas y checklist de cierre](#6-permisos-rutas-y-checklist-de-cierre)

---

## 1. Alcance, stack y convenciones

### 1.1 Qué cubre este sistema

Trunqi vende cartas sueltas, product sellado, accesorios y kits compuestos por **tienda física**, **subasta (Facebook / web / presencial)** y **tienda WooCommerce**. El POS no es un e-commerce: es el sistema de verdad del stock, las reservas, los cobros, el empaque, la facturación SUNAT y el yield de aperturas.

| Capa | Tecnología |
|------|------------|
| Frontend | Angular standalone, feature folders, tema Clean Administrative |
| API de negocio (objetivo) | ASP.NET Core + EF Core, multi-tenant (`EmpresaId`) |
| Facturación | Ejecutable local `capitalpos-cpe-api` (UBL + firma + SUNAT) |
| Auth | JWT Bearer + header `X-CapitalPos-EmpresaId` + matriz de permisos por rol |

### 1.2 Principios que no se negocian

1. **El catálogo es de empresa; el stock es de sede.** Una carta existe una vez en productos; su cantidad vive en `stocks_productos` por `(EmpresaId, SedeId, ProductoId)`.
2. **Solo se vende stock libre.** `CantidadLibre = CantidadDisponible − CantidadReservada`. Pedidos, subastas y WooCommerce consumen **libre**, no físico.
3. **El Kardex es un libro, no una planilla.** Los asientos no se editan ni se borran. Toda corrección es un asiento inverso.
4. **SUNAT no se calcula en Angular.** El POS arma una venta consistente y el módulo Ecosistema dispara el POST al ejecutable CPE.
5. **Una carta distinta (condición, foil, idioma) es otro SKU.** No hay variantes talla/color.

### 1.3 Sedes de referencia (seed operativo)

| Id | Nombre | Tipo | Rol logístico |
|----|--------|------|----------------|
| `sede-mira` | Tienda Miraflores | `TIENDA` | Punto de venta, recojo, subastas live, stock publicado a Woo |
| `sede-surco` | Almacén Surco | `ALMACEN` | Compra, resguardo, GRE de partida |

### 1.4 Mapa de los 11 módulos

| # | Módulo | Ruta Angular | Feature | Permiso de escritura por defecto |
|---|--------|--------------|---------|----------------------------------|
| 1 | Dashboard | `/app/dashboard` | `features/dashboard` | Admin |
| 2 | Productos TCG | `/app/productos-tcg` | `features/productos-tcg` | Admin, Almacén |
| 3 | Inventario | `/app/inventario`, `/app/inventario/kardex` | `features/inventario` | Admin, Almacén |
| 4 | Aperturas TCG | `/app/aperturas-tcg` | `features/aperturas-tcg` | Admin, Almacén |
| 5 | Subastas TCG | `/app/subastas-tcg`, `/app/subastas-tcg/:id` | `features/subastas-tcg` | Admin, Cajero |
| 6 | Pedidos Digitales | `/app/pedidos-digitales` | `features/pedidos-digitales` | Admin, Cajero |
| 7 | Pagos | `/app/pagos` | `features/pagos` | Admin, Cajero |
| 8 | Entregas | `/app/entregas` | `features/entregas` | Admin, Cajero, Almacén |
| 9 | WooCommerce | `/app/woocommerce` | `features/woocommerce` | Admin |
| 10 | Reportes | `/app/reportes` | `features/reportes` | Admin, Cajero (escritura de consulta/export) |
| 11 | Ecosistema | `/app/ecosistema` | `features/ecosistema` | Admin |

Entidades de soporte (no son módulo de UI propio): `empresas`, `sedes`, `puntos_venta`, `clientes`, `usuarios`, `ventas`, `venta_detalles`, `comprobantes`, `configuracion_fiscal_empresa`, `series_comprobante`.

---

## 2. Arquitectura operativa end-to-end

Esta sección recorre **una unidad de mercadería** desde que nace en catálogo hasta que aparece en reportes y en SUNAT. Cada paso indica qué módulo actúa, qué se escribe y qué stock queda.

```
Catálogo → Compra/ajuste → (opcional) Apertura
        → Publicación Woo / Subasta / Pedido manual
        → Reserva de stock libre
        → Cobro (Yape / Izipay / efectivo)
        → Empaque → Despacho / recojo
        → Confirmación = Venta + Kardex VENTA
        → CPE SUNAT
        → Reportes (canal, franquicia, yield, arqueo)
```

### 2.1 Paso 0 — Identidad fiscal y sedes (Ecosistema)

Antes de operar, Admin carga en **Ecosistema**:

- Ficha fiscal: RUC 11 dígitos, razón social, nombre comercial, dirección, UBIGEO 6 dígitos.
- Series: boleta `B***`, factura `F***` (no `FC`), nota de crédito `FC**`/`BC**`, guía `T***`.
- Sedes con flags GRE (punto de partida / llegada) y un único almacén principal.
- Usuarios con rol `ADMIN` | `CAJERO` | `ALMACEN`.
- Webhook WhatsApp opcional (`pedido.estado`, `guia.estado`).

Sin RUC válido no se simula ni se emite CPE. Sin sede no hay stock.

### 2.2 Paso 1 — Alta de producto (Productos TCG)

El operador (Almacén o Admin) crea un SKU según `TipoProducto`:

| Tipo | Qué representa | Datos extra | ¿Se abre? | ¿Se subasta? |
|------|----------------|-------------|-----------|--------------|
| `CARTA` | Una impresión + condición + foil + idioma | set, número, rareza, condición, foil, artista | No | Sí |
| `SELLADO` | Sobre, caja, ETB, tin, case | edición, tipo sellado, cartas esperadas, `permiteApertura` | Solo si `permiteApertura = true` | Sí |
| `ACCESORIO` | Fundas, binder, playmat, etc. | `tipoAccesorio` | No | Sí |
| `COMPUESTO` | Kit (BOM de un nivel) | hijos + cantidad | No | Sí |

Reglas de alta:

- `codigoSku` único por empresa (también se valida contra `woo.sku`).
- Precio de venta **con IGV**. Costo opcional (18,4). El costo del sellado alimenta el Yield.
- `stockLocal` del catálogo es un espejo de trabajo para Woo; **la verdad del stock es Inventario**.
- Un compuesto no puede contenerse a sí mismo ni a otro compuesto.
- Carta NM foil y carta NM no-foil son **dos productos**.

Al guardar, el producto queda `NO_MAPEADO` en Woo hasta que exista `wooCommerceId`.

### 2.3 Paso 2 — Ingreso físico (Inventario)

Almacén registra una **compra** o un **ajuste de entrada** en la sede destino.

1. `CantidadDisponible` sube.
2. `CantidadReservada` no se toca.
3. Se appenda un asiento Kardex `INGRESO_COMPRA` o `AJUSTE` con `stockAnterior` / `stockPosterior`.
4. `CantidadLibre` sube en la misma magnitud.

El producto ahora es vendible en esa sede. Si la sede es la `SedeOrigenId` de Woo, Admin debe **sincronizar stock** para que la web no ofrezca más de lo libre.

Armado / desarme de kits:

- `ARMADO_COMPUESTO`: sale 1× cada hijo, entra 1 padre (misma sede, stock libre suficiente de cada hijo).
- `DESARME_COMPUESTO`: sale 1 padre, entran los hijos.

### 2.4 Paso 3 — Apertura de sellado (Aperturas TCG)

Cuando Trunqi abre un ETB o un sobre para vender las cartas sueltas:

**Wizard (3 pasos)**

1. **Cabecera:** sede, sellado con `permiteApertura`, cantidad ≥ 1, observación. El sistema exige `stockLibre(sede, sellado) ≥ cantidad`.
2. **Cartas obtenidas:** se buscan por set + número; se elige condición wizard (`NM` / `EX` / `GD`) y foil. Cada línea debe resolver a un SKU de carta **ya existente** en catálogo. Se muestra la tarjeta de Yield en vivo.
3. **Confirmar:** persistir detalles, prorratear costo del sellado, escribir Kardex atómico.

Al **confirmar** (estado `BORRADOR` → `CONFIRMADA`):

| Asiento | Producto | Efecto |
|---------|----------|--------|
| `APERTURA_SALIDA_SELLADO` | sellado | − disponible |
| `APERTURA_INGRESO_CARTA` × N | cada carta | + disponible |

Todo ocurre dentro de `InventarioStore.transaccionar`: si un asiento falla, se restaura el snapshot. El sellado deja de estar a la venta; las cartas entran al stock libre de **la misma sede**.

Anular una confirmada genera los asientos **inversos** (`invertir: true`). Bloquea si alguna carta ya no tiene libre suficiente (se vendió o se reservó). Un borrador se anula sin tocar Kardex.

A partir de aquí el flujo de venta de una carta pull es idéntico al de cualquier SKU.

### 2.5 Paso 4 — Tres puertas de demanda (no se pisan)

La misma unidad libre puede salir por **una sola** de estas puertas. Las tres escriben reserva **antes** de cobrar.

```
                    ┌─ Subasta (Facebook / web / presencial)
Stock LIBRE de sede ┼─ Pedido manual (WhatsApp, IG, TikTok, mostrador)
                    └─ Pedido WooCommerce (import / webhook / polling)
```

#### 2.5.1 Puerta A — Subasta

1. Cajero crea subasta `BORRADOR` (producto, sede, precio base, incremento, precio reserva opcional, ventana de fechas).
2. `activar` → `ACTIVA`. Se registran pujas: monto ≥ última + incremento (o ≥ precio base si es la primera).
3. `cerrar` → `CERRADA`. Si la puja máxima ≥ precio reserva (o no hay reserva), se marca `esGanadora`.
4. `adjudicar`:
   - Exige 1 unidad **libre** en la sede de la subasta.
   - Kardex `PUJA_GANADORA_RESERVA` (+ reservada, disponible intacto).
   - Crea `PedidoDigital` en `PendientePago`, canal mapeado (`FACEBOOK_SUBASTA` / `WEB` / `OTRO`), total = monto ganador, 1 línea, entrega por defecto recojo.
   - **No** vuelve a ejecutar `RESERVA` sobre el pedido (evita doble reserva).
   - Subasta → `ADJUDICADA`, `pedidoDigitalId` vinculado.

Cancelar una `ACTIVA` no toca stock. Cancelar una `ADJUDICADA` con pedido aún `PendientePago` cancela el pedido (libera reserva). Si el Kanban ya avanzó, la cancelación de subasta se rechaza.

#### 2.5.2 Puerta B — Pedido digital manual

Cajero crea el pedido en el Kanban: cliente, sede, canal, líneas (SKU + cantidad + precio), entrega (recojo o dirección ≥ 5 caracteres).

Al `crear`:

1. Por cada línea: `stockLibre ≥ cantidad` o error nominativo (`No hay stock libre suficiente para {nombre}`).
2. Kardex `RESERVA` por línea (`referenciaTipo = PEDIDO_DIGITAL`).
3. Totales: `total = Σ (cantidad × precioUnitario)`; `subtotal = total / 1.18`; `igv = total − subtotal` (`TASA_IGV = 0.18`).
4. Estado inicial `PendientePago`. Indicador de reserva: **Reservado**.

#### 2.5.3 Puerta C — WooCommerce

Admin configura URL, consumer key/secret y `sedeOrigenId`.

**Salida (anti-overselling hacia la web):**

- `sincronizarStockYPrecios` publica, para SKUs mapeados, el CSV canónico `ID, SKU, Precio normal, Precio rebajado, Inventario`.
- El inventario publicado es el `stockLocal` del catálogo (debe mantenerse alineado con el libre de `SedeOrigenId`).
- SKUs `NO_MAPEADO` se omiten. Un desfase de stock o precio marca el mapeo `DESFASADO`.

**Entrada (pedido web → Kanban único):**

1. Polling (~7 s) o webhook simulado consume la cola Woo.
2. `importarDesdeWooCommerce`:
   - Rechaza `referenciaExterna` duplicada.
   - Canal `WOOCOMMERCE`, misma reserva `RESERVA` que un pedido manual (sede = `sedeOrigenId`).
   - Si Woo ya cobró (`pagado = true`), el pedido entra directo a `Pagado`.
3. A partir de ahí **no hay un Kanban Woo**: es el mismo tablero de Pedidos Digitales.

### 2.6 Paso 5 — Cobro (Pagos)

La bandeja es de primer nivel. Un pago puede nacer huérfano (voucher Yape, webhook Izipay) y asociarse después.

Máquina:

```
NOTIFICADO → ASOCIADO → CONFIRMADO
     ↘ RECHAZADO (cualquier estado excepto CONFIRMADO)
```

Reglas al asociar / confirmar:

- Solo pedidos en `PendientePago`.
- Código de operación obligatorio en Yape/Izipay; no puede repetirse (salvo pagos `RECHAZADO`).
- `Σ (ASOCIADO + CONFIRMADO) ≤ Total del pedido` (tolerancia 0,009).
- Al **confirmar**, si `Σ CONFIRMADO ≥ Total`, el pedido transiciona a `Pagado` (historial + observación del origen).
- `RECHAZADO` no suma. `CONFIRMADO` no se rechaza ni se reasocia.

Orígenes: `YAPE`, `IZIPAY`, `TRANSFERENCIA`, `EFECTIVO`, `OTRO`.

### 2.7 Paso 6 — Empaque y despacho (Pedidos + Entregas)

Máquina del Kanban (solo adelante, salvo cancelar):

```
PendientePago → Pagado → Empaquetado → PendienteEntrega → Entregado
         ↘ Cancelado (cualquier estado excepto Entregado)
```

Excepción de recojo: desde `Empaquetado` con `esRecojoTienda = true` se puede ir a `Entregado` sin pasar por courier.

Cola de Entregas = pedidos `Pagado` | `Empaquetado` | `PendienteEntrega`.

| Acción | Pedido | Entrega (logística) | Stock |
|--------|--------|---------------------|-------|
| Empaquetar (nota ≥ 3 chars) | `Pagado` → `Empaquetado` | persiste `notasEmpaque` | sin cambio (sigue reservado) |
| Despachar courier (tracking obligatorio) | `Empaquetado` → `PendienteEntrega` | `DESPACHADA`, tracking, agencia, costo | sin cambio |
| Despachar recojo | `Empaquetado` → `PendienteEntrega` | `PROGRAMADA`, tracking sintético `RECOJO-{id}` | sin cambio |
| Confirmar entrega | → `Entregado` + `ventaId` | `ENTREGADA` | Kardex `VENTA` |
| Fallar despacho | pedido no cambia | `FALLIDA` | **no** libera reserva |

Métodos: `RECOJO_TIENDA`, `OLVA_COURIER`, `SHALOM`, `DELIVERY_MOTO`. El costo de envío es **informativo**: no altera IGV del pedido.

Al confirmar, `VENTA` hace dos cosas a la vez:

- `CantidadDisponible − cantidad` (sale el físico).
- `CantidadReservada − min(cantidad, reservada)` (se confirma la reserva).

Indicador de reserva: **Confirmado**. Ya no se puede cancelar.

Cancelar en cualquier estado previo: `LIBERACION_RESERVA` y indicador **Liberado**. El SKU vuelve a estar vendible en todos los canales.

### 2.8 Paso 7 — Facturación SUNAT (Ecosistema)

Angular **nunca** habla con SUNAT ni ve `X-API-KEY`.

```
Pedido Entregado (hay Venta)
    → Ecosistema "emitir desde venta"
        → API CapitalPOS (serie, correlativo, cliente, IGV)
            → POST {CpeApi:BaseUrl}/api/cpe/emitir
                → ejecutable local (UBL + firma + sendBill / simulado)
                    → comprobantes.estado = SIMULADO | ACEPTADO | RECHAZADO | ERROR_*
```

En el prototipo Angular la emisión es **simulada**: toma un pedido `Entregado` (boleta/factura/NC) o una entrega despachada (guía `09`), incrementa el correlativo de la serie activa y deja log. La guía de remisión usa sedes marcadas como punto de partida GRE.

DTO canónico (`EmitirCpeRequest`, camelCase): emisor desde ficha fiscal, ítems desde `venta_detalles` (SKU + nombre; CPE **no** conoce `TipoProducto`), `unidadMedida = NIU`, afectación IGV `10`, moneda `PEN`, `tipoOperacion = 0101`, `formaPago = CONTADO`.

| Tipo | Código SUNAT | Serie | Cliente |
|------|--------------|-------|---------|
| Boleta | `03` | `B***` | DNI (`1`) o RUC (`6`) |
| Factura | `01` | `F***` | RUC obligatorio (`6`) |
| Nota de crédito | `07` | `FC**` / `BC**` | Exige motivo + documento de referencia |
| Guía de remisión | `09` | `T***` | Remitente = sede de origen |

### 2.9 Paso 8 — Métricas (Reportes + Dashboard)

**Reportes** consolida un periodo (`desde` / `hasta`, por defecto mes en curso):

| Bloque | Fuente | Qué mide |
|--------|--------|----------|
| KPIs | pedidos cobrados + stocks + pagos | ventas, margen bruto, inventario sellado vs cartas, conciliado Yape/Izipay |
| Ventas por canal | `origenDeCanal(canalPedido)` | Facebook, Woo/web, WhatsApp, tienda |
| Ventas por franquicia | `juego` del SKU | Pokémon, Magic, Yu-Gi-Oh!, Otros |
| Yield de aperturas | aperturas `CONFIRMADA` + Kardex | costo sellado vs valor comercial de pulls |
| Arqueo | pagos `CONFIRMADO` | por método y por estado de comprobante (emitido SUNAT vs pendiente) |

Pedido “cobrado” para ventas = `Pagado` | `Empaquetado` | `PendienteEntrega` | `Entregado`.

Margen bruto estimado = `Σ total pedidos − Σ (costo SKU × cantidad)`.

Un pedido `Entregado` con `ventaId` cuenta el pago asociado como **Emitido SUNAT** en el arqueo; el resto de confirmados queda **Pendiente**.

Exportación: CSV UTF-8 BOM `reportes-tcg-{desde}-{hasta}.csv`.

**Dashboard** (ruta `/app/dashboard`) es el tablero de turno: KPIs del día, mini-Kanban, stock bajo (umbral 3), subastas activas, aperturas del día. En el prototipo actual la página es placeholder; los datos ya se calculan en Pedidos, Inventario, Subastas y Reportes.

### 2.10 Diagrama de secuencia del caso feliz (subasta Facebook)

```
Cajero          Subastas         Kardex           Pedidos          Pagos         Entregas       Ecosistema
  |                |               |                 |               |              |               |
  |-- activar ---->|               |                 |               |              |               |
  |-- pujas ------>|               |                 |               |              |               |
  |-- adjudicar -->|-- PUJA_RESERVA>|-- PendientePago>|               |              |               |
  |                |               |                 |<- voucher ----|              |               |
  |                |               |                 |-- Pagado -----|              |               |
  |                |               |                 |               |-- empaquetar->|               |
  |                |               |                 |               |-- despachar->|               |
  |                |               |                 |               |-- confirmar->|-- VENTA ----->|
  |                |               |                 | Entregado+ventaId            |-- emitir CPE->|
```

### 2.11 Qué ve cada canal después de una reserva

| Canal | Qué deja de ofrecer | Mecanismo |
|-------|---------------------|-----------|
| Mostrador / WhatsApp | El SKU ya no sale en el alta de pedido si `libre = 0` | validación `stockLibre` |
| Subasta nueva | `adjudicar` falla si `libre < 1` | misma cuenta libre |
| WooCommerce | tras sync, inventario web = stock publicado | CSV / REST stock-out |
| Apertura | no se puede confirmar si el sellado no tiene libre | `stockLibre` del sellado |

La unidad reservada **sigue en `CantidadDisponible`** (sigue en la vitrina física) pero no es vendible. Por eso el indicador de la tarjeta Kanban dice Reservado / Liberado / Confirmado y no “sin stock”.

---

## 3. Diccionario técnico y entidades (módulos 1–11)

Convenciones globales:

- Identificadores: `string` UUID / código seed (`prd-001`, `sede-mira`). En EF Core: `Guid`.
- Fechas: ISO-8601 con offset (`DateTimeOffset`).
- Dinero: `number` en Angular; `decimal(18,2)` en SQL. Costos unitarios de apertura: `decimal(18,4)`.
- Cantidades de stock: `decimal(18,3)` en SQL; enteros positivos en UI de pedidos/aperturas.
- Auditoría: `FechaCreacion` en tablas transaccionales. Kardex además guarda `stockAnterior`, `stockPosterior`, `reservadoAnterior`, `reservadoPosterior`.

### 3.1 Módulo 1 — Dashboard

No tiene tablas propias. Consume lecturas de stock, aperturas, subastas y pedidos.

Ruta: `/app/dashboard`.

| Concepto | Tipo | Descripción |
|----------|------|-------------|
| Ventas del día | `number` | Suma de pedidos cobrados con fecha de hoy |
| Pedidos por columna | `Record<EstadoPedidoDigital, number>` | Conteos Kanban |
| Subastas activas | `number` | `estado = ACTIVA` |
| Aperturas del día | `number` | Confirmadas en la fecha local |
| Stock bajo | filas | `cantidadLibre ≤ 3` (constante `UMBRAL_STOCK_BAJO`) |

Contrato REST objetivo: `GET /api/dashboard/comercial-tcg`, `GET /api/dashboard/reporte-canales`.

### 3.2 Módulo 2 — Productos TCG

**Tablas:** `productos` + especializaciones TPT `producto_cartas`, `producto_sellados`, `producto_accesorios`, `producto_componentes`.

#### Enums

| Enum | Valores |
|------|---------|
| `TipoProductoTcg` | `CARTA`, `SELLADO`, `ACCESORIO`, `COMPUESTO` |
| `JuegoTcg` | `POKEMON`, `MAGIC`, `YUGIOH` |
| `RarezaTcg` | `COMUN`, `INFRECUENTE`, `RARA`, `RARA_HOLO`, `ULTRA`, `SECRETA`, `ESPECIAL` |
| `IdiomaTcg` | `ES`, `EN`, `JP`, `OTRO` |
| `CondicionTcg` | `NM`, `LP`, `MP`, `HP`, `DMG` |
| `TipoSellado` | `SOBRE`, `CAJA`, `CASE`, `TIN`, `COLECCION`, `OTRO` |
| `TipoAccesorio` | `FUNDAS`, `BINDER`, `PLAYMAT`, `DECKBOX`, `TOALLA`, `OTRO` |
| `EstadoStock` | `EN_STOCK`, `STOCK_BAJO`, `SIN_STOCK` |
| `EstadoSincronizacionWoo` | `SINCRONIZADO`, `DESFASADO`, `PENDIENTE`, `NO_MAPEADO`, `ERROR` |

`EstadoStock` se deriva: `≤ 0` → `SIN_STOCK`; `≤ 3` → `STOCK_BAJO`; resto `EN_STOCK`.

#### `ProductoTcg` (catálogo Angular / `productos` + TPT)

| Campo | Tipo | Notas |
|-------|------|-------|
| `id` | `string` | PK |
| `tipoProducto` | `TipoProductoTcg` | Discriminador TPT |
| `nombre` | `string(200)` | |
| `codigoSku` | `string(64)` | Único por empresa |
| `codigoBarras` | `string(64)?` | |
| `precioVenta` | `number` / `decimal(18,2)` | Con IGV |
| `costo` | `number?` / `decimal(18,4)?` | Costo de reposición / compra |
| `juego` | `JuegoTcg \| ''` | Vacío en accesorios genéricos |
| `activo` | `boolean` | Baja lógica |
| `stockLocal` | `number` | Espejo para Woo y filtros de catálogo |
| `atributosTcg` | `AtributosTcg` | Unión de campos de carta/sellado/accesorio |
| `componentes` | `ComponenteCompuesto[]` | Solo `COMPUESTO` |
| `woo` | `MetadatosWooCommerce` | Mapeo CSV oficial |

#### `AtributosTcg`

| Campo | Tipo | Aplica a |
|-------|------|----------|
| `setCodigo` | `string(32)` | Carta (ej. `SV8`) |
| `setNombre` | `string(120)` | Carta |
| `numeroCarta` | `string(16)` | Carta (ej. `025/198`) |
| `rareza` | `RarezaTcg \| ''` | Carta |
| `condicion` | `CondicionTcg \| ''` | Carta |
| `esFoil` | `boolean` | Carta |
| `idioma` | `IdiomaTcg \| ''` | Carta |
| `artista` | `string(120)?` | Carta |
| `tipoSellado` | `TipoSellado \| ''` | Sellado |
| `edicion` | `string(120)` | Sellado |
| `cartasEsperadas` | `number \| null` | Sellado (referencia al abrir) |
| `permiteApertura` | `boolean` | Sellado; default `true` |
| `tipoAccesorio` | `TipoAccesorio \| ''` | Accesorio |

#### `ComponenteCompuesto` (`producto_componentes`)

| Campo | Tipo | Notas |
|-------|------|-------|
| `productoHijoId` | `string` | Carta, sellado o accesorio (no compuesto) |
| `sku` | `string` | Denormalizado para UI |
| `nombre` | `string` | Denormalizado |
| `cantidad` | `number` / `decimal(18,3)` | Unidades de hijo por 1 padre |

#### `MetadatosWooCommerce` / `AtributoWoo`

| Campo | Tipo | Columna CSV Woo |
|-------|------|-----------------|
| `wooCommerceId` | `number \| null` | ID |
| `sku` | `string` | SKU |
| `categoriasWoo` | `string[]` | Categories (`>` jerárquico) |
| `precioNormal` | `number` | Regular price |
| `precioRebajado` | `number \| null` | Sale price |
| `stockWoo` | `number \| null` | Stock |
| `imagenes` | `string[]` | Images |
| `atributos` | `{ nombre, valores[] }[]` | Attribute N name/value(s) |
| `estadoSincronizacion` | `EstadoSincronizacionWoo` | — |
| `ultimaSincronizacion` | `string \| null` | — |
| `mensajeError` | `string \| null` | — |

**Precio vigente** (Yield, reportes, Woo):

```
si precioRebajado > 0 y precioRebajado < precioNormal → precioRebajado
si no → precioNormal || precioVenta
```

#### Filtros de listado

`busqueda`, `tipoProducto | TODOS`, `juego | TODOS`, `estadoStock | TODOS`, `sincronizacionWoo | TODOS`.

### 3.3 Módulo 3 — Inventario y Kardex

**Tablas:** `stocks_productos`, `movimientos_inventario`, `compras`, `compra_detalles`.

#### `SedeInventario`

| Campo | Tipo |
|-------|------|
| `id` | `string` |
| `nombre` | `string` |
| `tipo` | `TIENDA \| ALMACEN` |

#### `StockProductoSede` (`stocks_productos`)

| Campo | Tipo | Notas |
|-------|------|-------|
| `id` | `string` | PK |
| `sedeId` | `string` | |
| `productoId` | `string` | |
| `cantidadDisponible` | `number` | Físico en sede |
| `cantidadReservada` | `number` | Comprometido no entregado |
| `cantidadLibre` | computada | `disponible − reservada` |

Único por `(EmpresaId, SedeId, ProductoId)`.

#### `TipoMovimientoInventario`

| Valor | Δ disponible | Δ reservada | Origen típico |
|-------|--------------|-------------|---------------|
| `AJUSTE` | ± (sentido manual) | 0 | Inventario |
| `INGRESO_COMPRA` | + | 0 | Compras |
| `RESERVA` | 0 | + | Pedido digital / Woo |
| `LIBERACION_RESERVA` | 0 | − | Cancelación |
| `VENTA` | − | − min(qty, reservada) | Entrega / recojo |
| `ANULACION_VENTA` | + | 0 | Anulación |
| `APERTURA_SALIDA_SELLADO` | − | 0 | Confirmar apertura |
| `APERTURA_INGRESO_CARTA` | + | 0 | Confirmar apertura |
| `ARMADO_COMPUESTO` | ± manual | 0 | Armar kit |
| `DESARME_COMPUESTO` | ± manual | 0 | Desarmar kit |
| `PUJA_GANADORA_RESERVA` | 0 | + | Adjudicar subasta |

`cantidad` del asiento es **siempre positiva**. El signo lo da el tipo (o `sentido` + flag `invertir`).

#### `ReferenciaMovimiento`

`AJUSTE_MANUAL` | `APERTURA_TCG` | `SUBASTA_TCG` | `PEDIDO_DIGITAL` | `VENTA` | `COMPRA` | `ENTREGA` | `COMPUESTO`

#### `MovimientoInventario` (`movimientos_inventario`)

| Campo | Tipo |
|-------|------|
| `id` | `string` |
| `sedeId` | `string` |
| `productoId` | `string` |
| `tipoMovimiento` | `TipoMovimientoInventario` |
| `cantidad` | `number` (> 0) |
| `stockAnterior` / `stockPosterior` | `number` |
| `reservadoAnterior` / `reservadoPosterior` | `number` |
| `referenciaTipo` | `ReferenciaMovimiento` |
| `referenciaId` | `string \| null` |
| `motivo` | `string` (mín. 3 caracteres) |
| `usuario` | `string` |
| `fechaCreacion` | `string` ISO |

**No existen** `update` ni `delete` de movimientos en el store.

#### Requests

| Request | Campos |
|---------|--------|
| `AjusteInventarioRequest` | `sedeId`, `productoId`, `tipoMovimiento`, `sentido` (`ENTRADA` \| `SALIDA`), `cantidad`, `motivo` |
| `RegistrarMovimientoRequest` | lo anterior + `referenciaTipo`, `referenciaId`, `usuario?`, `invertir?` |

`sentido` manual obligatorio en `AJUSTE`, `ARMADO_COMPUESTO`, `DESARME_COMPUESTO`.

#### Filtros Kardex

`busqueda`, `sedeId | TODAS`, `productoId | TODOS`, `tipoMovimiento | TODOS`, `desde`, `hasta` (inclusive, inicio/fin de día local).

### 3.4 Módulo 4 — Aperturas TCG

**Tablas:** `aperturas_tcg`, `apertura_tcg_detalles`.

#### Enums

| Enum | Valores | Notas |
|------|---------|-------|
| `EstadoAperturaTcg` | `BORRADOR`, `CONFIRMADA`, `ANULADA` | |
| `EstadoCartaObtenida` | `NM`, `EX`, `GD` | Grading corto del wizard |

Mapeo wizard → catálogo: `NM`→`NM`, `EX`→`LP`, `GD`→`MP`.

#### `AperturaTcg`

| Campo | Tipo |
|-------|------|
| `id` | `string` |
| `sedeId` | `string` |
| `productoSelladoId` | `string` | FK sellado |
| `cantidadSellados` | `number` | entero ≥ 1 |
| `estado` | `EstadoAperturaTcg` |
| `usuarioNombre` | `string` |
| `observacion` | `string(500)?` |
| `fechaCreacion` | `string` |
| `fechaConfirmacion` | `string \| null` |
| `detalles` | `AperturaTcgDetalle[]` |

#### `AperturaTcgDetalle`

| Campo | Tipo |
|-------|------|
| `id` | `string` |
| `productoCartaId` | `string` | Debe existir y ser `CARTA` activa |
| `cantidad` | `number` | entero ≥ 1 |
| `costoUnitarioAsignado` | `number \| null` | Prorrateo del costo del sellado |
| `estado` | `EstadoCartaObtenida` |
| `esFoil` | `boolean` |

#### `RendimientoApertura` (no persistido; se calcula)

| Campo | Tipo |
|-------|------|
| `costoSellado` | `number` |
| `valorEstimadoCartas` | `number` |
| `diferencia` | `number` |
| `yieldPorcentaje` | `number \| null` |

Filtros: `sedeId | TODAS`, `estado | TODOS`, `desde`, `hasta`.

### 3.5 Módulo 5 — Subastas TCG

**Tablas:** `subastas_tcg`, `pujas`.

#### Enums

| Enum | Valores |
|------|---------|
| `CanalSubastaTcg` | `FACEBOOK_SUBASTA`, `WEB`, `PRESENCIAL`, `OTRO` |
| `EstadoSubastaTcg` | `BORRADOR`, `ACTIVA`, `CERRADA`, `ADJUDICADA`, `CANCELADA` |

Blueprint EF: cierra en `CERRADA` y el pedido se crea al adjudicar; el prototipo añade el estado explícito `ADJUDICADA` cuando ya hay `pedidoDigitalId`. Operativamente son el mismo hito.

#### `SubastaTcg`

| Campo | Tipo | Notas |
|-------|------|-------|
| `id` | `string` | |
| `sedeId` | `string` | Sede que reserva al adjudicar |
| `productoId` | `string` | Cualquier tipo de producto activo |
| `titulo` | `string(200)` | mín. 3 caracteres |
| `canal` | `CanalSubastaTcg` | |
| `precioBase` | `number` | > 0 |
| `incrementoMinimo` | `number` | > 0 |
| `precioReserva` | `number \| null` | Si existe, ≥ precio base; si no se alcanza, no adjudica |
| `fechaInicio` / `fechaCierre` | `string` | cierre > inicio |
| `estado` | `EstadoSubastaTcg` | |
| `pujaGanadoraId` | `string \| null` | |
| `pedidoDigitalId` | `string \| null` | |
| `observacion` | `string(500)?` | |
| `pujas` | `PujaTcg[]` | |

#### `PujaTcg`

| Campo | Tipo |
|-------|------|
| `id` | `string` |
| `subastaTcgId` | `string` |
| `clienteId` | `string \| null` |
| `nombrePostor` | `string(160)` | mín. 2 caracteres |
| `monto` | `number` | ≥ última + incremento |
| `fecha` | `string` |
| `esGanadora` | `boolean` |

#### `MargenSubasta` (calculado)

`precioBase`, `ofertaReferencia`, `diferencia = oferta − base`, `porcentaje = diferencia / base × 100` (null si no hay oferta o base = 0).

Mapeo canal subasta → canal pedido: `FACEBOOK_SUBASTA`→`FACEBOOK_SUBASTA`, `WEB`→`WEB`, resto→`OTRO`.

### 3.6 Módulo 6 — Pedidos Digitales

**Tablas:** `pedidos_digitales`, `pedido_digital_detalles`, `pedido_digital_historial_estados`.

#### Enums

| Enum | Valores |
|------|---------|
| `CanalPedidoDigital` | `FACEBOOK_SUBASTA`, `FACEBOOK_MARKETPLACE`, `WHATSAPP`, `INSTAGRAM`, `TIKTOK`, `WOOCOMMERCE`, `WEB`, `OTRO` |
| `OrigenPedidoDigital` | `FACEBOOK_SUBASTA`, `WEB_WOOCOMMERCE`, `WHATSAPP_DIRECTO`, `TIENDA_PRESENCIAL` |
| `EstadoPedidoDigital` | `PendientePago`, `Pagado`, `Empaquetado`, `PendienteEntrega`, `Entregado`, `Cancelado` |
| `IndicadorReservaPedido` | `Reservado`, `Liberado`, `Confirmado` |

Agrupación origen ← canal:

- Facebook subasta / Marketplace → `FACEBOOK_SUBASTA`
- WooCommerce / Web / Instagram / TikTok → `WEB_WOOCOMMERCE`
- WhatsApp → `WHATSAPP_DIRECTO`
- Otro → `TIENDA_PRESENCIAL`

Indicador: `Cancelado`→Liberado; `Entregado`→Confirmado; resto→Reservado.

#### `PedidoDigital`

| Campo | Tipo |
|-------|------|
| `id` | `string` |
| `clienteId` | `string \| null` |
| `clienteNombre` | `string` | mín. 2 caracteres |
| `clienteTelefono` | `string \| null` |
| `sedeId` | `string` | Sede de la reserva |
| `canalPedido` | `CanalPedidoDigital` |
| `estado` | `EstadoPedidoDigital` |
| `fechaPedido` | `string` |
| `subtotal` / `igv` / `total` | `number` |
| `referenciaExterna` | `string \| null` | Id Woo / post Facebook |
| `observacion` | `string(500)?` |
| `subastaTcgId` | `string \| null` |
| `ventaId` | `string \| null` | Se llena al convertir a venta |
| `detalles` | `PedidoDigitalDetalle[]` |
| `historialEstados` | `PedidoDigitalHistorialEstado[]` |
| `entrega` | `PedidoDigitalEntrega` | Snapshot; la entidad rica está en Entregas |

#### `PedidoDigitalDetalle`

| Campo | Tipo |
|-------|------|
| `id` | `string` |
| `productoId` | `string` |
| `descripcion` | `string` |
| `cantidad` | `number` | entero ≥ 1 |
| `precioUnitario` | `number` | ≥ 0 |
| `total` | `number` | `cantidad × precioUnitario` |

#### `PedidoDigitalHistorialEstado`

`estadoAnterior?`, `estadoNuevo`, `usuarioNombre?`, `fecha`, `observacion?`.

#### `PedidoDigitalEntrega` (snapshot)

| Campo | Tipo |
|-------|------|
| `destinatarioNombre` | `string(160)` |
| `destinatarioTelefono` | `string(32)?` |
| `direccion` | `string(300)?` | Obligatoria si no es recojo (mín. 5) |
| `distrito` / `provincia` / `departamento` | `string(80)?` |
| `courier` | `string(80)?` |
| `esRecojoTienda` | `boolean` |
| `numeroTracking` | `string(80)?` |
| `costoEnvio` | `number?` |
| `notasEmpaque` | `string?` |
| `agencia` | `string?` |

Constantes: `TASA_IGV = 0.18`. Columnas Kanban = los 6 estados (Cancelado visible para auditoría).

Filtros bandeja: `origen | TODOS`, `cliente`, `desde`, `hasta`, `montoMin`, `montoMax`.

### 3.7 Módulo 7 — Pagos

**Tabla:** `pagos`.

#### Enums

| Enum | Valores |
|------|---------|
| `OrigenPago` | `YAPE`, `IZIPAY`, `EFECTIVO`, `TRANSFERENCIA`, `OTRO` |
| `EstadoPago` | `NOTIFICADO`, `ASOCIADO`, `CONFIRMADO`, `RECHAZADO` |

`esOrigenDigital` = Yape o Izipay.

#### `Pago`

| Campo | Tipo |
|-------|------|
| `id` | `string` |
| `origen` | `OrigenPago` |
| `estado` | `EstadoPago` |
| `monto` | `number` | > 0 |
| `codigoOperacion` | `string(80)?` | Normalizado trim + UPPER |
| `referenciaExterna` | `string(120)?` |
| `pedidoDigitalId` | `string \| null` |
| `ventaId` | `string \| null` |
| `clienteNombre` | `string \| null` | Se copia del pedido al asociar |
| `fechaNotificacion` | `string` |
| `fechaConfirmacion` | `string \| null` |
| `usuarioAsocioNombre` | `string \| null` |
| `observacion` | `string(500)?` |

#### `PagosKpis`

`recaudadoHoy`, `cantidadHoy`, `pendientesConciliar` (digitales sin pedido y no rechazados), `montoPendienteConciliar`, `desglose: Record<OrigenPago, number>` (solo confirmados).

Filtros: `origen | TODOS`, `estado | TODOS`, `busqueda` (cliente o código), `montoMin`, `montoMax`.

Webhook Izipay objetivo: `POST /api/pagos/izipay/webhook` (firma, no JWT) → entra `NOTIFICADO`.

### 3.8 Módulo 8 — Entregas

**Tabla:** `entregas` (1:1 con el pedido en curso).

#### Enums

| Enum | Valores |
|------|---------|
| `MetodoEnvio` | `RECOJO_TIENDA`, `OLVA_COURIER`, `SHALOM`, `DELIVERY_MOTO` |
| `EstadoLogistica` | `PROGRAMADA`, `DESPACHADA`, `EN_TRANSITO`, `ENTREGADA`, `FALLIDA` |

#### `Entrega`

| Campo | Tipo |
|-------|------|
| `id` | `string` |
| `pedidoDigitalId` | `string` |
| `sedeOrigenId` | `string` |
| `metodoEnvio` | `MetodoEnvio` |
| `estado` | `EstadoLogistica` |
| `destinatarioNombre` | `string` |
| `destinatarioTelefono` | `string \| null` |
| `direccion` / `distrito` / `provincia` / `departamento` | `string \| null` |
| `agencia` | `string \| null` | Destino Shalom / Olva |
| `numeroTracking` | `string \| null` | Obligatorio si no es recojo |
| `costoEnvio` | `number` | ≥ 0, no suma IGV |
| `notasEmpaque` | `string \| null` |
| `fechaProgramada` / `fechaDespacho` / `fechaEntrega` | `string \| null` |
| `observacion` | `string \| null` |

#### `RemitenteTrunqi` (packing slip)

`nombre`, `razonSocial`, `direccion`, `distrito`, `telefono` — resuelto por `sedeOrigenId`.

Filtros: `metodoEnvio | TODOS`, `estado | TODOS`, `sedeId | TODAS`, `busqueda` (pedido, cliente, tracking, agencia).

KPIs de cola: `porEmpaquetar` (Pagado), `porDespachar` (Empaquetado), `enTransito` (PendienteEntrega).

### 3.9 Módulo 9 — WooCommerce

**Tablas:** `integraciones_woocommerce`, `woocommerce_mapeos_producto`, `woocommerce_sync_logs`.

#### Enums

| Enum | Valores |
|------|---------|
| `EstadoConexionWoo` | `CONECTADO`, `DESCONECTADO`, `ERROR_AUTENTICACION` |
| `EstadoMapeoWoo` | `SINCRONIZADO`, `DESFASADO`, `PENDIENTE_SUBIDA`, `NO_MAPEADO` |
| `TipoEventoSyncWoo` | `STOCK`, `PRECIO`, `PEDIDO` |
| `ResultadoEventoSyncWoo` | `OK`, `ERROR` |
| `ModoRecepcionPedidosWoo` | `POLLING`, `WEBHOOK` |

Mapeo `DESFASADO` si `stockWoo ≠ stockLocal` o `precioNormal ≠ precioVenta`, o si el catálogo marca `DESFASADO`/`ERROR`.

#### `ConfiguracionWooCommerce`

| Campo | Tipo | Notas |
|-------|------|-------|
| `urlTienda` | `string(300)` | Sin slash final |
| `consumerKey` / `consumerSecret` | `string` | En API real: cifrados; Angular no persiste secretos de CPE |
| `sedeOrigenId` | `string` | Stock que se publica y sede de reserva al importar |

#### `FilaCsvWoo` (contrato oficial)

`ID`, `SKU`, `Precio normal`, `Precio rebajado`, `Inventario`.

#### `FilaMapeoWoo`

Producto local vs Woo: ids, precios, stocks, `estadoMapeo`, mensaje, `ultimaSincronizacion`.

#### `PedidoWooPendiente` (cola)

`idWoo`, `referenciaExterna`, cliente, `pagado`, observación, `lineas[{ productoId, sku, cantidad, precioUnitario }]`, `entrega`.

#### `EventoSyncWoo`

`id`, `fechaHora`, `tipo`, `itemsAfectados[]`, `resultado`, `detalle`.

KPIs: conectado, sincronizados, desfasados, pendientes de subida, no mapeados, cola de pedidos.

### 3.10 Módulo 10 — Reportes

Sin tablas propias. Agrega pedidos, pagos, aperturas, productos y stocks.

#### Tipos

| Tipo | Valores / campos |
|------|------------------|
| `FranquiciaReporte` | `POKEMON`, `MAGIC`, `YUGIOH`, `OTROS` |
| `EstadoComprobanteReporte` | `EMITIDO_SUNAT`, `PENDIENTE` |
| `ReportesPeriodo` | `desde: string`, `hasta: string` (YYYY-MM-DD; vacío = sin cota) |

#### `ReportesKpis`

| Campo | Significado |
|-------|-------------|
| `ventasTotales` | Suma de `total` de pedidos cobrados del periodo |
| `pedidosCobrados` | Cantidad de esos pedidos |
| `margenBruto` | Ventas − Σ costo SKU × cantidad |
| `margenPorcentaje` | `margenBruto / ventasTotales × 100` o null |
| `inventarioSellado` / `inventarioCartas` | Valor a precio vigente × `cantidadDisponible` de SKUs activos |
| `inventarioActivo` | Suma de los dos anteriores |
| `unidadesSellado` / `unidadesCartas` | Unidades físicas |
| `totalConciliado` | Suma de pagos confirmados Yape/Izipay del periodo |
| `pagosConciliados` | Cantidad de esos pagos |

#### `SerieReporte`

`clave`, `etiqueta`, `monto`, `cantidad`, `porcentaje`.

#### `FilaYieldApertura` / `ResumenYieldAperturas`

Por apertura confirmada: sede, sellado, cantidades, `cartasObtenidas`, `cartasIngresadasKardex` (asientos `APERTURA_INGRESO_CARTA` con esa `referenciaId`), `costoCompra`, `valorComercial`, `diferencia`, `yieldPorcentaje`. El resumen suma costos/valores y recalcula yield global.

#### `ReporteArqueo`

`porMetodo[]` (`origen`, monto, cantidad, `emitidoSunat`, `pendiente`), `porComprobante[]`, `totalConfirmado`.

Métodos de arqueo: Yape, Izipay, Transferencia, Efectivo.

### 3.11 Módulo 11 — Ecosistema

**Tablas:** `ecosistema_conexiones` + `configuracion_fiscal_empresa` + `series_comprobante` + disparo CPE. El prototipo Angular concentra ficha fiscal, series, sedes, usuarios/permisos, webhooks y emisión simulada.

#### Enums

| Enum | Valores |
|------|---------|
| `TipoComprobanteSunat` | `BOLETA`, `FACTURA`, `NOTA_CREDITO`, `GUIA_REMISION` |
| `EstadoEmisionSunat` | `SIMULADO`, `ACEPTADO`, `RECHAZADO`, `ERROR_VALIDACION` |
| `TipoSedeEmpresa` | `TIENDA`, `ALMACEN` |
| `RolUsuario` | `ADMIN`, `CAJERO`, `ALMACEN` |
| `ModuloPermiso` | `dashboard`, `productos-tcg`, `inventario`, `aperturas-tcg`, `subastas-tcg`, `pedidos-digitales`, `pagos`, `entregas`, `woocommerce`, `reportes`, `ecosistema` |
| `EventoWebhook` | `pedido.estado`, `guia.estado` |
| Conexión (blueprint) | `CPE_LOCAL`, `WOOCOMMERCE`, `YAPE`, `IZIPAY` |
| Salud conexión (blueprint) | `DESCONOCIDO`, `OK`, `DEGRADADO`, `CAIDO` |

Códigos SUNAT: boleta `03`, factura `01`, NC `07`, GRE `09`.  
Series por defecto: `B001`, `F001`, `FC01`, `T001`.

#### `ConfiguracionFiscalEmpresa`

`ruc` (11 dígitos), `razonSocial`, `nombreComercial`, `direccionFiscal`, `ubigeo` (6 dígitos), `departamento`, `provincia`, `distrito`.

#### `SerieComprobante`

`tipo`, `serie` (4 chars, prefijo validado), `correlativo` (entero ≥ 1), `activa`.

#### `SedeEmpresa`

`id`, `nombre` (mín. 3), `tipo`, dirección y UBIGEO, `esPuntoPartidaGre`, `esPuntoLlegadaGre`, `esAlmacenPrincipal` (único), `activa`.

#### `UsuarioEmpresa`

`id`, `nombre`, `email` (único), `rol`, `activo`.

#### `PermisoModulo` / `MatrizPermisos`

`{ modulo, lectura, escritura }` por cada rol. Escritura implica lectura. Quitar lectura apaga escritura.

#### `WebhookSalida` / `WebhookLog`

Salida: `nombre`, `url` (http/https), `token`, `canal = WHATSAPP`, `eventos[]`, `activo`.  
Log: `evento`, `destino`, `payloadResumen`, `estado` `ENVIADO | OMITIDO | ERROR`, `fecha`.

#### `DocumentoEmitible` / `EmisionSimulada`

Emitible: origen `PEDIDO` (comprobantes) o `ENTREGA` (guía), ids, cliente, total, etiqueta.  
Emisión: tipo, serie, correlativo usado, estado, ids, mensaje, fecha.

### 3.12 Entidades puente venta / CPE (no son módulo de UI)

| Entidad | Rol |
|---------|-----|
| `ventas` | Se materializa al confirmar entrega o recojo. Único agregado que consume el DTO CPE. |
| `venta_detalles` | SKU, descripción, cantidad, valor/precio unitario, IGV, total, afectación `10`. |
| `venta_pagos` | Copia de pagos `CONFIRMADO` al convertir. |
| `comprobantes` | Serie, correlativo, XML, CDR, estado SUNAT. |
| `clientes` | `tipoDocumento` `1` DNI / `6` RUC / `4` CE / `7` pasaporte. |

---

## 4. Reglas de negocio clave

### 4.1 Fórmulas de Yield en aperturas

El Yield responde: **¿las cartas que salieron del sellado valen más o menos que lo que costó el sellado?** Se calcula en el wizard (tarjeta en vivo), al confirmar (se congela el prorrateo de costo) y en Reportes (solo aperturas `CONFIRMADA` del periodo).

#### 4.1.1 Costo del sellado

```
costoUnitarioSellado = ProductoSellado.costo  ??  ProductoSellado.precioVenta
costoSellado         = costoUnitarioSellado × cantidadSellados
```

Si el sellado no tiene costo cargado, el sistema **no inventa cero**: usa el precio de venta como proxy. Un Yield inflado suele ser un sellado sin `costo`. Buena práctica de Almacén: cargar costo en la compra.

#### 4.1.2 Valor de mercado de las cartas

```
precioVigente(carta) =
    carta.woo.precioRebajado    si > 0 y < carta.woo.precioNormal
    carta.woo.precioNormal      si no, cuando > 0
    carta.precioVenta           en último término

valorEstimadoCartas = Σ  precioVigente(carta_i) × cantidad_i
```

La carta se resuelve contra catálogo por **set + número + foil + condición** (el wizard mapea `EX`→`LP`, `GD`→`MP`). Si no hay match exacto, se usa la misma foil y, en último caso, la primera coincidencia de set/número. Cartas no encontradas aportan `0` al valor (bajan el Yield).

#### 4.1.3 Diferencia y porcentaje

```
diferencia       = valorEstimadoCartas − costoSellado
yieldPorcentaje  = (diferencia / costoSellado) × 100     si costoSellado > 0
                 = null                                  si costoSellado = 0
```

| Signo | Lectura operativa |
|-------|-------------------|
| Yield > 0 | El pull cubre el costo del sellado (a precios de lista vigentes) |
| Yield = 0 | Empate |
| Yield < 0 | Se abrió “por debajo”: el valor de mercado no cubre el costo |
| `null` | No hay denominador; no informar % |

En Reportes los montos se redondean a 2 decimales (`round2`) **después** de calcular. El Yield del periodo **no** es el promedio de porcentajes: se recalcula sobre la suma de costos y la suma de valores.

```
yieldPeriodo = (Σ valorComercial − Σ costoCompra) / Σ costoCompra × 100
```

#### 4.1.4 Prorrateo de costo hacia cada carta (costeo)

Al reemplazar detalles o confirmar, cada línea recibe `costoUnitarioAsignado`:

```
valorLinea_i = precioVigente(carta_i) × cantidad_i
totalValor   = Σ valorLinea_i
totalUnids   = Σ cantidad_i

si totalValor > 0:
    costoUnitario_i = (valorLinea_i / totalValor) × costoSellado / cantidad_i
si no, si totalUnids > 0:
    costoUnitario_i = costoSellado / totalUnids          // prorrateo uniforme
si no:
    0
```

Es un **prorrateo por valor de mercado**, no por rareza. Una secret rare se lleva más costo que un común. Ese costo unitario es el que debería usar el margen de la venta posterior de esa carta.

#### 4.1.5 Cruce con Kardex en el reporte

`cartasIngresadasKardex` cuenta asientos `APERTURA_INGRESO_CARTA` con `referenciaId = apertura.id`. Si `cartasObtenidas ≠ cartasIngresadasKardex`, la apertura está descuadrada (anulación parcial, fallo de transacción, o seed inconsistente) y hay que auditar el libro, no “corregir” la fila.

#### 4.1.6 Ejemplo numérico

ETB con `costo = 180`, cantidad 1. Pulls: carta A precio vigente 150 × 1 + carta B 12.50 × 2.

```
costoSellado        = 180
valorEstimado       = 150 + 25 = 175
diferencia          = −5
yieldPorcentaje     = −5 / 180 × 100 = −2.78 %

costoUnitario A     = (150/175) × 180 / 1 = 154.2857
costoUnitario B     = (25/175) × 180 / 2  = 12.8571
```

### 4.2 Lógica inmutable de asientos en Kardex

El Kardex es un **append-only ledger**. `InventarioStore` guarda `stocks` y `movimientos` como señales; `StockApiService.registrarMovimiento` **solo inserta** al frente de la lista. No hay API de edición ni de borrado.

#### 4.2.1 Anatomía de un asiento

Cada movimiento congela:

- Identidad: producto, sede, tipo, cantidad (> 0), motivo (≥ 3 chars), usuario, timestamp.
- Trazabilidad: `referenciaTipo` + `referenciaId` (apertura, pedido, subasta, venta, compra, compuesto).
- Fotografía: `stockAnterior` / `stockPosterior` y `reservadoAnterior` / `reservadoPosterior`.

Esa fotografía permite reconstruir el libro sin mutar filas viejas.

#### 4.2.2 Motor de efectos

```
efectoBase = f(tipoMovimiento, sentido, cantidad)

si invertir:
    efecto = −efectoBase

si tipo = VENTA y no invertir:
    efecto.deltaDisponible = −cantidad
    efecto.deltaReservada  = −min(cantidad, reservadaActual)

siguiente.disponible = actual.disponible + efecto.deltaDisponible
siguiente.reservada  = actual.reservada  + efecto.deltaReservada
```

Clasificación por tipo:

- Reserva (`RESERVA`, `PUJA_GANADORA_RESERVA`): +reservada, disponible igual.
- Liberación (`LIBERACION_RESERVA`): −reservada, disponible igual.
- Entrada neta (`INGRESO_COMPRA`, `APERTURA_INGRESO_CARTA`, `ANULACION_VENTA`): +disponible.
- Salida neta (`APERTURA_SALIDA_SELLADO`, `VENTA`): −disponible (venta además baja reservada).
- Manual (`AJUSTE`, armado/desarme): el operador elige `ENTRADA` o `SALIDA`.

#### 4.2.3 Invariantes (el asiento se rechaza si se rompen)

1. `cantidadDisponible ≥ 0`
2. `cantidadReservada ≥ 0`
3. `cantidadLibre ≥ 0` (la reserva nunca puede superar el físico)
4. `cantidad > 0` y finita
5. Motivo presente

Si el producto no tenía fila de stock en esa sede, se crea en ceros **antes** de aplicar el efecto (un ingreso a sede nueva es válido; una venta a ceros no).

#### 4.2.4 Atomicidad entre varios asientos

`InventarioStore.transaccionar` y `registrarMovimientos` copian stocks + movimientos, ejecutan el lote y, ante cualquier `throw`, restauran el snapshot. Por eso confirmar una apertura (1 salida de sellado + N ingresos de carta) **no deja el libro a medias**.

#### 4.2.5 Cómo se “corrige” entonces

| Situación | Qué hacer | Qué no hacer |
|-----------|-----------|--------------|
| Apertura mal confirmada | `anular` → asientos inversos (`invertir: true`) si hay libre | Borrar los asientos originales |
| Pedido cancelado | `LIBERACION_RESERVA` | Bajar `cantidadReservada` a mano sin asiento |
| Venta mal cobrada | `ANULACION_VENTA` (cuando exista el flujo) | Editar el asiento `VENTA` |
| Merma / daño | `AJUSTE` salida con motivo | Callar el faltante |
| Recuento a favor | `AJUSTE` entrada con motivo | “Redondear” el disponible en UI |

Anular apertura confirmada recorre primero las cartas: si `libre < cantidad ingresada`, aborta con *«hay cartas ingresadas que ya fueron vendidas o reservadas»*. Eso protege el libro: no se puede “devolver” al sellado algo que ya salió por otra puerta.

#### 4.2.6 Lectura del Kardex

La página `/app/inventario/kardex` es de **solo lectura**. Filtros por sede, SKU, tipo y rango de fechas. Un auditor debe poder explicar cada salto `stockAnterior → stockPosterior` con el `referenciaId`.

### 4.3 Reservas omnicanal (subastas / WooCommerce / pedidos) y overselling

Overselling = prometer la misma unidad en dos canales. Se evita con **una sola cuenta de stock libre por sede** y con reservas que no descuentan el físico hasta la entrega.

#### 4.3.1 La ecuación

```
libre(sede, sku) = disponible − reservada

vendible en TODOS los canales = libre
físico en vitrina / almacén   = disponible
comprometido no entregado     = reservada
```

Publicar Woo, aceptar un WhatsApp, adjudicar una subasta y abrir un sellado leen **la misma** `libre`.

#### 4.3.2 Quién reserva y con qué asiento

| Origen | ¿Cuándo reserva? | Tipo de asiento | ¿El pedido vuelve a reservar? |
|--------|------------------|-----------------|-------------------------------|
| Pedido manual | Al `crear` | `RESERVA` por cada línea | — |
| WooCommerce import | Al `importarDesdeWooCommerce` | `RESERVA` por cada línea | No (es el mismo `crear` interno) |
| Subasta | Al `adjudicar` | `PUJA_GANADORA_RESERVA` (qty 1) | **No.** `crearDesdeSubasta` inserta el pedido sin segundo `RESERVA` |
| Apertura | Confirmar | no reserva; saca sellado y mete cartas | Las cartas nuevas nacen libres |

Doble reserva de subasta es el bug clásico: el prototipo lo evita dejando la reserva en la adjudicación y creando el pedido “ya cubierto”. Cancelar ese pedido sí dispara `LIBERACION_RESERVA`, que deshace la puja ganadora.

#### 4.3.3 Guardas anti-overselling

1. **Antes de escribir:** `if (libre < cantidad) throw` con el nombre del SKU y el libre actual.
2. **Al escribir:** el motor del Kardex rechaza `libre < 0` y `reservada < 0`.
3. **Woo duplicado:** misma `referenciaExterna` → *«ya fue importado»*; no se reserva dos veces el mismo pedido web.
4. **Woo no mapeado:** no se publica ni se importa una línea sin `productoId` local.
5. **Subasta:** `adjudicar` exige `libre ≥ 1` **en la sede de la subasta**, no en otra.
6. **Apertura vs venta:** no se confirma apertura si el sellado ya está reservado (porque reserva reduce libre).
7. **Cancelación vs entrega:** `Entregado` no se cancela; `FALLIDA` no libera (el cliente sigue teniendo derecho sobre la unidad hasta que Cajero cancele el pedido).

#### 4.3.4 Publicación Woo (la otra cara del overselling)

La web puede vender más que el POS si se publica `disponible` en vez de `libre`, o si no se resincroniza después de una reserva.

Protocolo operativo:

1. Toda reserva (pedido, Woo, subasta) ocurre **primero** en CapitalPOS.
2. Inmediatamente, Admin (o el job `PROGRAMADA`) corre **Sincronizar stock y precios**.
3. Woo recibe `Inventario = stockLocal` del SKU mapeado. Ese `stockLocal` debe reflejar el libre de `sedeOrigenId` (en el prototipo el catálogo y el inventario son dos señales: Almacén/Admin deben mantenerlos alineados; el API objetivo publicará `CantidadLibre` de `SedeOrigenId`).
4. Un mapeo `DESFASADO` es una alerta de overselling potencial: no se ignora.

#### 4.3.5 Liberación y confirmación

```
Reservado   → cancelar pedido / cancelar subasta con pedido PendientePago
              → LIBERACION_RESERVA → libre sube, Woo puede volver a vender

Confirmado  → entrega o recojo
              → VENTA (−disponible, −reservada) → ya no hay vuelta atrás por cancelación
```

No existe “reservar sin asiento”. Cualquier compromiso visible en Kanban tiene fila en Kardex con `referenciaId = pedidoId` o `subastaId`.

#### 4.3.6 Kits compuestos

Armar un kit consume **libre** de los hijos. Si un hijo está reservado por un pedido Woo, no se puede armar el kit con esa unidad. Desarmar devuelve hijos libres. Un compuesto se reserva como **padre** (el kit), no como explosión de BOM, en el flujo de pedido actual.

---

## 5. Guía de uso por roles

Roles (`RolUsuario`): **Admin**, **Cajero / Vendedor**, **Encargado de almacén / Logística**. La matriz vive en Ecosistema y se puede ajustar; lo que sigue es el default (`matrizPermisosPorDefecto`) más la rutina diaria recomendada.

Escritura por defecto:

| Módulo | Admin | Cajero | Almacén |
|--------|:-----:|:------:|:-------:|
| Dashboard | L/E | L | L |
| Productos TCG | L/E | L | L/E |
| Inventario | L/E | L | L/E |
| Aperturas TCG | L/E | L | L/E |
| Subastas TCG | L/E | L/E | L |
| Pedidos Digitales | L/E | L/E | L |
| Pagos | L/E | L/E | L |
| Entregas | L/E | L/E | L/E |
| WooCommerce | L/E | L | L |
| Reportes | L/E | L/E | L |
| Ecosistema | L/E | L | L |

L = lectura, E = escritura. Todos pueden **ver** el tablero; no todos pueden **mover** stock o emitir CPE.

### 5.1 Admin — dueño de la operación y del cierre

**Objetivo del día:** que catálogo, canales, caja y SUNAT cierren cuadrados.

**Al abrir (10–15 min)**

1. Ecosistema: RUC, series activas, correlativos, salud de conexiones (CPE local, Woo, Yape/Izipay).
2. WooCommerce: estado `CONECTADO`; mapeos `DESFASADO` / `ERROR` en cero o con plan.
3. Dashboard / Reportes: stock bajo, pedidos `PendientePago` viejos, subastas `ACTIVA` que ya debieron cerrar.
4. Inventario: ojeada de Kardex del día anterior (ajustes raros, anulaciones).

**Durante el día**

- Autoriza altas de SKU delicados (secret rares, sellados de alto costo) y el `costo` correcto.
- Decide precio reserva de lives grandes y cancela subastas que no deben adjudicarse.
- Si Woo se cae: pasar recepción a `WEBHOOK`/`POLLING` según toque; no importar a mano el mismo id.
- Si CPE está `CAIDO`: se sigue despachando; se deja la venta en cola de emisión. No se “inventa” boleta en Excel.
- Usuarios: alta/baja, rotación de roles, webhooks WhatsApp.

**Al cerrar**

1. Reportes del día/mes: ventas por canal y franquicia.
2. Arqueo: Yape + Izipay + transferencia + efectivo vs pedidos cobrados. Conciliar `pendientesConciliar`.
3. Yield de aperturas del día: ¿se abrió a pérdida? ¿faltó costear el sellado?
4. Columna `PendientePago` > 24 h: cancelar (libera stock) o insistir cobro.
5. Exportar CSV si hay que mandar al contador.
6. Verificar que todo `Entregado` con `ventaId` tenga emisión (o quede explícitamente pendiente).

**No hace (salvo cobertura):** pujar en el live, empaquetar, ajustar stock sin motivo.

### 5.2 Cajero / Vendedor — cobro, subasta y Kanban

**Objetivo del día:** no vender lo que no está libre; no dejar pagos huérfanos; mover el Kanban.

**Al abrir**

1. Pedidos Digitales: columnas `PendientePago` y `Pagado`. Filtro por origen (Facebook / Woo / WhatsApp / tienda).
2. Pagos: bandeja `NOTIFICADO` (Yapes del grupo, Izipay).
3. Subastas: si hay live hoy, revisar borradores (precio base, incremento, reserva, sede correcta).

**Flujo WhatsApp / mostrador**

1. Buscar SKU; mirar **libre de la sede**, no el número del catálogo.
2. Crear pedido (cliente, teléfono, recojo o dirección). Si el sistema rechaza por libre, **no** se “anota en el cuaderno”.
3. Pedir voucher → Pagos: registrar Yape/Izipay con código de operación → asociar → confirmar.
4. Cuando el pedido salte a `Pagado`, avisar a Almacén (empaque).

**Flujo subasta Facebook / presencial**

1. Activar. Registrar pujas en caliente (nombre o alias, monto).
2. Al corte: cerrar. Si no llegó a reserva, no adjudicar.
3. Adjudicar: nace el pedido `PendientePago` ya reservado. Mandar datos de pago al ganador.
4. El cobro es el mismo de la bandeja de Pagos. No crear un segundo pedido “por si acaso”.

**Flujo recojo en tienda**

1. Pedido `Empaquetado` + `esRecojoTienda`.
2. Verificar identidad / código.
3. Confirmar entrega desde Entregas o transicionar a `Entregado` (el sistema convierte a venta y asienta `VENTA`).
4. Si pide boleta/factura, escalar a Admin (Ecosistema) con el pedido ya `Entregado`.

**Pagos difíciles**

- Código de operación repetido: no forzar; buscar el pago existente.
- Monto mayor al saldo: el sistema corta. Registrar el exacto o partir en dos orígenes.
- Pago sin pedido: dejarlo `NOTIFICADO` y conciliar; no confirmar a ciegas.

**Al cerrar**

- Cero `NOTIFICADO` digitales sin decisión (asociado, confirmado o rechazado).
- Subastas `CERRADA` con ganador: adjudicadas o documentadas.
- Entregar a Almacén la lista de `Pagado` sin empaque.

**No hace:** ajustar Kardex, confirmar aperturas, cambiar RUC/series, publicar Woo.

### 5.3 Encargado de almacén / Logística — físico, pull y despacho

**Objetivo del día:** que el físico coincida con `disponible`, que las reservas se puedan hallar, que lo pagado salga empaquetado.

**Al abrir**

1. Inventario de la sede: stock bajo (≤ 3) y `libre = 0` con `reservada > 0` (hay compromiso: no se reponen a la vitrina de venta).
2. Entregas: KPI `porEmpaquetar` / `porDespachar`.
3. Aperturas: borradores del día anterior; no dejar ETBs abiertos sin confirmar.

**Ingresos**

1. Compra o ajuste de entrada con motivo (`Guía 123`, `Inventario cíclico`).
2. Si el SKU es nuevo: darlo de alta **antes** (set, número, condición, foil, costo).
3. Avisar a Admin para sync Woo si el SKU está mapeado.

**Aperturas (pull session)**

1. Wizard paso 1: sede donde está el sellado (no abrir “de Surco” unidades que están en Miraflores).
2. Paso 2: escanear / teclear set + número; marcar foil y estado (`NM`/`EX`/`GD`). Si la carta no existe en catálogo, **crearla primero**.
3. Mirar la tarjeta de Yield **antes** de confirmar. Si el Yield es muy negativo, decidir con Admin si se sigue.
4. Confirmar. Verificar en Kardex: una salida de sellado y N ingresos de carta.
5. Ubicación física: las cartas nuevas van a binder/vitrina de esa sede.

**Empaque y despacho**

1. Tomar pedidos `Pagado`. Armar con la nota de empaque (mín. 3 caracteres: “toploader + bubble + guia”).
2. Imprimir packing slip (remitente por sede).
3. Courier: tracking + agencia + costo. Recojo: dejar en anaquel de “listos” y pasar a `PendienteEntrega`.
4. Confirmar entrega **solo** con evidencia (cargo, foto, recojo firmado). Eso dispara la venta.
5. Fallido: marcar `FALLIDA`, **no** devolver al stock. Cajero decide si se reenvía o se cancela el pedido.

**Qué no tocar**

- Cancelar pedidos (libera plata y stock: es de Cajero).
- Adjudicar subastas.
- Emitir CPE.
- Ajustar a negativo o “redondear” reservada.

**Al cerrar**

- Cero `Pagado` de días anteriores sin empaque, salvo bloqueo documentado.
- Borradores de apertura: confirmar o anular.
- Recuento rápido de SKUs de alto valor (secret, ETB). Cualquier hueco = `AJUSTE` con motivo, no silencio.

### 5.4 Rutina cruzada de un día típico (los tres roles)

```
08:30  Almacén  recuento + cola de empaque
08:45  Admin    salud Woo/CPE + desfasados
09:00  Cajero   bandeja Yape + WhatsApp
10:00  Cajero   live / subasta (si hay)
12:00  Almacén  pull de ETB (Yield a la vista)
13:00  Admin    sync Woo post-reservas de la mañana
16:00  Almacén  Olva / Shalom / recojos
18:00  Cajero   confirmar recojos de mostrador
19:00  Admin    arqueo + reportes + emisiones pendientes
```

---

## 6. Permisos, rutas y checklist de cierre

### 6.1 Rutas de trabajo

| Ruta | Uso diario |
|------|------------|
| `/app/dashboard` | Arranque de turno |
| `/app/productos-tcg` | Alta SKU, BOM, estado Woo |
| `/app/inventario` | Stock por sede, ajustes, armado |
| `/app/inventario/kardex` | Auditoría; nunca para “corregir celdas” |
| `/app/aperturas-tcg` | Wizard + historial de Yield |
| `/app/subastas-tcg` | Tablero; `/:id` pujas en vivo |
| `/app/pedidos-digitales` | Kanban omnicanal |
| `/app/pagos` | Conciliación Yape/Izipay |
| `/app/entregas` | Empaque, tracking, packing slip, confirmar |
| `/app/woocommerce` | Conexión, mapeos, sync, logs, cola |
| `/app/reportes` | KPIs, canales, franquicia, yield, arqueo, CSV |
| `/app/ecosistema` | Fiscal, series, sedes, usuarios, webhooks, emitir CPE |

### 6.2 Transiciones que el software bloquea (memorizar)

- Pedido: no se salta de `PendientePago` a `Empaquetado` (hay que cobrar).
- Pedido: no se cancela `Entregado`.
- Recojo: se puede entregar desde `Empaquetado`; courier no.
- Pago: no confirmar sin asociar si ya se eligió pedido; no rechazar `CONFIRMADO`; no superar el total.
- Subasta: no pujar si no está `ACTIVA`; no adjudicar sin ganadora ≥ reserva; no cancelar si el pedido ya no está `PendientePago`.
- Apertura: no confirmar sin líneas; no editar si no es `BORRADOR`; no anular confirmada con cartas ya comprometidas.
- CPE: no emitir sin RUC; factura exige documento `Entregado`; guía exige despacho.

### 6.3 Checklist de cierre diario (una hoja)

- [ ] Libre ≥ 0 en todas las filas de stock (el sistema no debería permitirlo; si se ve, hay bug y se escala).
- [ ] Cada tarjeta Kanban `Reservado` tiene asiento `RESERVA` o `PUJA_GANADORA_RESERVA`.
- [ ] Cada `Cancelado` del día tiene `LIBERACION_RESERVA`.
- [ ] Cada `Entregado` del día tiene `VENTA` y `ventaId`.
- [ ] Suma de pagos `CONFIRMADO` del día = arqueo de Reportes.
- [ ] Woo: 0 errores de sync o con ticket; desfasados revisados.
- [ ] Aperturas: 0 borradores olvidados; Yield del día revisado.
- [ ] Subastas: 0 `ACTIVA` fuera de horario sin dueño.
- [ ] Comprobantes: cola de `Entregado` sin CPE identificada (no necesariamente emitida el mismo día, pero listada).

### 6.4 Relación con la planificación

Este manual es la lectura operativa de `docs/PLANIFICACION_INICIAL.md` más el comportamiento real de:

- `frontend/src/app/features/*/models/`
- `frontend/src/app/features/*/data-access/`
- Stores de inventario (`InventarioStore.transaccionar`)

Cuando se implementen migraciones EF Core y controladores .NET, las reglas de las secciones 2 y 4 deben copiarse al servidor: el frontend prototipo es la especificación ejecutable del negocio TCG de Trunqi, no un mock descartable de pantallas.

---

*Documento generado a partir de la inspección de la solución SistColeccionables (planificación inicial + features Angular módulos 1–11).*
