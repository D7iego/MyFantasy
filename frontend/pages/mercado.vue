<script setup lang="ts">
import type { MarketRow } from '~/components/MarketTable.vue'

const api = useApi()

const { data: rows, pending, error } = await useAsyncData('market', () =>
  api.get<MarketRow[]>('/api/market')
)
</script>

<template>
  <section class="space-y-4">
    <div>
      <h1 class="text-2xl font-extrabold tracking-tight">Mercado</h1>
      <p class="text-sm text-muted">
        Jugadores en venta hoy en tu liga, con su variación de precio. Ordena por Día o Semana
        para ver quién más sube o baja.
      </p>
    </div>

    <div v-if="pending" class="h-64 animate-pulse rounded-card bg-white/5" />
    <AppError v-else-if="error" />
    <AppEmpty
      v-else-if="!rows || rows.length === 0"
      icon="🛒"
      title="No hay jugadores en el mercado"
      hint="El mercado puede estar vacío ahora mismo. Sincroniza y vuelve a intentarlo."
    />
    <MarketTable v-else :rows="rows" />
  </section>
</template>
