# Capacitación de entrega — CapitalPOS Trunqi TCG

**Cliente:** Katherina · Trunqi  
**Formato:** Google Meet · 40 minutos · pantalla compartida  
**Rol de quien capacita:** Lead de implementación POS (no sesión técnica de código)  
**Objetivo:** que Katherina salga entendiendo *qué es el sistema de verdad*, *cómo opera un día real* y *qué hace cada persona del equipo* — sin Excel paralelo y sin vender la misma carta dos veces.

---

## 0. Antes de entrar a la Meet (checklist del capacitador)

Hacer esto 15 minutos antes. Si algo falla, avisar al minuto 1; no improvisar a ciegas.

- [ ] Navegador limpio, notificaciones silenciadas, Meet con cámara + pantalla.
- [ ] Sesión ya logueada en CapitalPOS (usuario Admin de demostración).
- [ ] Caja **abierta** en la sede que vas a usar (Miraflores). Sin caja abierta no se confirma venta.
- [ ] Tener a mano 1 SKU de carta, 1 sellado con apertura permitida y 1 pedido de ejemplo.
- [ ] WhatsApp / Zoom / Meet: pedir a Katherina que también entre desde computadora (el POS no se opera bien desde el celular).
- [ ] Grabar la sesión (pedir permiso al inicio). Mandar el recorte de 5–8 min del flujo feliz al día siguiente.
- [ ] Tener este documento en un segundo monitor. No leerlo en voz alta: es tu teleprompter.

**Regla de oro de la Meet:** demostrar, no explicar. Cada bloque cierra con “esto es lo que tu equipo hace mañana”.

---

## 1. Guía rápida (para Katherina — se puede reenviar después)

### 1.1 Qué es y qué no es

| Es | No es |
|----|--------|
| El sistema de verdad del stock, las reservas, los cobros, el empaque, la caja y SUNAT | La tienda web (eso sigue siendo WooCommerce) |
| Un tablero único para Facebook, WhatsApp, tienda y web | Un Excel más bonito |
| Stock **por sede** (Miraflores ≠ Surco) | Un número único “de inventario” para toda la empresa |

Frase para anclar:

> *WooCommerce vende. CapitalPOS decide si hay carta para vender.*

### 1.2 Las 3 ideas que no se negocian

1. **Catálogo de empresa, stock de sede.** Una carta existe una vez. Su cantidad vive en Miraflores o en Surco.
2. **Solo se vende stock libre.** Libre = lo físico menos lo ya comprometido (pedido, subasta, web). Lo que está reservado sigue en la vitrina, pero **ya no se puede volver a vender**.
3. **Nada se borra del historial.** Si te equivocas, se anula o se ajusta con motivo. El Kardex es un libro, no una planilla editable.

### 1.3 Mapa mental del día

```
Catálogo → Ingreso a sede → (opcional) Abrir sellado
        → Demanda por UNA sola puerta: Subasta  |  WhatsApp/mostrador  |  Woo
        → Reserva de stock libre
        → Cobro (Yape / Izipay / efectivo)
        → Empaque → Recojo o courier
        → Confirmación = Venta + baja de stock
        → Boleta/factura SUNAT
        → Cierre de caja
```

### 1.4 Los 12 módulos (cómo hablarlos con el equipo)

