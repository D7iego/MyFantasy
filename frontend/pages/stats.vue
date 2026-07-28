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
  todayMovement: number
  availableMoney: number | null
}

interface DailyPnl {
  fecha: string
  movimiento: number
}

const api = useApi()
const { eur, signed } = useFormat()

const { data: stats, pending: pStats, error: eStats } = await useAsyncData('stats', () =>
  api.get<Stats>('/api/stats')
)
const { data: players } = await useAsyncData('stats-players', () =>
  api.get<Holding[]>('/api/players')
)
const { data: daily } = await useAsyncData('stats-daily', () =>
  api.get<DailyPnl[]>('/api/stats/daily-pnl?days=7')
)

const UP = '#18C29C'
const DOWN = '#FF3D4D'

const shortDate = (iso: string) => {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString('es-ES', { day: '2-digit', month: 'short' })
}

// Movimiento diario de la plantilla (barras): verde si sube, rojo si baja.
const dailySeries = computed(() => (daily.value || []).map((d) => d.movimiento))
const hasDaily = computed(() => dailySeries.value.some((v) => v !== 0))
const dailyOptions = computed(() => ({
  chart: { type: 'bar', toolbar: { show: false }, foreColor: '#8B8F9C', fontFamily: 'Sora, sans-serif' },
  plotOptions: { bar: { borderRadius: 4, columnWidth: '55%', distributed: true } },
  colors: (daily.value || []).map((d) => (d.movimiento >= 0 ? UP : DOWN)),
  dataLabels: { enabled: false },
  legend: { show: false },
  grid: { borderColor: 'rgba(255,255,255,0.06)' },
  xaxis: { categories: (daily.value || []).map((d) => shortDate(d.fecha)) },
  yaxis: { labels: { formatter: (v: number) => eur(v, { compact: true }) } },
  tooltip: { y: { formatter: (v: number) => signed(v) } }
}))

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
      <!-- Destacado: dinero disponible + movimiento del día + gráfico 7 días -->
      <div class="grid gap-3 lg:grid-cols-3">
        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-1">
          <StatTile
            label="Dinero disponible"
            :value="eur(stats.availableMoney, { compact: true })"
            tone="gold"
            sub="saldo del equipo"
          />
          <StatTile
            label="Movimiento de hoy"
            :value="signed(stats.todayMovement)"
            :tone="stats.todayMovement > 0 ? 'up' : stats.todayMovement < 0 ? 'down' : 'default'"
            sub="valor de mercado hoy vs. ayer"
          />
        </div>

        <div class="panel p-4 lg:col-span-2">
          <div class="section-label mb-2">Movimiento diario · últimos 7 días</div>
          <ClientOnly>
            <apexchart
              v-if="hasDaily"
              type="bar"
              height="240"
              :options="dailyOptions"
              :series="[{ name: 'Movimiento', data: dailySeries }]"
            />
            <p v-else class="py-12 text-center text-sm text-muted">
              Aún no hay suficiente histórico de precios. Sincroniza varios días para ver la serie.
            </p>
          </ClientOnly>
        </div>
      </div>

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
          label="Plusvalía acumulada"
          :value="signed(stats.activeUnrealizedProfitLoss)"
          :tone="stats.activeUnrealizedProfitLoss > 0 ? 'up' : stats.activeUnrealizedProfitLoss < 0 ? 'down' : 'default'"
          sub="cartera actual − precio de compra"
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
