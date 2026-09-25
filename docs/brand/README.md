# Trunqi TCG — logo oficial

Versión limpia para documentos (boletas, facturas, etiquetas SUNAT).

## Archivos

| Archivo | Uso |
|---------|-----|
| `logo-trunqi-oficial.svg` | Fuente vectorial (edición / máxima calidad) |
| `logo-trunqi-oficial.png` | Raster 2000×1750 con fondo transparente (PDF / UI) |

Copias operativas:

- Backend: `backend/CapitalPos.Tcg.Api/Assets/logo-trunqi.{svg,png}`
- Frontend: `frontend/src/assets/img/logo-trunqi.{svg,png}` y `frontend/public/assets/img/`

El JPG legado (`logo-trunqi.jpg`) se mantiene solo como fallback.

## Mejoras respecto al original

1. **Vectorial** — SVG con geometría explícita (cartas, espada, banner, tipografía).
2. **Sin reborde blanco** — fondo transparente; área de seguridad ~8% en el `viewBox`.
3. **Alineación** — eje de simetría vertical en `x=320`; banner espejado; icono y textos centrados.
4. **Sin mascota** — solo el mark corporativo (apto para CPE).

## Paleta print-safe (RGB ≈ CMYK)

| Color | Hex | CMYK aprox. |
|-------|-----|-------------|
| Azul real | `#0A4EA8` | C95 M70 Y0 K5 |
| Azul borde | `#083F8A` | C100 M75 Y0 K15 |
| Oro | `#C9A227` | C10 M25 Y90 K10 |
| Negro | `#1A1A1A` | C0 M0 Y0 K90 |
| Blanco | `#FFFFFF` | C0 M0 Y0 K0 |

Los valores RGB evitan amarillos/azules “neón” que fallan en CMYK de oficina.
