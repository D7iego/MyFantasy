<script setup lang="ts">
interface League {
  id: number
  externalId: string
  name: string
  isDefault: boolean
  createdAt: string
}

const api = useApi()
const { data: leagues, pending, error, refresh } = await useAsyncData('leagues', () =>
  api.get<League[]>('/api/leagues')
)

const setting = ref<number | null>(null)
const setDefault = async (id: number) => {
  setting.value = id
  try {
    await api.put(`/api/leagues/${id}/default`)
    await refresh()
  } finally {
    setting.value = null
  }
}
</script>

<template>
  <section class="space-y-4">
    <div class="flex items-end justify-between">
      <div>
        <h1 class="text-2xl font-extrabold tracking-tight">Mis ligas</h1>
        <p class="text-sm text-muted">La liga por defecto alimenta el resto de pestañas.</p>
      </div>
    </div>

    <p class="section-label">Ligas</p>

    <div v-if="pending" class="grid gap-3 sm:grid-cols-2">
      <div v-for="i in 2" :key="i" class="h-24 animate-pulse rounded-card bg-white/5" />
    </div>

    <AppError v-else-if="error" />

    <AppEmpty
      v-else-if="!leagues || leagues.length === 0"
      title="Aún no hay ligas"
      hint="Pulsa «Sincronizar» arriba para traerlas de LaLiga."
    />

    <div v-else class="grid gap-3 sm:grid-cols-2">
      <article
        v-for="league in leagues"
        :key="league.id"
        class="card flex items-center gap-4 p-4"
      >
        <div
          class="grid h-12 w-12 shrink-0 place-items-center rounded-xl text-lg font-extrabold text-white"
          :class="league.isDefault ? 'bg-brand' : 'bg-ink-700'"
        >
          {{ league.name.charAt(0).toUpperCase() }}
        </div>

        <div class="min-w-0 flex-1">
          <div class="flex items-center gap-2">
            <h3 class="truncate font-bold text-ink-900">{{ league.name }}</h3>
            <span v-if="league.isDefault" class="pill bg-brand/10 text-brand">Por defecto</span>
          </div>
          <p class="text-xs text-ink-600">ID LaLiga: {{ league.externalId }}</p>
        </div>

        <button
          v-if="!league.isDefault"
          class="shrink-0 rounded-lg border border-ink-900/10 px-3 py-2 text-xs font-semibold text-ink-900 transition hover:bg-ink-900/5 disabled:opacity-50"
          :disabled="setting === league.id"
          @click="setDefault(league.id)"
        >
          {{ setting === league.id ? '…' : 'Hacer por defecto' }}
        </button>
      </article>
    </div>
  </section>
</template>
