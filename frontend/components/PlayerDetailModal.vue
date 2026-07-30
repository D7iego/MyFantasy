<script setup lang="ts">
interface PricePoint { date: string; value: number; delta: number | null }
interface MatchStat { week: number | null; points: number | null; goals: number | null; assists: number | null; minutes: number | null }
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
}

const api = useApi()
const { openPlayerId, openBid, close } = usePlayerModal()
const { eur, signed, deltaClass, date } = useFormat()

const pct = (v: number) => `${Math.round(v * 100)}%`

const detail = ref<PlayerDetail | null>(null)
const pending = ref(false)
const failed = ref(false)
const page = ref(0)
const PAGE = 7

watch(openPlayerId, async (id) => {
  detail.value = null
  failed.value = false
  page.value = 0
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

const pages = computed(() => Math.max(1, Math.ceil((detail.value?.priceHistory.length || 0) / PAGE)))
const pricePage = computed(() => (detail.value?.priceHistory || []).slice(page.value * PAGE, page.value * PAGE + PAGE))

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

          <!-- Puja sugerida (solo si se abrió desde Mercado) -->
          <div v-if="openBid" class="border-t border-white/5 px-5 py-4">
            <div class="section-label mb-2 flex items-center justify-between">
              <span>Puja sugerida</span>
              <span class="normal-case tracking-normal text-muted">estimación</span>
            </div>

            <div class="rounded-xl bg-gradient-to-b from-brand/15 to-brand/5 p-4">
              <div class="flex items-end justify-between gap-3">
                <div>
                  <div class="text-2xl font-extrabold tabular-nums text-white">{{ eur(openBid.suggestedBid) }}</div>
                  <div class="mt-0.5 text-[11px] text-muted">sobre el precio actual de {{ eur(detail.currentValue) }}</div>
                </div>
                <div class="text-right text-xs">
                  <div class="text-muted">Media últ. 5</div>
                  <div class="font-bold tabular-nums">{{ openBid.avgPointsLast5 != null ? openBid.avgPointsLast5.toFixed(1) + ' pts' : '—' }}</div>
                  <div class="mt-1 text-muted">Precio semana</div>
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
                Estimación orientativa basada en rendimiento reciente y tendencia de precio frente al resto
                del mercado de hoy. No es una predicción garantizada.
                <span v-if="openBid.limitedData" class="text-gold">Datos limitados (faltan jornadas).</span>
              </p>
            </div>
          </div>

          <!-- Valor de mercado paginado -->
          <div class="border-t border-white/5 px-5 py-4">
            <div class="section-label mb-2 flex items-center justify-between">
              <span>Valor de mercado</span>
              <span class="normal-case tracking-normal text-muted">7 por página</span>
            </div>

            <div v-if="pricePage.length === 0" class="py-6 text-center text-sm text-muted">
              Aún no hay histórico. Sincroniza varios días.
            </div>

            <div v-else>
              <div
                v-for="p in pricePage"
                :key="p.date"
                class="grid grid-cols-[1fr_auto_auto] items-center gap-3 border-b border-white/5 py-2 last:border-0"
              >
                <span class="text-sm text-white/80">{{ date(p.date) }}</span>
                <span class="text-sm font-semibold tabular-nums">{{ eur(p.value) }}</span>
                <span class="w-28 text-right text-xs font-bold tabular-nums" :class="deltaClass(p.delta)">
                  <template v-if="p.delta != null">{{ p.delta > 0 ? '▲' : p.delta < 0 ? '▼' : '' }} {{ signed(p.delta) }}</template>
                  <template v-else>—</template>
                </span>
              </div>

              <div class="mt-3 flex items-center justify-between">
                <button class="pager-btn" :disabled="page >= pages - 1" @click="page++">‹ Anterior</button>
                <span class="text-xs text-muted">{{ page + 1 }} de {{ pages }}</span>
                <button class="pager-btn" :disabled="page === 0" @click="page--">Siguiente ›</button>
              </div>
            </div>
          </div>

          <!-- Últimos partidos -->
          <div class="border-t border-white/5 px-5 py-4">
            <div class="section-label mb-2">Últimos partidos</div>
            <div v-if="detail.sportsAvailable" class="flex gap-2.5">
              <div
                v-for="m in detail.matches.slice(0, 5)"
                :key="m.week ?? Math.random()"
                class="flex-1 rounded-xl bg-ink-700 p-2.5 text-center"
              >
                <div class="text-[10px] uppercase tracking-wide text-muted">J{{ m.week ?? '?' }}</div>
                <div class="my-0.5 text-base font-extrabold tabular-nums">{{ m.points ?? '—' }}</div>
                <div class="text-[11px] text-white/70">{{ m.goals ?? 0 }}⚽ {{ m.assists ?? 0 }}🅰 {{ m.minutes ?? 0 }}′</div>
              </div>
            </div>
            <div v-else class="py-4 text-center text-sm text-muted">
              Sin partidos disputados aún · se rellenará al empezar la temporada
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
</style>