| Módulo | Para qué sirve en la operación | Quién lo usa de verdad |
|--------|--------------------------------|------------------------|
| Dashboard | Foto del turno: ventas, stock bajo, subastas vivas | Admin al abrir |
| Productos TCG | Alta de cartas, sellado, accesorios. NM foil ≠ NM no-foil | Admin / Almacén |
| Inventario | Stock por sede + ajustes con motivo | Almacén |
| Kardex | Historial de cada movimiento. Solo se lee | Admin / Almacén |
| Aperturas TCG | Abrir ETB/sobre → salen cartas + Yield | Almacén + Admin |
| Subastas TCG | Live Facebook / web / presencial → pedido reservado | Cajero |
| Pedidos Digitales | Kanban único de todos los canales | Cajero |
| Pagos | Conciliar Yape / Izipay / efectivo | Cajero |
| Entregas | Empaque, tracking, recojo, packing slip | Almacén / Cajero |
| WooCommerce | Publicar stock libre a la web e importar pedidos | Admin |
| Reportes | Ventas por canal, franquicia, yield, arqueo | Admin |
| Ecosistema | RUC, series, usuarios, emitir boleta/factura | Admin |
| Caja | Abrir turno, ventas del turno, cierre y ticket 80 mm | Cajero / Admin |

### 1.5 Stock en una frase

| Número | Significado | Pregunta que responde |
|--------|-------------|------------------------|
| Disponible | Lo que está físicamente en la sede | ¿Está en la vitrina / anaquel? |
| Reservado | Comprometido, aún no entregado | ¿Ya se lo prometimos a alguien? |
| **Libre** | Disponible − Reservado | **¿Puedo venderlo ahora?** |

Si Libre = 0, el sistema debe rechazar: nuevo pedido, nueva subasta y publicación web.

### 1.6 Kanban (el corazón del día)

```
PendientePago → Pagado → Empaquetado → PendienteEntrega → Entregado
                      ↘ Cancelado (libera el stock)
```

- No se empaqueta sin cobrar.
- Recojo en tienda: de Empaquetado se puede pasar a Entregado (el cliente se lo lleva).
- Courier: hay que despachar con tracking antes de confirmar.
- **Entregado no se cancela.** Ahí ya es venta.

Indicador de la tarjeta:

- **Reservado** — hay unidad comprometida.
- **Liberado** — se canceló; vuelve a estar vendible.
- **Confirmado** — ya salió; no hay vuelta atrás por cancelación.

### 1.7 Quién hace qué (roles)

| | Admin (Katherina) | Cajero / vendedor | Almacén |
|--|-------------------|-------------------|---------|
| Alta de SKU / costo | Sí | No | Sí |
| Ajuste de stock | Sí | No | Sí |
| Subasta y pujas | Supervisa | Opera el live | No |
| Crear pedido / cobrar | Sí | Día a día | No |
| Empacar y despachar | Cobertura | Recojos | Día a día |
| Abrir sellado (Yield) | Decide si conviene | No | Ejecuta |
| WooCommerce / SUNAT | Sí | No | No |
| Cerrar caja | Revisa | Ejecuta | No |

### 1.8 Lo que el sistema bloquea (para no pelear con la pantalla)

- Vender sin stock libre.
- Empaquetar un pedido que aún no está pagado.
- Confirmar venta **sin caja abierta** en esa sede.
- Cancelar un pedido ya Entregado.
- Adjudicar una subasta si no hay 1 unidad libre en **esa** sede.
- Abrir un sellado que ya está reservado o no tiene libre.
- Factura sin RUC del cliente. Boleta con DNI o RUC.
- Código de operación Yape/Izipay repetido.
- Cobrar de más que el total del pedido.

### 1.9 Día típico (rutina de 30 segundos)

| Hora | Quién | Qué mira |
|------|-------|----------|
| Apertura | Cajero | Abre caja. Bandeja de Yapes. Columna PendientePago. |
| Apertura | Almacén | Cola de empaque. Stock bajo. Borradores de apertura. |
| Apertura | Admin | Woo conectado. Desfasados. Subastas que debieron cerrar. |
| Durante | Cajero | WhatsApp / live / recojos. |
| Durante | Almacén | Pull de ETB (mirar Yield **antes** de confirmar). Olva / Shalom. |
| Cierre | Admin | Arqueo vs caja. Pedidos PendientePago > 24 h (cobrar o cancelar). Emisiones SUNAT pendientes. |

