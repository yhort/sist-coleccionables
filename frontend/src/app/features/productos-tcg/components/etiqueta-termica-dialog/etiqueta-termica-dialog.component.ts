import {
  AfterViewInit,
  Component,
  ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';
import JsBarcode from 'jsbarcode';

import { ProductoTcg } from '../../models/producto-tcg.model';

@Component({
  selector: 'app-etiqueta-termica-dialog',
  templateUrl: './etiqueta-termica-dialog.component.html',
  styleUrl: './etiqueta-termica-dialog.component.scss',
})
export class EtiquetaTermicaDialogComponent implements AfterViewInit {
  readonly producto = input.required<ProductoTcg>();
  readonly closed = output<void>();

  private readonly barcodeSvg = viewChild<ElementRef<SVGSVGElement>>('barcodeSvg');

  ngAfterViewInit(): void {
    this.renderBarcode();
  }

  get sku(): string {
    const p = this.producto();
    return (p.skuInterno || p.codigoSku || '').trim();
  }

  imprimir(): void {
    this.renderBarcode();
    const svg = this.barcodeSvg()?.nativeElement;
    if (!svg) {
      return;
    }

    const p = this.producto();
    const svgMarkup = new XMLSerializer().serializeToString(svg);
    const win = window.open('', '_blank', 'noopener,noreferrer,width=420,height=320');
    if (!win) {
      return;
    }

    win.document.write(`<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8" />
  <title>Etiqueta ${this.escapeHtml(this.sku)}</title>
  <style>
    @page { size: 50mm 25mm; margin: 0; }
    html, body { margin: 0; padding: 0; width: 50mm; height: 25mm; }
    body { font-family: Arial, Helvetica, sans-serif; }
    .label {
      box-sizing: border-box;
      width: 50mm;
      height: 25mm;
      padding: 1.2mm 1.5mm;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      align-items: center;
    }
    .name {
      font-size: 7pt;
      font-weight: 700;
      line-height: 1.1;
      max-height: 8mm;
      overflow: hidden;
      text-align: center;
      width: 100%;
    }
    .barcode { width: 46mm; height: 10mm; display: flex; justify-content: center; }
    .barcode svg { width: 46mm; height: 10mm; }
    .sku { font-size: 7.5pt; letter-spacing: 0.04em; font-weight: 700; }
    .meta { font-size: 6pt; color: #333; }
  </style>
</head>
<body>
  <div class="label">
    <div class="name">${this.escapeHtml(p.nombre)}</div>
    <div class="barcode">${svgMarkup}</div>
    <div class="sku">${this.escapeHtml(this.sku)}</div>
    ${p.ubicacionTienda ? `<div class="meta">${this.escapeHtml(p.ubicacionTienda)}</div>` : ''}
  </div>
  <script>window.onload = () => { window.print(); window.onafterprint = () => window.close(); };</script>
</body>
</html>`);
    win.document.close();
  }

  private renderBarcode(): void {
    const el = this.barcodeSvg()?.nativeElement;
    const value = this.sku;
    if (!el || !value) {
      return;
    }
    JsBarcode(el, value, {
      format: 'CODE128',
      displayValue: false,
      margin: 0,
      width: 1.4,
      height: 36,
      background: '#ffffff',
      lineColor: '#000000',
    });
  }

  private escapeHtml(value: string): string {
    return value
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;');
  }
}
