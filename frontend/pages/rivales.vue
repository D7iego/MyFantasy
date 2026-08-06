<script setup lang="ts">
interface Manager {
  teamId: string
  managerName: string
  teamName: string | null
  rank: number
  points: number | null
  teamValue: number | null
}
interface RivalPlayer {
  playerId: number | null
  externalId: string
  name: string
  team: string | null
  position: string
  currentValue: number | null
  dailyDelta: number | null
  weeklyDelta: number | null
  buyoutClause: number | null
  buyoutClauseLockedEndTime: string | null
  isShielded: boolean
  imageUrl: string | null
}
interface RivalsResp { managers: Manager[]; selectedTeamId: string | null; squad: RivalPlayer[] | null }

const api = useApi()
const { eur, deltaClass, signed, date } = useFormat()
const { open } = usePlayerModal()

const { data: initial, pending, error } = await useAsyncData('rivals', () => api.get<RivalsResp>('/api/rivals'))
const managers = computed(() => initial.value?.managers || [])

const selected = ref('')
const squad = ref<RivalPlayer[] | null>(null)
const loadingSquad = ref(false)

const selectedManager = computed(() => managers.value.find(m => m.teamId === selected.value) || null)

const selectManager = (teamId: string) => { selected.value = selected.value === teamId ? '' : teamId }

watch(selected, async (tid) => {
  squad.value = null
  if (!tid) return
  loadingSquad.value = true
  try {
    const r = await api.get<RivalsResp>(`/api/rivals?teamId=${encodeURIComponent(tid)}`)
    squad.value = r.squad || []
  } finally {
    loadingSquad.value = false
  }
})

// ---- Avatar de manager (iniciales + color determinista) ----
const AVATAR_COLORS = ['#4F46E5', '#0EA5E9', '#059669', '#DB2777', '#D97706', '#7C3AED', '#DC2626', '#0D9488']
const initials = (n: string) => n.trim().split(/\s+/).slice(0, 2).map(w => w[0]).join('').toUpperCase()
const avatarColor = (m: Manager) => AVATAR_COLORS[managers.value.indexOf(m) % AVATAR_COLORS.length] ?? AVATAR_COLORS[0]
const points = (p: number | null) => p != null ? Math.round(p).toLocaleString('es-ES') : '—'

const posShort = (p: string) =>
  ({ Portero: 'POR', Defensa: 'DEF', Centrocampista: 'MED', Delantero: 'DEL' } as Record<string, string>)[p] || p
const posColor = (p: string) =>
  ({
    Portero: 'bg-amber-100 text-amber-700',
    Defensa: 'bg-sky-100 text-sky-700',
    Centrocampista: 'bg-emerald-100 text-emerald-700',
    Delantero: 'bg-rose-100 text-rose-700'
  } as Record<string, string>)[p] || 'bg-gray-100 text-gray-600'
const posDot = (p: string) =>
  ({ POR: 'bg-amber-400', DEF: 'bg-sky-400', MED: 'bg-emerald-400', DEL: 'bg-rose-400' } as Record<string, string>)[p] || 'bg-white/40'

const isLocked = (r: RivalPlayer) => {
  const end = r.buyoutClauseLockedEndTime ? new Date(r.buyoutClauseLockedEndTime) : null
  return (end && end.getTime() > Date.now()) || r.isShielded
}
const clause = (r: RivalPlayer) => ({
  locked: isLocked(r),
  label: isLocked(r) ? (r.buyoutClauseLockedEndTime ? `Blindada hasta ${date(r.buyoutClauseLockedEndTime)}` : 'Blindada') : 'Clausulable'
})

// ---- Desglose de estadísticas del rival (calculado de su plantilla) ----
const breakdown = computed(() => {
  const s = squad.value || []
  const sum = (pick: (p: RivalPlayer) => number | null) => s.reduce((a, p) => a + (pick(p) ?? 0), 0)
  const counts: Record<string, number> = { POR: 0, DEF: 0, MED: 0, DEL: 0 }
  s.forEach(p => { const k = posShort(p.position); if (k in counts) counts[k]++ })
  return {
    count: s.length,
    totalValue: sum(p => p.currentValue),
    totalClause: sum(p => p.buyoutClause),
    totalWeekly: sum(p => p.weeklyDelta),
    locked: s.filter(isLocked).length,
    counts
  }
})