### 1.10 Frases listas para el equipo (WhatsApp interno)

- “No anotes en el cuaderno: si el POS dice que no hay libre, no hay.”
- “NM foil y NM no-foil son dos productos distintos.”
- “El live reserva al adjudicar. No crees un segundo pedido ‘por si acaso’.”
- “Un Yape sin código de operación no se confirma.”
- “Despacho fallido no devuelve el stock. Primero se decide: reenvío o cancelación.”
- “Si Woo está desfasado, la web puede vender de más. Se sincroniza, no se ignora.”

---

## 2. Guion estructurado — 40 minutos

Reloj en pantalla. Si un bloque se alarga, recortar el de Reportes, no el de stock libre ni el de Kanban.

### Minuto 0–3 · Apertura y contrato de la sesión

**Qué haces:** cámara, nombre, agenda en 4 viñetas. Pedir grabación.

**Qué dices (casi textual):**

> Katherina, gracias. Estos 40 minutos no son un tour de botones: es la entrega operativa de CapitalPOS para Trunqi. Al terminar quiero que queden claros tres puntos: (1) el POS es la verdad del stock, no la web; (2) cómo corre un pedido desde WhatsApp o Facebook hasta que el cliente se lleva la carta; (3) qué hace tu rol de dueña vs cajero vs almacén. Si algo no calza con cómo trabajan hoy, lo anotamos y no lo disfrazamos.

**Agenda que muestras (escribe en el chat de Meet):**

1. Cómo piensa el sistema (3 ideas).
2. Catálogo + stock por sede.
3. Flujo feliz: pedido → cobro → entrega → boleta.
4. Subasta Facebook y apertura de sellado.
5. Web, caja y cierre del día.
6. Preguntas y siguientes pasos.

**Pregunta de anclaje (1 sola):**

> Hoy, si alguien gana una carta en el live de Facebook y a la vez otro la pide por WhatsApp, ¿cómo evitan venderla dos veces?

Escucha 20 segundos. No corrijas todavía. Esa respuesta es el gancho del bloque siguiente.

---

### Minuto 3–8 · Modelo mental (sin pantallas profundas)

**Qué haces:** diagrama simple en pizarra Meet o slide de 1 página. No entres aún a módulos.

**Qué dices:**

> Trunqi vende por tres puertas: tienda, subasta (Facebook / presencial / web) y WooCommerce. Las tres puertas miran **el mismo stock libre**. Si una reserva, las otras ya no pueden tomarla. Por eso no hay un Kanban de Woo, otro de Facebook y un Excel: hay un solo tablero de Pedidos Digitales.

**Las 3 ideas (lento, con pausa):**

1. El catálogo es de Trunqi. El stock es de **Miraflores** o de **Surco**. No se abre un ETB de Surco como si estuviera en Mira.
2. Disponible ≠ Libre. Disponible es lo físico. Libre es lo vendible. Una carta reservada sigue en la vitrina y **no** se ofrece.
3. El historial no se edita. Equivocación = anular, ajustar con motivo, o nota de crédito. Nunca “corregir la celda”.

**Cierre del bloque:**

> Si el equipo internaliza esto, el resto de pantallas se entiende solo. Si no, van a pelear con el sistema y van a volver al cuaderno.

---

### Minuto 8–14 · Demo 1 — Productos e Inventario

**Pantalla:** Productos TCG → Inventario (sede Miraflores).

**Demo (máximo 5 clics conscientes):**

1. Abre una carta. Señala: juego, set, número, condición, foil, idioma, SKU, precio **con IGV**, costo.
2. Di en voz alta: *“Pikachu NM foil y Pikachu NM no-foil son dos SKU. Si los mezclan, el yield y la web mienten.”*
3. Pasa a Inventario. Muestra Disponible / Reservado / Libre de esa carta en Mira vs Surco.
4. (Opcional, 30 s) Un ajuste de entrada con motivo visible: “Ingreso guía 123”. Enseña que el Kardex **agregó una línea**, no editó la anterior.

