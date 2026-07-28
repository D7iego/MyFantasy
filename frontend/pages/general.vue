<script setup lang="ts">
import type { PlayerRow } from '~/components/OverviewTable.vue'

interface Team {
  id: string
  name: string
  badgeUrl: string | null
}
interface TeamAggregate {
  teamId: string
  playerCount: number
  avgDailyDelta: number
  avgWeeklyDelta: number
  avgDailyPct: number
  avgWeeklyPct: number
}
interface Overview {
  players: PlayerRow[]
  teamAggregate: TeamAggregate | null
}

const api = useApi()
const { signed } = useFormat()

const selectedTeamId = ref<string | null>(null)

const { data: teams } = await useAsyncData('teams', () => api.get<Team[]>('/api/teams'))

const { data, pending, error } = await useAsyncData(
  'overview',
  () =>
    api.get<Overview>(
      `/api/players/all${selectedTeamId.value ? `?teamId=${encodeURIComponent(selectedTeamId.value)}` : ''}`
    ),
  { watch: [selectedTeamId] }
)

const selectedTeam = computed(() => teams.value?.find((t) => t.id === selectedTeamId.value) || null)

const toggleTeam = (id: string) => {
  selectedTeamId.value = selectedTeamId.value === id ? null : id
}

const pct = (v: number) => `${v > 0 ? '+' : v < 0 ? '−' : ''}${Math.abs(v).toFixed(2)}%`
const pctClass = (v: number) => (v > 0 ? 'text-up' : v < 0 ? 'text-down' : 'text-muted')
</script>

<template>
  <section class="space-y-4">
    <div>
      <h1 class="text-2xl font-extrabold tracking-tight">General</h1>
      <p class="text-sm text-muted">
        Todos los jugadores de la liga con su variación y tendencia. Filtra por equipo con los escudos.
      </p>
    </div>

    <!-- Filtro visual por equipo -->
    <div v-if="teams && teams.length" class="flex gap-2 overflow-x-auto pb-1">
      <button
        v-for="t in teams"
        :key="t.id"
        class="flex shrink-0 items-center gap-2 rounded-xl border px-3 py-2 text-sm font-semibold transition"
        :class="selectedTeamId === t.id
          ? 'border-brand bg-brand/10 text-white'
          : 'border-white/10 text-muted hover:text-white'"
        :title="t.name"
        @click="toggleTeam(t.id)"
      >
        <img
          v-if="t.badgeUrl"
          :src="t.badgeUrl"
          :alt="t.name"
          class="h-6 w-6 object-contain"
          @error="(e) => ((e.target as HTMLImageElement).style.display = 'none')"
        />
        <span class="whitespace-nowrap">{{ t.name }}</span>
      </button>
    </div>

    <!-- Tendencia agregada del equipo seleccionado -->
    <div
      v-if="data?.teamAggregate && selectedTeam"
      class="panel flex flex-wrap items-center justify-between gap-3 p-4"
    >
      <div class="flex items-center gap-3">
        <img
          v-if="selectedTeam.badgeUrl"
          :src="selectedTeam.badgeUrl"
          :alt="selectedTeam.name"
          class="h-8 w-8 object-contain"
        />
        <div>
          <div class="font-bold text-white">{{ selectedTeam.name }}</div>
          <div class="text-xs text-muted">{{ data.teamAggregate.playerCount }} jugadores</div>
        </div>
      </div>
      <div class="flex gap-6 text-right">
        <div>
          <div class="section-label">De media hoy</div>
          <div class="font-extrabold tabular-nums" :class="pctClass(data.teamAggregate.avgDailyPct)">
            {{ pct(data.teamAggregate.avgDailyPct) }}
          </div>
          <div class="text-xs text-muted tabular-nums">{{ signed(data.teamAggregate.avgDailyDelta) }}</div>
        </div>
        <div>
          <div class="section-label">De media esta semana</div>
          <div class="font-extrabold tabular-nums" :class="pctClass(data.teamAggregate.avgWeeklyPct)">
            {{ pct(data.teamAggregate.avgWeeklyPct) }}
          </div>
          <div class="text-xs text-muted tabular-nums">{{ signed(data.teamAggregate.avgWeeklyDelta) }}</div>
        </div>
      </div>
    </div>

    <div v-if="pending" class="h-64 animate-pulse rounded-card bg-white/5" />
    <AppError v-else-if="error" />
    <AppEmpty
      v-else-if="!data || data.players.length === 0"
      icon="📋"
      title="Sin jugadores"
      hint="Sincroniza para traer los jugadores de la competición y sus precios."
    />
    <OverviewTable v-else :rows="data.players" />
  </section>
</template>
