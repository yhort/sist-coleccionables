/** Código ISO de la moneda local (Perú). */
export const MONEDA_CODIGO = 'PEN';

/**
 * Prefijo visible en UI.
 * Evita el código ISO "PEN" que produce CurrencyPipe con `symbol-narrow`.
 */
export const SIMBOLO_SOL = 'S/ ';

/** Dígitos por defecto: siempre 2 decimales. */
export const MONEDA_DIGITOS = '1.2-2';

/**
 * Formatea un monto en soles para uso en TypeScript (fuera de plantillas).
 * Ej.: formatearSoles(4928.79) → "S/ 4,928.79"
 */
export function formatearSoles(valor: number | null | undefined): string {
  const n = valor == null || Number.isNaN(Number(valor)) ? 0 : Number(valor);
  const numero = new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n);
  return `${SIMBOLO_SOL}${numero}`;
}