**Qué no hagas aquí:** no crees un compuesto, no expliques TPT, no hables de API.

**Pregunta de cierre:**

> Cuando cuenten vitrina esta noche, ¿qué número van a mirar: el del catálogo o el Libre de la sede?

Respuesta correcta: **Libre de la sede**. El del catálogo es espejo para la web.

---

### Minuto 14–22 · Demo 2 — El flujo feliz (el bloque que no se recorta)

Este es el corazón de la capacitación. Reloj: 8 minutos. Un solo pedido de punta a punta.

**Historia que narras:**

> Llega un WhatsApp: “¿Tienes esta carta NM?”. Cajero mira Libre en Mira. Hay 1. Crea el pedido. El stock queda Reservado. Llega el Yape. Se asocia y se confirma. Almacén empaca. El cliente recoje. Recién ahí baja el físico y nace la venta. Después, boleta.

**Pantalla, en este orden:**

1. **Caja** — confirma que el turno está abierto. Si no, ábrelo ahora y explica: *sin caja abierta el POS no confirma venta. Es a propósito.*
2. **Pedidos Digitales** — crear pedido: cliente, teléfono, sede Mira, canal WhatsApp, 1 línea, recojo en tienda.
3. Señala la tarjeta: estado **PendientePago**, indicador **Reservado**. Vuelve 3 segundos a Inventario: Reservado +1, Libre −1.
4. **Pagos** — registrar Yape con código de operación → asociar al pedido → confirmar. El Kanban salta a **Pagado**.
5. **Empaque** — nota de empaque (“toploader + sobre”). Estado **Empaquetado**.
6. **Entrega / recojo** — confirmar. Estado **Entregado**. Inventario: Disponible baja, Reservado baja, Libre sigue en 0 (ya no está).
7. **Ecosistema** — emitir boleta desde esa venta. Series B001. Cliente con DNI.

**Frases de ancla mientras demuestras:**

- “No saltamos de PendientePago a Empaquetado. Primero el dinero.”
- “El costo de envío (Olva/Shalom) es informativo: no se mete al IGV del pedido.”
- “Si cancelamos antes de entregar, el stock vuelve a Libre y Woo puede volver a venderlo. Si ya entregamos, no.”

**Si el tiempo aprieta:** omite la emisión SUNAT y di: *la boleta se dispara cuando ya hay venta; no se inventa en Excel si SUNAT está caído — queda en cola.*

---

### Minuto 22–28 · Demo 3 — Subasta Facebook (caso Trunqi)

**Pantalla:** Subastas TCG.

**Historia:**

> Live de Facebook. Activas. Registras pujas. Cierras. Si llegó al precio reserva, adjudicas. El sistema reserva 1 unidad en la sede del live y **crea solo** el pedido PendientePago. El cobro es el mismo Kanban de siempre.

**Demo corta:**

1. Subasta en Borrador: producto, sede Mira, precio base, incremento, reserva opcional, canal Facebook.
2. Activar → una o dos pujas (nombres de postores reales de ejemplo, no “test1”).
3. Cerrar → Adjudicar.
4. Saltar a Pedidos: nació la tarjeta. **No** crear un segundo pedido.

**Tres reglas para el live (diles que las impriman):**

1. Se puja solo si está **Activa**.
2. Si no llega a reserva, no se adjudica. La carta sigue libre.
3. Si el ganador no paga, se cancela el pedido **PendientePago** y el stock se libera. Si ya avanzó el Kanban, no se cancela la subasta a la fuerza: lo resuelve Cajero en el pedido.

---

### Minuto 28–32 · Demo 4 — Apertura de sellado + Yield

**Pantalla:** Aperturas TCG (wizard de 3 pasos). No abras 10 cartas: 2 líneas bastan.

**Qué dices:**

