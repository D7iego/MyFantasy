<script setup lang="ts">
interface Manager { teamId: string; managerName: string; teamName: string | null }
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

const posShort = (p: string) =>
  ({ Portero: 'POR', Defensa: 'DEF', Centrocampista: 'MED', Delantero: 'DEL' } as Record<string, string>)[p] || p
const posColor = (p: string) =>
  ({
    Portero: 'bg-amber-100 text-amber-700',
    Defensa: 'bg-sky-100 text-sky-700',
    Centrocampista: 'bg-emerald-100 text-emerald-700',
    Delantero: 'bg-rose-100 text-rose-700'
  } as Record<string, string>)[p] || 'bg-gray-100 text-gray-600'

const clause = (r: RivalPlayer) => {
  const end = r.buyoutClauseLockedEndTime ? new Date(r.buyoutClauseLockedEndTime) : null
  const locked = (end && end.getTime() > Date.now()) || r.isShielded
  return { locked, label: locked ? (end ? `Blindada hasta ${date(r.buyoutClauseLockedEndTime)}` : 'Blindada') : 'Clausulable' }
}

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
      <p class="text-sm text-muted">Plantillas de los demás managers de tu liga, con valor y estado de cláusula.</p>
    </div>

    <div v-if="pending" class="h-12 animate-pulse rounded-xl bg-white/5" />
    <AppError v-else-if="error" />

    <template v-else>
      <div class="flex items-center gap-3">
        <label class="section-label">Manager</label>
        <select
          v-model="selected"
          class="rounded-xl border border-white/10 bg-ink-800 px-3 py-2 text-sm text-white outline-none focus:border-brand"
        >
          <option value="">Elige un manager…</option>
          <option v-for="m in managers" :key="m.teamId" :value="m.teamId">
            {{ m.managerName }}{{ m.teamName ? ` · ${m.teamName}` : '' }}
          </option>
        </select>
      </div>

      <AppEmpty
        v-if="!selected"
        icon="⚔️"
        title="Elige un manager"
        hint="Selecciona un rival para ver su plantilla y sus cláusulas."
      />

      <div v-else-if="loadingSquad" class="h-64 animate-pulse rounded-card bg-white/5" />

      <AppEmpty v-else-if="!squad || squad.length === 0" title="Sin jugadores" />

      <div v-else class="card overflow-hidden">
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
    </template>
  </section>
</template>
