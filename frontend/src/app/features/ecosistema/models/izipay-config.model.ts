export type ModoIzipay = 'TEST' | 'PROD';

export interface IntegracionIzipay {
  id: string;
  shopId: string;
  hmacSha256Enmascarada: string;
  tieneHmac: boolean;
  modo: ModoIzipay;
  activa: boolean;
}

export interface GuardarIntegracionIzipay {
  shopId: string;
  hmacSha256Clave?: string;
  modo: ModoIzipay;
  activa: boolean;
}

export function urlNotificacionIpnIzipay(empresaId: string, apiBaseUrl: string, origin: string): string {
  const path = `pagos/izipay/ipn/${empresaId}`;
  const base = apiBaseUrl.replace(/\/$/, '');
  if (/^https?:\/\//i.test(base)) {
    return `${base}/${path}`;
  }
  const sufijo = `${base}/${path}`.replace(/\/{2,}/g, '/');
  return `${origin.replace(/\/$/, '')}${sufijo.startsWith('/') ? sufijo : `/${sufijo}`}`;
}
