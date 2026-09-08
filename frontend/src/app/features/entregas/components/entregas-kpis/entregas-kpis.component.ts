import { Component, input } from '@angular/core';

@Component({
  selector: 'app-entregas-kpis',
  template: `
    <section class="kpis" aria-label="Resumen de despacho">
      <article>
        <span>Por empaquetar</span>
        <strong>{{ porEmpaquetar() }}</strong>
      </article>
      <article>
        <span>Por despachar</span>
        <strong>{{ porDespachar() }}</strong>
      </article>
      <article>
        <span>Pendiente de entrega</span>
        <strong>{{ enTransito() }}</strong>
      </article>
    </section>
  `,
  styles: `
    :host { display: block; }
    .kpis {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 0.75rem;
    }
    article {
      padding: 1rem 1.1rem;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-container);
      box-shadow: var(--shadow-container);
    }
    span {
      display: block;
      color: var(--color-text-secondary);
      font-size: 0.75rem;
      font-weight: 600;
    }
    strong {
      display: block;
      margin-top: 0.25rem;
      font-size: 1.25rem;
    }
    @media (max-width: 640px) {
      .kpis { grid-template-columns: 1fr; }
    }
  `,
})
export class EntregasKpisComponent {
  readonly porEmpaquetar = input.required<number>();
  readonly porDespachar = input.required<number>();
  readonly enTransito = input.required<number>();
}