const sortKey = ref('currentValue')
const sortDir = ref<'asc' | 'desc'>('desc')
const setSort = (k: string) => {
  if (sortKey.value === k) sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  else { sortKey.value = k; sortDir.value = k === 'name' ? 'asc' : 'desc' }
}
const sortedSquad = computed(() => {
  const arr = [...(squad.value || [])]
  const k = sortKey.value, mul = sortDir.value === 'asc' ? 1 : -1
  return arr.sort((a: any, b: any) => {
    if (k === 'name') return String(a.name).localeCompare(String(b.name)) * mul
    const av = a[k] ?? Number.NEGATIVE_INFINITY, bv = b[k] ?? Number.NEGATIVE_INFINITY
    return (av - bv) * mul
  })
})
</script>

<template>
  <section class="space-y-4">
    <div>
      <h1 class="text-2xl font-extrabold tracking-tight">Rivales</h1>
      <p class="text-sm text-muted">Los managers de tu liga. Pulsa un perfil para ver su desglose y su plantilla.</p>
    </div>

    <div v-if="pending" class="grid grid-cols-2 gap-3 sm:grid-cols-3">
      <div v-for="i in 6" :key="i" class="h-28 animate-pulse rounded-card bg-white/5" />
    </div>
    <AppError v-else-if="error" />

    <template v-else>
      <!-- Tarjetas de perfil de manager -->
      <div class="grid grid-cols-2 gap-3 sm:grid-cols-3">
        <button
          v-for="m in managers"
          :key="m.teamId"
          type="button"
          class="card group relative overflow-hidden p-4 text-left transition duration-150 hover:-translate-y-0.5 hover:shadow-lg"
          :class="selected === m.teamId ? 'ring-2 ring-brand' : 'ring-1 ring-transparent'"
          @click="selectManager(m.teamId)"
        >
          <span
            class="absolute right-3 top-3 rounded-full border px-2 py-0.5 text-[11px] font-extrabold tabular-nums"
            :class="m.rank <= 3 ? 'border-gold bg-gold text-ink-900' : 'border-gold/40 bg-gold/15 text-amber-700'"
          >#{{ m.rank }}</span>
          <div class="flex items-center gap-3">
            <span class="grid h-12 w-12 shrink-0 place-items-center rounded-xl text-lg font-extrabold text-white" :style="{ backgroundColor: avatarColor(m) }">
              {{ initials(m.managerName) }}
            </span>
            <div class="min-w-0 pr-6">
              <div class="truncate font-extrabold leading-tight text-ink-900">{{ m.managerName }}</div>
              <div class="truncate text-xs text-ink-600">{{ m.teamName || '—' }}</div>
            </div>
          </div>
          <div class="mt-3 flex gap-4 border-t border-ink-900/10 pt-3">
            <div>
              <div class="text-[10px] uppercase tracking-wide text-ink-600">Puntos</div>
              <div class="font-extrabold tabular-nums text-ink-900">{{ points(m.points) }}</div>
            </div>
            <div>
              <div class="text-[10px] uppercase tracking-wide text-ink-600">Valor equipo</div>
              <div class="font-extrabold tabular-nums text-amber-700">{{ eur(m.teamValue, { compact: true }) }}</div>
            </div>
          </div>
        </button>
      </div>

      <AppEmpty
        v-if="!selected"
        icon="⚔️"
        title="Elige un rival"
        hint="Pulsa el perfil de un manager para ver su desglose de estadísticas y su plantilla."
      />

      <div v-else-if="loadingSquad" class="h-64 animate-pulse rounded-card bg-white/5" />

      <AppEmpty v-else-if="!squad || squad.length === 0" title="Sin jugadores" />

      <div v-else class="space-y-3">
        <!-- Cabecera del rival -->
        <div v-if="selectedManager" class="card flex items-center gap-4 p-5">
          <span class="grid h-14 w-14 shrink-0 place-items-center rounded-2xl text-xl font-extrabold text-white" :style="{ backgroundColor: avatarColor(selectedManager) }">
            {{ initials(selectedManager.managerName) }}
          </span>
          <div class="min-w-0">
            <h2 class="truncate text-xl font-extrabold text-ink-900">{{ selectedManager.managerName }}</h2>
            <div class="truncate text-xs text-ink-600">{{ selectedManager.teamName || '—' }}</div>
          </div>
          <div class="ml-auto flex gap-6 text-right">
            <div>
              <div class="text-[10px] uppercase tracking-wide text-ink-600">Ranking</div>
              <div class="text-lg font-extrabold tabular-nums text-ink-900">#{{ selectedManager.rank }}</div>
            </div>
            <div>
              <div class="text-[10px] uppercase tracking-wide text-ink-600">Puntos</div>
              <div class="text-lg font-extrabold tabular-nums text-ink-900">{{ points(selectedManager.points) }}</div>
            </div>
            <div>
              <div class="text-[10px] uppercase tracking-wide text-ink-600">Valor equipo</div>
              <div class="text-lg font-extrabold tabular-nums text-amber-700">{{ eur(selectedManager.teamValue, { compact: true }) }}</div>
            </div>
          </div>
        </div>

        <!-- Desglose de estadísticas (de su plantilla) -->
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatTile label="Valor plantilla" :value="eur(breakdown.totalValue, { compact: true })" :sub="`${breakdown.count} jugadores`" tone="gold" />
          <StatTile label="Jugadores" :value="String(breakdown.count)" sub="en plantilla" />
          <StatTile label="Cláusula total" :value="eur(breakdown.totalClause, { compact: true })" :sub="`${breakdown.locked} blindados`" />
          <StatTile label="Variación semana" :value="signed(breakdown.totalWeekly, { compact: true })" :sub="'valor de mercado'" :tone="breakdown.totalWeekly > 0 ? 'up' : breakdown.totalWeekly < 0 ? 'down' : 'default'" />
        </div>

        <!-- Reparto por posición -->
        <div class="flex flex-wrap gap-2">
          <span v-for="(c, k) in breakdown.counts" :key="k" class="panel inline-flex items-center gap-2 px-3 py-2 text-sm font-bold">
            <span class="h-2.5 w-2.5 rounded-full" :class="posDot(k)" />
            {{ k }}<span class="font-semibold text-muted">{{ c }}</span>
          </span>
        </div>

        <!-- Plantilla del rival (como se ve ahora) -->
        <div class="card overflow-hidden">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[760px] text-sm">
              <thead>
                <tr class="border-b border-ink-900/10">
                  <SortTh col-key="name" :active="sortKey" :dir="sortDir" @sort="setSort">Jugador</SortTh>
                  <SortTh col-key="currentValue" align="right" :active="sortKey" :dir="sortDir" @sort="setSort">Valor</SortTh>
                  <SortTh col-key="dailyDelta" align="right" :active="sortKey" :dir="sortDir" @sort="setSort">Día</SortTh>
                  <SortTh col-key="weeklyDelta" align="right" :active="sortKey" :dir="sortDir" @sort="setSort">Semana</SortTh>
                  <SortTh col-key="buyoutClause" align="right" :active="sortKey" :dir="sortDir" @sort="setSort">Cláusula</SortTh>
                </tr>
              </thead>
              <tbody class="divide-y divide-ink-900/5">
                <tr v-for="r in sortedSquad" :key="r.externalId" class="hover:bg-ink-900/[0.02]">
                  <td class="px-4 py-3">
                    <div
                      class="flex items-center gap-3"
                      :class="r.playerId ? 'cursor-pointer' : ''"
                      @click="r.playerId && open(r.playerId)"
                    >
                      <span class="pill shrink-0" :class="posColor(r.position)">{{ posShort(r.position) }}</span>
                      <div class="min-w-0">
                        <div class="truncate font-semibold text-ink-900" :class="r.playerId ? 'hover:underline' : ''">{{ r.name }}</div>
                        <div class="truncate text-xs text-ink-600">{{ r.team || '—' }}</div>
                      </div>
                      <div class="ml-auto shrink-0">
                        <img
                          v-if="r.imageUrl"
                          :src="r.imageUrl"
                          :alt="r.name"
                          class="h-11 w-11 rounded-full bg-ink-900/5 object-cover object-top ring-1 ring-ink-900/10"
                          @error="(e) => ((e.target as HTMLImageElement).style.display = 'none')"
                        />
                      </div>
                    </div>
                  </td>
                  <td class="px-4 py-3 text-right font-semibold tabular-nums text-ink-900">{{ eur(r.currentValue) }}</td>
                  <td class="px-4 py-3 text-right"><DeltaBadge :value="r.dailyDelta" compact /></td>
                  <td class="px-4 py-3 text-right"><DeltaBadge :value="r.weeklyDelta" compact /></td>
                  <td class="px-4 py-3 text-right">
                    <div class="font-semibold tabular-nums text-ink-900">{{ eur(r.buyoutClause) }}</div>
                    <div class="text-[11px]" :class="clause(r).locked ? 'text-amber-600' : 'text-emerald-600'">
                      {{ clause(r).label }}
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </template>
  </section>
</template>
