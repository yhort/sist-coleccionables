const UUID =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function pareceUuid(valor: string | null | undefined): boolean {
  return !!valor && UUID.test(valor.trim());
}

export interface CodigoAmigableFuente {
  id?: string | null;
  codigo?: string | null;
  numeroPedido?: string | null;
  correlativo?: number | string | null;
}

export function codigoAmigable(
  id: string | null | undefined,
  opciones?: {
    codigo?: string | null;
    numeroPedido?: string | null;
    correlativo?: number | string | null;
    prefijo?: string;
    longitud?: number;
  },
): string {
  const propio = primerCodigoPropio(opciones?.codigo, opciones?.numeroPedido);
  if (propio) {
    return propio;
  }

  const correlativo = normalizarCorrelativo(opciones?.correlativo);
  if (correlativo !== null) {
    return `${(opciones?.prefijo ?? 'PED').toUpperCase()}-${String(correlativo).padStart(5, '0')}`;
  }

  const texto = id?.trim() ?? '';
  if (!texto) {
    return '—';
  }
  if (!pareceUuid(texto)) {
    return texto;
  }

  const prefijo = (opciones?.prefijo ?? 'PED').toUpperCase();
  const longitud = opciones?.longitud ?? 6;
  const hex = texto.replaceAll('-', '').slice(0, longitud).toUpperCase();
  return `#${prefijo}-${hex}`;
}

export function codigoPedido(fuente: CodigoAmigableFuente | string | null | undefined): string {
  if (typeof fuente === 'string' || fuente == null) {
    return codigoAmigable(fuente, { prefijo: 'PED', longitud: 6 });
  }
  return codigoAmigable(fuente.id, {
    codigo: fuente.codigo,
    numeroPedido: fuente.numeroPedido,
    correlativo: fuente.correlativo,
    prefijo: 'PED',
    longitud: 6,
  });
}

export function codigoSubasta(id: string | null | undefined): string {
  return codigoAmigable(id, { prefijo: 'SUB', longitud: 4 });
}

export function codigoVenta(id: string | null | undefined): string {
  return codigoAmigable(id, { prefijo: 'VEN', longitud: 6 });
}

export function codigoItem(id: string | null | undefined): string {
  return codigoAmigable(id, { prefijo: 'ITM', longitud: 6 });
}

function primerCodigoPropio(...valores: Array<string | null | undefined>): string | null {
  for (const valor of valores) {
    const texto = valor?.trim() ?? '';
    if (texto && !pareceUuid(texto)) {
      return texto;
    }
  }
  return null;
}

function normalizarCorrelativo(valor: number | string | null | undefined): number | null {
  if (valor == null) {
    return null;
  }
  const numero = typeof valor === 'number' ? valor : Number(String(valor).trim());
  if (!Number.isInteger(numero) || numero <= 0) {
    return null;
  }
  return numero;
}