> Cuando abren un ETB, el sellado sale del stock y entran las cartas a la misma sede. El Yield responde: ¿lo que salió vale más o menos que lo que costó el ETB? Si el sellado no tiene costo cargado, el Yield miente (usa el precio de venta como respaldo). Por eso Almacén carga el costo en la compra.

**Wizard:**

1. Sede + sellado con “permite apertura” + cantidad 1.
2. Cartas: set + número + NM/EX/GD + foil. Si la carta no existe en catálogo, **se crea primero** — no se confirma a ciegas.
3. Mirar la tarjeta de Yield **antes** de Confirmar. Confirmar.

**Regla dura:**

> Si confirman mal, se anula (el sistema escribe el movimiento inverso). No se puede anular si ya vendieron o reservaron esas cartas. Eso protege el libro.

---

### Minuto 32–36 · Woo, caja y cierre del día

**Pantalla:** WooCommerce (30–40 s) → Caja (1 min) → Reportes o checklist verbal.

**WooCommerce — tres frases, no un tour:**

1. Se publica el stock **libre** de la sede origen (Miraflores), no el físico total.
2. Un pedido de la web entra al **mismo** Kanban. No hay tablero paralelo.
3. Estado **Desfasado** = riesgo de overselling. Se sincroniza el mismo día, no “mañana”.

**Caja:**

> Se abre al empezar el turno con el fondo. Las ventas del turno cuelgan de esa sesión. Al cierre: conteo físico vs teórico. Sobrante, faltante o cuadrado. Ticket 80 mm. Si la caja está cerrada, el POS no deja confirmar entregas: es el candado anti “vendimos sin turno”.

**Cierre diario de dueña (léelo como checklist, no como amenaza):**

- [ ] PendientePago de ayer: se cobra o se cancela (libera stock).
- [ ] Cero Yapes notificados sin decisión.
- [ ] Empaques de lo Pagado.
- [ ] Yield del día: ¿se abrió a pérdida? ¿faltó el costo del sellado?
- [ ] Woo: cero desfasados ignorados.
- [ ] Entregados del día: venta + boleta (o lista explícita de pendientes SUNAT).
- [ ] Caja cerrada y ticket guardado.

---

### Minuto 36–40 · Preguntas, acuerdos y cierre

**Qué haces:** deja de compartir un momento, mira a cámara. Máximo 2 preguntas profundas, no 8 superficiales.

**Preguntas que tú lanzas si hay silencio:**

1. ¿Quién va a ser el cajero dueño del Kanban y quién el de almacén? (nombres, no “el equipo”).
2. ¿El live de Facebook lo opera una sola persona o se turnan? El sistema asume un operador por subasta.
3. ¿Quieres que el recuento de vitrina de la próxima semana se haga ya sobre Libre de sede?

**Acuerdos a dejar por escrito en el chat de Meet (copiar/pegar):**

```
Acuerdos sesión CapitalPOS — Trunqi (fecha)
1. El POS es la verdad del stock. No se vende fuera del sistema.
2. Roles: Admin = Katherina. Cajero = ___. Almacén = ___.
3. Primera semana: un canal piloto (WhatsApp o Facebook) + recojo Mira.
   Woo y courier Olva/Shalom se suman cuando el Kanban ya es hábito.
4. Dudas operativas: se anotan aquí y se resuelven en la sesión de seguimiento.
5. Grabación + esta guía rápida se envían hoy.
```

**Cierre textual:**

> Katherina, el sistema ya está pensado para cómo vende Trunqi: live, WhatsApp, tienda y web, con stock que no se pisa. El éxito de la entrega no es “conocer los 12 módulos”: es que mañana el primer WhatsApp entre por el Kanban y no por el cuaderno. Cualquier traba de ese primer flujo me la escribes el mismo día.

**Despedida:** confirmar fecha de seguimiento (sugerido: 20–25 min a los 3–4 días, solo dudas reales del piloto).

