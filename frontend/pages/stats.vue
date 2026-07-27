<script setup lang="ts">
import type { Holding } from '~/components/HoldingsTable.vue'

interface Stats {
  totalProfitLoss: number
  totalSales: number
  profitableSales: number
  profitableRate: number
  bestSale: any | null
  worstSale: any | null
  activePortfolioValue: number
  activeUnrealizedProfitLoss: number
  activeHoldings: number
}

const api = useApi()
const { eur, signed } = useFormat()

const { data: stats, pending: pStats, error: eStats } = await useAsyncData('stats', () =>
  api.get<Stats>('/api/stats')
)
const { data: players } = await useAsyncData('stats-players', () =>
  api.get<Holding[]>('/api/players')
)

// Composición de la cartera por posición (donut)
const byPosition = computed(() => {
  const acc: Record<string, number> = {}
  for (const p of players.value || []) {
    acc[p.position] = (acc[p.position] || 0) + (p.currentValue || 0)
  }
  const labels = Object.keys(acc)
  return { labels, series: labels.map((l) => acc[l]) }
})

// Top jugadores por valor (barras)
const topPlayers = computed(() => {
  const list = [...(players.value || [])]
    .sort((a, b) => (b.currentValue || 0) - (a.currentValue || 0))
    .slice(0, 8)
  return {
    categories: list.map((p) => p.name),
    series: list.map((p) => Math.round((p.currentValue || 0) / 1_000_000 * 10) / 10)
  }
})

const donutOptions = computed(() => ({
  chart: { type: 'donut', foreColor: '#8B8F9C', fontFamily: 'Sora, sans-serif' },
  labels: byPosition.value.labels,
  colors: ['#F7C948', '#38BDF8', '#18C29C', '#FF3D4D', '#A78BFA'],
  legend: { position: 'bottom' },
  stroke: { width: 0 },
  dataLabels: { enabled: false },
  tooltip: { y: { formatter: (v: number) => eur(v) } },
  plotOptions: { pie: { donut: { size: '68%' } } }
}))

const barOptions = computed(() => ({
  chart: { type: 'bar', toolbar: { show: false }, foreColor: '#8B8F9C', fontFamily: 'Sora, sans-serif' },
  colors: ['#FF3D4D'],
  plotOptions: { bar: { borderRadius: 6, horizontal: true, barHeight: '62%' } },
  dataLabels: { enabled: false },
  grid: { borderColor: 'rgba(255,255,255,0.06)' },
  xaxis: { categories: topPlayers.value.categories, labels: { formatter: (v: string) => `${v}M` } },
  tooltip: { y: { formatter: (v: number) => `${v} M €` } }
}))
</script>

<template>
  <section class="space-y-5">
    <div>
      <h1 class="text-2xl font-extrabold tracking-tight">Estadísticas</h1>
      <p class="text-sm text-muted">Métricas sobre tus operaciones y tu cartera.</p>
    </div>

    <div v-if="pStats" class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <div v-for="i in 4" :key="i" class="h-24 animate-pulse rounded-card bg-white/5" />
    </div>
    <AppError v-else-if="eStats" />

    <template v-else-if="stats">
      <!-- KPIs -->
      <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile
          label="G/P realizada"
          :value="signed(stats.totalProfitLoss)"
          :tone="stats.totalProfitLoss > 0 ? 'up' : stats.totalProfitLoss < 0 ? 'down' : 'default'"
          :sub="`${stats.totalSales} ventas`"
        />
        <StatTile
          label="Valor cartera"
          :value="eur(stats.activePortfolioValue, { compact: true })"
          tone="brand"
          :sub="`${stats.activeHoldings} jugadores`"
        />
        <StatTile
          label="G/P no realizada"
          :value="signed(stats.activeUnrealizedProfitLoss)"
          :tone="stats.activeUnrealizedProfitLoss > 0 ? 'up' : stats.activeUnrealizedProfitLoss < 0 ? 'down' : 'default'"
          sub="cartera actual vs compra"
        />
        <StatTile
          label="% con beneficio"
          :value="`${Math.round(stats.profitableRate * 100)}%`"
          :sub="`${stats.profitableSales}/${stats.totalSales} operaciones`"
        />
      </div>

      <!-- Mejor / peor operación -->
      <div v-if="stats.bestSale || stats.worstSale" class="grid gap-3 sm:grid-cols-2">
        <div v-if="stats.bestSale" class="panel flex items-center justify-between p-4">
          <div>
            <div class="section-label">Mejor operación</div>
            <div class="font-bold">{{ stats.bestSale.name }}</div>
          </div>
          <div class="text-lg font-extrabold text-up tabular-nums">{{ signed(stats.bestSale.profitLoss) }}</div>
        </div>
        <div v-if="stats.worstSale" class="panel flex items-center justify-between p-4">
          <div>
            <div class="section-label">Peor operación</div>
            <div class="font-bold">{{ stats.worstSale.name }}</div>
          </div>
          <div class="text-lg font-extrabold text-down tabular-nums">{{ signed(stats.worstSale.profitLoss) }}</div>
        </div>
      </div>

      <!-- Gráficos -->
      <div class="grid gap-3 lg:grid-cols-2">
        <div class="panel p-4">
          <div class="section-label mb-2">Cartera por posición</div>
          <ClientOnly>
            <apexchart
              v-if="byPosition.series.length"
              type="donut"
              height="280"
              :options="donutOptions"
              :series="byPosition.series"
            />
            <p v-else class="py-12 text-center text-sm text-muted">Sin datos de plantilla.</p>
          </ClientOnly>
        </div>
        <div class="panel p-4">
          <div class="section-label mb-2">Jugadores más valiosos</div>
          <ClientOnly>
            <apexchart
              v-if="topPlayers.series.length"
              type="bar"
              height="280"
              :options="barOptions"
              :series="[{ name: 'Valor', data: topPlayers.series }]"
            />
            <p v-else class="py-12 text-center text-sm text-muted">Sin datos de plantilla.</p>
          </ClientOnly>
        </div>
      </div>
    </template>
  </section>
</template>
