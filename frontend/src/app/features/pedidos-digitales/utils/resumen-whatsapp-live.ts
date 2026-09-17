import { PedidoDigital, round2 } from '../models/pedido-digital.model';

function montoWhatsapp(valor: number): string {
  return `S/ ${round2(valor).toFixed(2)}`;
}

export function construirResumenWhatsAppLive(pedidos: readonly PedidoDigital[]): string {
  const nombre = pedidos[0]?.clienteNombre.trim() || 'cliente';
  const lineasCartas: string[] = [];
  for (const pedido of pedidos) {
    for (const detalle of pedido.detalles) {
      const carta = detalle.descripcion.trim() || 'Carta';
      const etiqueta = detalle.cantidad > 1 ? `${carta} x${detalle.cantidad}` : carta;
      lineasCartas.push(`- ${etiqueta} (${montoWhatsapp(detalle.total)})`);
    }
  }

  const total = round2(pedidos.reduce((sum, pedido) => sum + pedido.total, 0));
  return [
    `¡Hola ${nombre}! Gracias por participar en nuestro Live.`,
    'Aquí tienes el detalle de tus cartas adjudicadas:',
    ...lineasCartas,
    `*Total a pagar:* ${montoWhatsapp(total)}`,
    '*Medios de pago:* Yape / Plin / Transferencia',
    'Por favor envíanos la captura de tu pago por aquí para procesar tu pedido. ¡Muchas gracias!',
  ].join('\n');
}

export async function copiarAlPortapapeles(texto: string): Promise<boolean> {
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(texto);
      return true;
    }
  } catch {
    /* fallback */
  }
  const area = document.createElement('textarea');
  area.value = texto;
  area.setAttribute('readonly', '');
  area.style.position = 'fixed';
  area.style.left = '-9999px';
  document.body.appendChild(area);
  area.select();
  const ok = document.execCommand('copy');
  area.remove();
  return ok;
}