---

## 3. Plan B de tiempo (si se atrasa)

| Si vas… | Recorta | Nunca recortes |
|---------|---------|----------------|
| 5 min tarde | Yield (déjalo en 2 frases) | Stock libre + Kanban |
| 10 min tarde | Woo + Reportes (solo el checklist) | Flujo feliz WhatsApp |
| Cliente quiere “ver todo” | Ofrece una 2.ª sesión de 25 min para Almacén (aperturas + empaque) | No aceleres pujas + SUNAT + Woo en el mismo bloque |

Si Katherina se va por una anécdota de un cliente: anota, di “lo vemos en el flujo”, y vuelve al reloj.

---

## 4. Objeciones frecuentes (respuesta de consultor, 20 segundos)

**“Es más lento que el cuaderno.”**  
La primera semana sí. La tercera, no: dejan de buscar la carta en tres chats y de pelear overselling. El costo del cuaderno es la carta vendida dos veces.

**“¿Y si Woo ya cobró?”**  
Entra al Kanban ya en Pagado, con stock reservado. A partir de ahí es el mismo empaque.

**“El cliente pagó de más / en dos Yapes.”**  
Se registran dos pagos. La suma no puede pasar el total. No se “redondea” confirmando de más.

**“El courier no entregó.”**  
Se marca fallida. **No** vuelve al stock sola. Cajero decide reenvío o cancelación (ahí sí se libera).

**“SUNAT está caído.”**  
Se sigue despachando. La venta queda. La boleta se emite cuando el servicio responde. No se fabrica un PDF paralelo como verdad.

**“¿Puedo editar el Kardex?”**  
No. Ajuste con motivo o anulación. Si alguien pide “corregir la fila”, es una bandera roja de proceso.

**“¿Puedo usar el celular en el live?”**  
Para pujar, computadora. El celular es para mirar, no para operar el tablero.

---

## 5. Qué no decir en esta Meet

- No nombres de tablas, sprints, JWT, PostgreSQL ni “está in-memory”.
- No “es un prototipo”. Habla de **sistema en entrega** y de **siguiente oleada** si hace falta (kits compuestos, compras formales, dashboard más rico).
- No prometas fecha de features que no están en el alcance de hoy.
- No culpes al equipo actual por el Excel. Sustituye el hábito, no a la persona.
- No hagas un recuento de 12 módulos “porque están en el menú”. El menú intimida; el flujo enseña.

Si pregunta por lo que aún se está endureciendo (pantallas de pagos/entregas/reportes más nativas, kits, compras):

> Está en el mapa. El piloto de esta semana no depende de eso: depende de catálogo, stock libre, Kanban, cobro, recojo, caja y boleta.

---

## 6. Material para pegar en el chat al minuto 1

```
Hoy: entrega operativa CapitalPOS — Trunqi (40 min)

1. El POS es la verdad del stock (no la web).
2. Solo se vende stock LIBRE por sede (Mira / Surco).
3. Un solo Kanban: Facebook, WhatsApp, tienda y Woo.
4. Flujo: reservar → cobrar → empacar → entregar → boleta → cerrar caja.

Regla de la semana: nada se vende fuera del sistema.
```

---

## 7. Después de la Meet (tú, el mismo día)

- [ ] Enviar esta guía (sección 1) + grabación.
- [ ] Completar los nombres de Cajero y Almacén en los acuerdos.
- [ ] Agendar seguimiento de 20 min.
- [ ] Anotar objeciones reales de Katherina (son insumo de implementación, no “resistencia”).
- [ ] Si hubo un bloqueo en la demo, reproducirlo en frío y escribir la causa en una línea: proceso, dato o pantalla.

---

*Uso: teleprompter de la sesión de entrega. No sustituye el manual operativo interno (`docs/MANUAL_OPERATIVO.md`). No contiene código.*
