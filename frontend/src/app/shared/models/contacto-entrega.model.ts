/** Catálogo compartido de punto de entrega y canal de contacto del cliente. */

export type CanalContactoCliente =
  | 'WHATSAPP'
  | 'FACEBOOK'
  | 'INSTAGRAM'
  //| 'TELEFONO'
  //| 'TELEGRAM'
  | 'MSN'
  | 'OTRO';

export const CANALES_CONTACTO: readonly CanalContactoCliente[] = [
  'WHATSAPP',
  'FACEBOOK',
  'INSTAGRAM',
  //'TELEFONO',
  //'TELEGRAM',
  'MSN',
  'OTRO',
];

export const ETIQUETAS_CANAL_CONTACTO: Record<CanalContactoCliente, string> = {
  WHATSAPP: 'WhatsApp',
  FACEBOOK: 'Facebook',
  INSTAGRAM: 'Instagram',
  //TELEFONO: 'Teléfono',
  //TELEGRAM: 'Telegram',
  MSN: 'Msn',
  OTRO: 'Otro',
};

/** Valores base sugeridos; se combinan con puntos ya usados en clientes. */
export const PUNTOS_ENTREGA_SUGERIDOS = [
  'Papurris',
  'Dapkris',
  'TCG House',
  'Recojo en tienda',
] as const;

const STORAGE_PUNTOS_ENTREGA = 'capitalpos.puntosEntregaPreferidos';

export function normalizarPuntoEntrega(valor: string | null | undefined): string {
  return (valor ?? '').trim().replace(/\s+/g, ' ').slice(0, 80);
}

/** Une sugerencias fijas, puntos del API y extras locales (alta al vuelo). */
export function combinarPuntosEntrega(
  ...fuentes: ReadonlyArray<readonly string[] | string[] | null | undefined>
): string[] {
  const vistos = new Map<string, string>();
  for (const sugerido of PUNTOS_ENTREGA_SUGERIDOS) {
    vistos.set(sugerido.toLowerCase(), sugerido);
  }
  for (const fuente of fuentes) {
    for (const crudo of fuente ?? []) {
      const punto = normalizarPuntoEntrega(crudo);
      if (!punto) {
        continue;
      }
      const clave = punto.toLowerCase();
      if (!vistos.has(clave)) {
        vistos.set(clave, punto);
      }
    }
  }
  return [...vistos.values()].sort((a, b) => a.localeCompare(b, 'es', { sensitivity: 'base' }));
}

export function leerPuntosEntregaLocales(): string[] {
  try {
    const raw = localStorage.getItem(STORAGE_PUNTOS_ENTREGA);
    if (!raw) {
      return [];
    }
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) {
      return [];
    }
    return parsed
      .filter((item): item is string => typeof item === 'string')
      .map(normalizarPuntoEntrega)
      .filter(Boolean);
  } catch {
    return [];
  }
}

/** Persiste un punto nuevo para próximos formularios (mismo navegador). */
export function registrarPuntoEntregaLocal(valor: string | null | undefined): string[] {
  const punto = normalizarPuntoEntrega(valor);
  if (!punto) {
    return leerPuntosEntregaLocales();
  }
  const actualizados = combinarPuntosEntrega(leerPuntosEntregaLocales(), [punto]).filter(
    (item) =>
      !PUNTOS_ENTREGA_SUGERIDOS.some((base) => base.toLowerCase() === item.toLowerCase()),
  );
  try {
    localStorage.setItem(STORAGE_PUNTOS_ENTREGA, JSON.stringify(actualizados));
  } catch {
    /* ignore quota / private mode */
  }
  return actualizados;
}

export function etiquetaCanalContacto(canal: CanalContactoCliente | null | undefined): string {
  if (!canal) {
    return '—';
  }
  return ETIQUETAS_CANAL_CONTACTO[canal] ?? canal;
}

export function etiquetaPuntoEntrega(punto: string | null | undefined): string {
  const texto = punto?.trim() ?? '';
  return texto || '—';
}
