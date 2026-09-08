import { HttpErrorResponse } from '@angular/common/http';

export function readApiError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as
      | { detail?: string; title?: string; message?: string }
      | string
      | null;
    if (body && typeof body === 'object') {
      const texto = [body.message, body.detail, body.title].find(
        (valor) => typeof valor === 'string' && valor.trim().length > 0,
      );
      if (texto) {
        return texto.trim();
      }
    }
    if (typeof body === 'string' && body.trim()) {
      return extraerMensajeTexto(body);
    }
    if (error.status === 0) {
      return 'No hay conexión con CapitalPos.Tcg.Api. ¿Está levantada en el puerto 5249?';
    }
    if (error.status === 401) {
      return 'Sesión inválida o expirada.';
    }
    if (error.status === 400) {
      return 'No se pudo completar la operación. Revisa los datos e inténtalo de nuevo.';
    }
    return error.message;
  }
  if (error instanceof Error) {
    return error.message;
  }
  return 'No se pudo completar la operación.';
}

function extraerMensajeTexto(cuerpo: string): string {
  try {
    const parsed = JSON.parse(cuerpo) as { message?: string; detail?: string };
    return parsed.message?.trim() || parsed.detail?.trim() || cuerpo.trim();
  } catch {
    return cuerpo.trim();
  }
}
