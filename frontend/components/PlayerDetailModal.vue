<script setup lang="ts">
interface PricePoint { date: string; value: number; delta: number | null }
interface TradeMarker { date: string; type: 'buy' | 'sell'; price: number }
interface MatchStat {
  week: number | null
  points: number | null
  goals: number | null
  assists: number | null
  minutes: number | null
  homeTeam: string | null
  awayTeam: string | null
  homeGoals: number | null
  awayGoals: number | null
  isHome: boolean | null
}
interface PlayerDetail {
  externalId: string
  name: string
  team: string | null
  position: string
  imageUrl: string | null
  currentValue: number | null
  dailyDelta: number | null
  buyoutClause: number | null
  buyoutClauseLockedEndTime: string | null
  isShielded: boolean
  points: number | null
  averagePoints: number | null
  priceHistory: PricePoint[]
  matches: MatchStat[]
  sportsAvailable: boolean
  season: string
  trades: TradeMarker[]
}

const api = useApi()
const { openPlayerId, openBid, close } = usePlayerModal()
const { eur, signed, deltaClass, date } = useFormat()

const pct = (v: number) => `${Math.round(v * 100)}%`

const detail = ref<PlayerDetail | null>(null)
const pending = ref(false)
const failed = ref(false)
const page = ref(0)
const matchPage = ref(0)
const PAGE = 5
const activeTab = ref<'bid' | 'stats' | 'price'>('price')
const selectedMatch = ref<MatchStat | null>(null)
const priceView = ref<'list' | 'chart'>('list')
const chartRange = ref<'1w' | '1m' | 'all'>('1m')

watch(openPlayerId, async (id) => {
  detail.value = null
  failed.value = false
  page.value = 0
  matchPage.value = 0
  selectedMatch.value = null
  priceView.value = 'list'
  chartRange.value = '1m'
  // Si se abrió desde Mercado (hay puja), la pestaña por defecto es la de puja.
  activeTab.value = openBid.value ? 'bid' : 'price'
  if (id == null) return
  pending.value = true
  try {
    detail.value = await api.get<PlayerDetail>(`/api/players/${id}/detail`)
  } catch {
    failed.value = true
  } finally {
    pending.value = false
  }
})

// ---- Histórico de precios (paginado 5) ----
const pages = computed(() => Math.max(1, Math.ceil((detail.value?.priceHistory.length || 0) / PAGE)))
const pricePage = computed(() => (detail.value?.priceHistory || []).slice(page.value * PAGE, page.value * PAGE + PAGE))

// ---- Partidos (paginado 5) + totales de temporada ----
const matchPages = computed(() => Math.max(1, Math.ceil((detail.value?.matches.length || 0) / PAGE)))
const matchSlice = computed(() => (detail.value?.matches || []).slice(matchPage.value * PAGE, matchPage.value * PAGE + PAGE))

const totals = computed(() => {
  const ms = detail.value?.matches || []
  const sum = (pick: (m: MatchStat) => number | null) => ms.reduce((a, m) => a + (pick(m) ?? 0), 0)
  const pointsSum = sum(m => m.points)
  return {
    played: ms.length,
    goals: sum(m => m.goals),
    assists: sum(m => m.assists),
    minutes: sum(m => m.minutes),
    points: detail.value?.points ?? (ms.length ? Math.round(pointsSum) : null),
    avg: detail.value?.averagePoints ?? (ms.length ? pointsSum / ms.length : null)
  }
})

const abbr = (n: string | null) => n ? n.trim().split(/\s+/).slice(-1)[0].slice(0, 3).toUpperCase() : ''
const matchResult = (m: MatchStat) => {
  if (!m.homeTeam || !m.awayTeam) return null
  const score = m.homeGoals != null && m.awayGoals != null ? `${m.homeGoals}-${m.awayGoals}` : 'vs'
  return `${abbr(m.homeTeam)} ${score} ${abbr(m.awayTeam)}`
}
const pointsClass = (v: number | null) => v == null ? 'text-white' : v > 0 ? 'text-up' : v < 0 ? 'text-down' : 'text-white'

