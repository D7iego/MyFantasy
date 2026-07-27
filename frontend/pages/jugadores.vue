<script setup lang="ts">
import type { Holding } from '~/components/HoldingsTable.vue'

const api = useApi()
const { eur } = useFormat()

const { data: rows, pending, error, refresh } = await useAsyncData('players', () =>
  api.get<Holding[]>('/api/players')
)

const totals = computed(() => {
  const list = rows.value || []
  return {
    count: list.length,
    value: list.reduce((s, h) => s + (h.currentValue || 0), 0),
    pl: list.reduce((s, h) => s + (h.profitLoss || 0), 0)
  }
})
</script>

<template>
  <section class="space-y-4">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 class="text-2xl font-extrabold tracking-tight">Plantilla</h1>
        <p class="text-sm text-muted">Tus jugadores activos con precio, variación y G/P.</p>
      </div>
      <div v-if="rows && rows.length" class="flex gap-2">
        <div class="rounded-xl bg-ink-800 px-3 py-2 text-right">
          <div class="section-label">Valor</div>
          <div class="font-bold tabular-nums">{{ eur(totals.value, { compact: true }) }}</div>
        </div>
        <div class="rounded-xl bg-ink-800 px-3 py-2 text-right">
          <div class="section-label">Jugadores</div>
          <div class="font-bold tabular-nums">{{ totals.count }}</div>
        </div>
      </div>
    </div>

    <div v-if="pending" class="h-64 animate-pulse rounded-card bg-white/5" />
    <AppError v-else-if="error" />
    <AppEmpty
      v-else-if="!rows || rows.length === 0"
      icon="👥"
      title="Sin jugadores en la plantilla"
      hint="Sincroniza para detectar tu equipo en la liga por defecto."
    />
    <HoldingsTable v-else :rows="rows" editable @saved="refresh" />
  </section>
</template>
