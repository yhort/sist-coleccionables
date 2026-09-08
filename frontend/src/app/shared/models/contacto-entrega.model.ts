/** Catálogo compartido de punto de entrega y canal de contacto del cliente. */

export type CanalContactoCliente =
  | 'WHATSAPP'
  | 'FACEBOOK'
  | 'INSTAGRAM'
  | 'TELEFONO'
  | 'TELEGRAM'
  | 'OTRO';

export const CANALES_CONTACTO: readonly CanalContactoCliente[] = [
  'WHATSAPP',
  'FACEBOOK',
  'INSTAGRAM',
  'TELEFONO',
  'TELEGRAM',
  'OTRO',
];

export const ETIQUETAS_CANAL_CONTACTO: Record<CanalContactoCliente, string> = {
  WHATSAPP: 'WhatsApp',
  FACEBOOK: 'Facebook',
  INSTAGRAM: 'Instagram',
  TELEFONO: 'Teléfono',
  TELEGRAM: 'Telegram',
  OTRO: 'Otro',
};

/** Valores sugeridos; también se acepta texto libre. */
export const PUNTOS_ENTREGA_SUGERIDOS = [
  'Papurris',
  'Dapkris',
  'TCG House',
  'Recojo en tienda',
] as const;

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