// Marcas de compra/venta indexadas por fecha (para el histórico de precios).
const tradesByDate = computed(() => {
  const map: Record<string, TradeMarker[]> = {}
  for (const t of detail.value?.trades || []) (map[t.date] ??= []).push(t)
  return map
})

// Al cambiar de pestaña se sale del detalle de un partido.
watch(activeTab, () => { selectedMatch.value = null })

// ---- Gráfico de valor de mercado (rango 1 semana / 1 mes / total) ----
// priceHistory viene descendente (hoy primero); el gráfico lo quiere ascendente.
const priceAsc = computed(() => [...(detail.value?.priceHistory || [])].reverse())
const chartData = computed(() => {
  const asc = priceAsc.value
  const n = chartRange.value === '1w' ? 7 : chartRange.value === '1m' ? 30 : asc.length
  return asc.slice(Math.max(0, asc.length - n))
})
const chartSummary = computed(() => {
  const d = chartData.value
  if (d.length === 0) return null
  const first = d[0].value, last = d[d.length - 1].value, chg = last - first
  const pct = first ? (chg / first) * 100 : 0
  const up = chg >= 0
  return {
    last,
    up,
    chgText: `${up ? '+' : '−'}${eur(Math.abs(chg))} (${up ? '+' : '−'}${Math.abs(pct).toFixed(1)}%)`,
    from: d[0].date,
    to: d[d.length - 1].date
  }
})
const priceSeries = computed(() => [{
  name: 'Valor',
  data: chartData.value.map(p => [new Date(p.date).getTime(), p.value])
}])
const priceChartOptions = computed(() => {
  const valByDate = new Map(chartData.value.map(p => [p.date, p.value]))
  const from = chartData.value[0]?.date
  const to = chartData.value[chartData.value.length - 1]?.date
  const points = (detail.value?.trades || [])
    .filter(t => from && to && t.date >= from && t.date <= to)
    .map(t => ({
      x: new Date(t.date).getTime(),
      y: valByDate.get(t.date) ?? t.price,
      marker: { size: 5, fillColor: t.type === 'buy' ? '#18C29C' : '#F7C948', strokeColor: '#14161D', strokeWidth: 2 },
      label: {
        text: t.type === 'buy' ? 'Compra' : 'Venta', borderColor: 'transparent', offsetY: 0,
        style: { background: t.type === 'buy' ? '#18C29C' : '#F7C948', color: '#0E0F14', fontSize: '9px', fontWeight: 700 }
      }
    }))
  return {
    chart: { type: 'area', toolbar: { show: false }, zoom: { enabled: false }, foreColor: '#8B8F9C', fontFamily: 'Sora, sans-serif' },
    colors: ['#FF3D4D'],
    stroke: { curve: 'smooth', width: 2.5 },
    fill: { type: 'gradient', gradient: { shadeIntensity: 1, opacityFrom: 0.35, opacityTo: 0, stops: [0, 100] } },
    dataLabels: { enabled: false },
    grid: { borderColor: 'rgba(255,255,255,0.06)', padding: { left: 6, right: 6, top: 0 } },
    xaxis: { type: 'datetime', labels: { datetimeUTC: false, format: 'dd MMM', style: { fontSize: '10px' } }, axisBorder: { show: false }, axisTicks: { show: false }, tooltip: { enabled: false } },
    yaxis: { labels: { formatter: (v: number) => eur(v, { compact: true }) }, tickAmount: 3 },
    tooltip: { x: { format: 'dd MMM yyyy' }, y: { formatter: (v: number) => eur(v) } },
    annotations: { points }
  }
})

const clauseStatus = computed(() => {
  const d = detail.value
  if (!d) return null
  const end = d.buyoutClauseLockedEndTime ? new Date(d.buyoutClauseLockedEndTime) : null
  const locked = (end && end.getTime() > Date.now()) || d.isShielded
  return locked
    ? { text: end ? `Blindada hasta ${date(d.buyoutClauseLockedEndTime)}` : 'Blindada', locked: true }
    : { text: 'Clausulable', locked: false }
})

const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') close() }
onMounted(() => window.addEventListener('keydown', onKey))
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))
</script>

<template>
  <Teleport to="body">
    <div
      v-if="openPlayerId != null"
      class="fixed inset-0 z-50 grid place-items-center overflow-y-auto bg-black/60 p-4"
      @click.self="close"
    >
      <div class="w-full max-w-xl overflow-hidden rounded-2xl border border-white/10 bg-ink-800 shadow-2xl">
        <!-- Cargando -->
        <div v-if="pending" class="grid h-64 place-items-center">
          <span class="h-7 w-7 animate-spin rounded-full border-2 border-white/30 border-t-white" />
        </div>

        <div v-else-if="failed || !detail" class="grid h-64 place-items-center p-6 text-center">
          <div>
            <p class="font-semibold">No se pudo cargar el jugador</p>
            <button class="btn-ghost mt-3" @click="close">Cerrar</button>
          </div>
        </div>

        <template v-else>
          <!-- Hero -->
          <div class="relative flex gap-4 bg-gradient-to-b from-brand/10 to-transparent p-5">
            <button class="absolute right-4 top-4 grid h-8 w-8 place-items-center rounded-lg bg-white/5 text-white/70 hover:bg-white/10" @click="close">✕</button>
            <img
              v-if="detail.imageUrl"
              :src="detail.imageUrl"
              :alt="detail.name"
              class="h-24 w-24 shrink-0 rounded-2xl border border-white/10 bg-white/5 object-cover object-top"
            />
            <div class="min-w-0 flex-1 pt-1">
              <p class="truncate text-xs font-medium text-muted">{{ detail.team || '—' }}</p>
              <h2 class="mt-0.5 text-2xl font-extrabold leading-tight tracking-tight">{{ detail.name }}</h2>
              <span class="mt-1.5 inline-block rounded-full bg-brand/15 px-2.5 py-0.5 text-[11px] font-bold uppercase tracking-wide text-brand">{{ detail.position }}</span>
              <div class="mt-2.5 flex items-baseline gap-2">
                <b class="text-xl font-extrabold tabular-nums">{{ eur(detail.currentValue) }}</b>
                <DeltaBadge v-if="detail.dailyDelta != null" :value="detail.dailyDelta" compact />
              </div>
            </div>
          </div>

          <!-- KPIs -->
          <div class="grid grid-cols-3 gap-2.5 px-5 pb-4">
            <div class="rounded-xl bg-ink-700 p-3">
              <div class="section-label">Puntos</div>
              <div class="text-lg font-bold tabular-nums">{{ detail.points ?? '—' }}</div>
            </div>
            <div class="rounded-xl bg-ink-700 p-3">
              <div class="section-label">Media</div>
              <div class="text-lg font-bold tabular-nums">{{ detail.averagePoints != null ? detail.averagePoints.toFixed(1) : '—' }}</div>
            </div>
            <div class="rounded-xl bg-ink-700 p-3">
              <div class="section-label">Cláusula</div>
              <div class="text-lg font-bold tabular-nums">{{ eur(detail.buyoutClause, { compact: true }) }}</div>
              <div v-if="clauseStatus" class="mt-0.5 text-[10px]" :class="clauseStatus.locked ? 'text-gold' : 'text-up'">{{ clauseStatus.text }}</div>
            </div>
          </div>

          <!-- Últimos partidos (vista rápida) -->
          <div class="border-t border-white/5 px-5 py-4">
            <div class="section-label mb-2">Últimos partidos</div>
            <div v-if="detail.sportsAvailable" class="flex gap-2.5">
              <div
                v-for="m in detail.matches.slice(0, 5)"
                :key="m.week ?? Math.random()"
                class="flex-1 rounded-xl bg-ink-700 p-2.5 text-center"
              >
                <div class="text-[10px] uppercase tracking-wide text-muted">J{{ m.week ?? '?' }}</div>
                <div class="my-0.5 text-base font-extrabold tabular-nums" :class="pointsClass(m.points)">{{ m.points ?? '—' }}</div>
                <div class="flex items-center justify-center gap-1.5 text-[11px] text-white/70">
                  <span class="inline-flex items-center gap-0.5"><StatIcon name="goal" :size="12" class="text-muted" />{{ m.goals ?? 0 }}</span>
                  <span class="inline-flex items-center gap-0.5"><StatIcon name="assist" :size="12" class="text-muted" />{{ m.assists ?? 0 }}</span>
                  <span class="inline-flex items-center gap-0.5"><StatIcon name="minutes" :size="12" class="text-muted" />{{ m.minutes ?? 0 }}</span>
                </div>
              </div>
            </div>
            <div v-else class="py-4 text-center text-sm text-muted">
              Sin partidos disputados aún · se rellenará al empezar la temporada
            </div>
          </div>

          <!-- Barra de pestañas (siempre visible) -->
          <div class="border-t border-white/5">
            <div class="flex gap-2 px-5 pt-3">
              <button
                v-if="openBid"
                class="tab-btn"
                :class="activeTab === 'bid' ? 'tab-on' : 'tab-off'"
                @click="activeTab = 'bid'"
              >Puja sugerida</button>
              <button
                class="tab-btn inline-flex items-center gap-1.5"
                :class="activeTab === 'stats' ? 'tab-on' : 'tab-off'"
                @click="activeTab = 'stats'"
              ><StatIcon name="chart" :size="14" /> Estadísticas</button>
              <button
                class="tab-btn"
                :class="activeTab === 'price' ? 'tab-on' : 'tab-off'"
                @click="activeTab = 'price'"
              >Valor de mercado</button>
            </div>

            <!-- Panel: Puja sugerida -->
            <div v-if="openBid && activeTab === 'bid'" class="px-5 py-4">
              <div class="rounded-xl bg-gradient-to-b from-brand/15 to-brand/5 p-4">
                <div class="flex items-end justify-between gap-3">
                  <div>
                    <div class="text-2xl font-extrabold tabular-nums text-white">{{ eur(openBid.suggestedBid) }}</div>
                    <div class="mt-0.5 text-[11px] text-muted">sobre el precio actual de {{ eur(detail.currentValue) }}</div>
                  </div>
                  <div class="text-right text-xs">
                    <div class="text-muted">Media últ. 5</div>
                    <div class="font-bold tabular-nums">{{ openBid.avgPointsLast5 != null ? openBid.avgPointsLast5.toFixed(1) + ' pts' : '—' }}</div>
                    <div class="mt-1 text-muted">Tendencia precio</div>
                    <div class="font-bold tabular-nums" :class="deltaClass(openBid.weeklyPct)">
                      {{ openBid.weeklyPct != null ? (openBid.weeklyPct > 0 ? '+' : '') + openBid.weeklyPct + '%' : '—' }}
                    </div>
                  </div>
                </div>

                <!-- Desglose de scores -->
                <div class="mt-3 space-y-1.5">
                  <div v-for="s in [
                    { l: 'Rendimiento', v: openBid.performanceScore },
                    { l: 'Tendencia precio', v: openBid.priceTrendScore },
                    { l: 'Combinado', v: openBid.combinedScore }
                  ]" :key="s.l" class="flex items-center gap-2">
                    <span class="w-28 shrink-0 text-[11px] text-muted">{{ s.l }}</span>
                    <div class="h-1.5 flex-1 overflow-hidden rounded-full bg-white/10">
                      <div class="h-full rounded-full bg-brand" :style="{ width: s.v != null ? pct(s.v) : '0%' }" />
                    </div>
                    <span class="w-10 text-right text-[11px] tabular-nums text-white/80">{{ s.v != null ? pct(s.v) : '—' }}</span>
                  </div>
                </div>

                <p class="mt-3 text-[11px] leading-snug text-muted">
                  Estimación orientativa (rendimiento reciente + tendencia de precio). Referencia: la liga te
                  recompra por hasta +10% del valor, así que hasta ahí la puja es de bajo riesgo. No es una
                  predicción garantizada.
                  <span v-if="openBid.limitedData" class="text-gold">Datos limitados: sin partidos suficientes, la puja se basa solo en el precio.</span>
                </p>
              </div>
            </div>

            <!-- Panel: Estadísticas -->
            <div v-if="activeTab === 'stats'" class="px-5 py-4">
              <!-- Vista general -->
              <template v-if="!selectedMatch">
              <div class="section-label mb-3 flex items-center justify-between">
                <span>Temporada {{ detail.season }}</span>
                <span v-if="totals.played" class="normal-case tracking-normal text-muted">5 partidos por página</span>
              </div>

              <template v-if="totals.played">
                <!-- Totales de temporada -->
                <div class="mb-4 grid grid-cols-5 gap-2">
                  <div v-for="t in [
                    { ic: 'goal', n: totals.goals, cap: 'Goles' },
                    { ic: 'assist', n: totals.assists, cap: 'Asist.' },
                    { ic: 'minutes', n: totals.minutes.toLocaleString('es-ES'), cap: 'Minutos' },
                    { ic: 'points', n: totals.points ?? '—', cap: 'Puntos' },
                    { ic: 'avg', n: totals.avg != null ? totals.avg.toFixed(1) : '—', cap: 'Media' }
                  ]" :key="t.cap" class="rounded-xl bg-ink-700 p-2.5 text-center">
                    <StatIcon :name="(t.ic as any)" :size="18" class="mx-auto mb-1 text-muted" />
                    <div class="text-base font-extrabold tabular-nums leading-none">{{ t.n }}</div>
                    <div class="mt-1 text-[9.5px] uppercase tracking-wide text-muted">{{ t.cap }}</div>
                  </div>
                </div>

                <!-- Tabla de partidos (pulsables) -->
                <table class="w-full">
                  <thead>
                    <tr class="text-[10px] uppercase tracking-wide text-muted">
                      <th class="pb-2 text-left font-bold">Jornada</th>
                      <th class="pb-2 text-right font-bold">Pts</th>
                      <th class="pb-2 text-right font-bold"><StatIcon name="goal" :size="13" class="ml-auto" /></th>
                      <th class="pb-2 text-right font-bold"><StatIcon name="assist" :size="13" class="ml-auto" /></th>
                      <th class="pb-2 text-right font-bold"><StatIcon name="minutes" :size="13" class="ml-auto" /></th>
                      <th class="w-4 pb-2"></th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="m in matchSlice"
                      :key="m.week ?? Math.random()"
                      class="cursor-pointer border-t border-white/5 transition hover:bg-white/5"
                      @click="selectedMatch = m"
                    >
                      <td class="py-2.5 text-left">
                        <span class="font-bold">J{{ m.week ?? '?' }}</span>
                        <span v-if="matchResult(m)" class="ml-2 text-[10px] font-semibold text-muted">{{ matchResult(m) }}</span>
                      </td>
                      <td class="py-2.5 text-right text-sm font-extrabold tabular-nums" :class="pointsClass(m.points)">
                        {{ m.points != null ? (m.points > 0 ? '+' : '') + m.points : '—' }}
                      </td>
                      <td class="py-2.5 text-right text-sm tabular-nums" :class="m.goals ? 'text-white/90' : 'text-white/25'">{{ m.goals ?? 0 }}</td>
                      <td class="py-2.5 text-right text-sm tabular-nums" :class="m.assists ? 'text-white/90' : 'text-white/25'">{{ m.assists ?? 0 }}</td>
                      <td class="py-2.5 text-right text-sm tabular-nums text-white/80">{{ m.minutes ?? 0 }}′</td>
                      <td class="py-2.5 pl-2 text-right text-muted">›</td>
                    </tr>
                  </tbody>
                </table>

                <div v-if="matchPages > 1" class="mt-3 flex items-center justify-between">
                  <button class="pager-btn" :disabled="matchPage === 0" @click="matchPage--">‹ Anterior</button>
                  <span class="text-xs text-muted">{{ matchPage + 1 }} de {{ matchPages }}</span>
                  <button class="pager-btn" :disabled="matchPage >= matchPages - 1" @click="matchPage++">Siguiente ›</button>
                </div>
                <div class="mt-3 text-center text-xs text-muted">
                  Total: <b class="text-white">{{ totals.played }} {{ totals.played === 1 ? 'partido' : 'partidos' }}</b> disputados
                </div>
              </template>

              <div v-else class="py-6 text-center text-sm text-muted">
                Sin partidos disputados aún · se rellenará al empezar la temporada
              </div>
              </template>

              <!-- Detalle de un partido -->
              <template v-else>
                <button class="pager-btn mb-3" @click="selectedMatch = null">‹ Volver a estadísticas</button>

                <div class="mb-4 flex items-center justify-between gap-3 rounded-xl bg-gradient-to-b from-brand/15 to-brand/5 p-4">
                  <div>
                    <div class="text-[10px] uppercase tracking-wide text-muted">Jornada {{ selectedMatch.week ?? '?' }}</div>
                    <div v-if="matchResult(selectedMatch)" class="mt-0.5 text-lg font-extrabold">{{ matchResult(selectedMatch) }}</div>
                    <div v-if="selectedMatch.isHome != null" class="mt-0.5 text-[11px] text-muted">{{ selectedMatch.isHome ? 'Como local' : 'Como visitante' }}</div>
                  </div>
                  <div class="text-right">
                    <div class="text-2xl font-extrabold tabular-nums" :class="pointsClass(selectedMatch.points)">
                      {{ selectedMatch.points != null ? (selectedMatch.points > 0 ? '+' : '') + selectedMatch.points : '—' }}
                    </div>
                    <div class="text-[10px] uppercase tracking-wide text-muted">Puntos</div>
                  </div>
                </div>

                <div class="section-label mb-2">Rendimiento</div>
                <div class="grid grid-cols-3 gap-2">
                  <div v-for="s in [
                    { ic: 'goal', n: String(selectedMatch.goals ?? 0), cap: 'Goles' },
                    { ic: 'assist', n: String(selectedMatch.assists ?? 0), cap: 'Asistencias' },
                    { ic: 'minutes', n: (selectedMatch.minutes ?? 0) + '′', cap: 'Minutos' }
                  ]" :key="s.cap" class="rounded-xl bg-ink-700 p-3 text-center">
                    <StatIcon :name="(s.ic as any)" :size="18" class="mx-auto mb-1 text-muted" />
                    <div class="text-lg font-extrabold tabular-nums leading-none">{{ s.n }}</div>
                    <div class="mt-1 text-[10px] uppercase tracking-wide text-muted">{{ s.cap }}</div>
                  </div>
                </div>

                <p class="mt-3 text-[11px] leading-snug text-muted">
                  Más detalle del partido (rating, tiros, tarjetas y desglose de puntos) se añadirá cuando la API de LaLiga lo aporte durante la temporada.
                </p>
              </template>
            </div>

            <!-- Panel: Valor de mercado -->
            <div v-if="activeTab === 'price'" class="px-5 py-4">
              <div class="section-label mb-3 flex items-center justify-between">
                <span>Valor de mercado</span>
                <div class="seg">
                  <button class="seg-btn" :class="priceView === 'list' ? 'seg-on' : 'seg-off'" @click="priceView = 'list'">Lista</button>
                  <button class="seg-btn" :class="priceView === 'chart' ? 'seg-on' : 'seg-off'" @click="priceView = 'chart'">Gráfico</button>
                </div>
              </div>

              <div v-if="detail.priceHistory.length === 0" class="py-6 text-center text-sm text-muted">
                Aún no hay histórico. Sincroniza varios días.
              </div>

              <!-- Vista Lista -->
              <div v-else-if="priceView === 'list'">
                <div
                  v-for="p in pricePage"
                  :key="p.date"
                  class="grid grid-cols-[1fr_auto_auto] items-center gap-3 border-b border-white/5 py-2 last:border-0"
                >
                  <div class="flex min-w-0 flex-wrap items-center gap-1.5">
                    <span class="text-sm text-white/80">{{ date(p.date) }}</span>
                    <span
                      v-for="(t, i) in (tradesByDate[p.date] || [])"
                      :key="i"
                      class="inline-flex items-center gap-1 rounded-full border px-1.5 py-0.5 text-[10px] font-extrabold leading-none"
                      :class="t.type === 'buy' ? 'border-up/40 bg-up/15 text-up' : 'border-gold/40 bg-gold/15 text-gold'"
                    >
                      {{ t.type === 'buy' ? '▼ Compra' : '▲ Venta' }} · {{ eur(t.price, { compact: true }) }}
                    </span>
                  </div>
                  <span class="text-sm font-semibold tabular-nums">{{ eur(p.value) }}</span>
                  <span class="w-28 text-right text-xs font-bold tabular-nums" :class="deltaClass(p.delta)">
                    <template v-if="p.delta != null">{{ p.delta > 0 ? '▲' : p.delta < 0 ? '▼' : '' }} {{ signed(p.delta) }}</template>
                    <template v-else>—</template>
                  </span>
                </div>

                <div v-if="pages > 1" class="mt-3 flex items-center justify-between">
                  <button class="pager-btn" :disabled="page >= pages - 1" @click="page++">‹ Anterior</button>
                  <span class="text-xs text-muted">{{ page + 1 }} de {{ pages }}</span>
                  <button class="pager-btn" :disabled="page === 0" @click="page--">Siguiente ›</button>
                </div>
                <div class="mt-3 text-center text-xs text-muted">
                  Histórico: <b class="text-white">{{ detail.priceHistory.length }} {{ detail.priceHistory.length === 1 ? 'registro' : 'registros' }}</b>
                </div>
              </div>

              <!-- Vista Gráfico -->
              <div v-else>
                <div class="mb-3 flex items-end justify-between gap-3">
                  <div>
                    <div class="text-xl font-extrabold tabular-nums">{{ eur(chartSummary ? chartSummary.last : detail.currentValue) }}</div>
                    <div v-if="chartSummary" class="mt-0.5 text-[11px] text-muted">
                      en el rango:
                      <span class="font-bold tabular-nums" :class="chartSummary.up ? 'text-up' : 'text-down'">{{ chartSummary.chgText }}</span>
                    </div>
                  </div>
                  <div class="flex gap-1">
                    <button
                      v-for="r in [{ k: '1w', l: '1 sem' }, { k: '1m', l: '1 mes' }, { k: 'all', l: 'Total' }]"
                      :key="r.k"
                      class="range-btn"
                      :class="chartRange === r.k ? 'range-on' : ''"
                      @click="chartRange = (r.k as any)"
                    >{{ r.l }}</button>
                  </div>
                </div>

                <div class="rounded-xl border border-white/5 bg-ink-700 p-2">
                  <ClientOnly>
                    <apexchart type="area" height="200" :options="priceChartOptions" :series="priceSeries" />
                  </ClientOnly>
                </div>
                <div v-if="chartSummary" class="mt-2 flex justify-between px-1 text-[10px] text-muted">
                  <span>{{ date(chartSummary.from) }}</span><span>{{ date(chartSummary.to) }}</span>
                </div>
              </div>
            </div>
          </div>
        </template>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.pager-btn {
  @apply rounded-lg border border-white/10 bg-ink-700 px-3 py-1.5 text-xs font-semibold text-white/85 transition hover:bg-white/10 disabled:opacity-40 disabled:pointer-events-none;
}
.tab-btn {
  @apply rounded-lg px-3 py-1.5 text-sm font-semibold transition;
}
.tab-on {
  @apply bg-brand/15 text-brand;
}
.tab-off {
  @apply text-muted hover:text-white;
}
.seg {
  @apply inline-flex rounded-lg border border-white/10 bg-ink-700 p-0.5;
}
.seg-btn {
  @apply rounded-md px-2.5 py-1 text-xs font-bold transition;
}
.seg-on {
  @apply bg-brand text-white;
}
.seg-off {
  @apply text-muted hover:text-white;
}
.range-btn {
  @apply rounded-lg border border-white/10 bg-ink-700 px-2.5 py-1 text-[11px] font-bold text-muted transition hover:text-white;
}
.range-on {
  @apply border-brand/40 bg-brand/15 text-brand;
}
</style>
