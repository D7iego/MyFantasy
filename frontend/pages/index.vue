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

const busy = ref<number | null>(null)
const selectLeague = async (league: League) => {
  if (league.isDefault || busy.value) return
  busy.value = league.id
  try {
    await api.put(`/api/leagues/${league.id}/default`)
    await refresh()
  } finally {
    busy.value = null
  }
}

// Borrado con confirmación
const toDelete = ref<League | null>(null)
const deleting = ref(false)
const confirmDelete = async () => {
  if (!toDelete.value) return
  deleting.value = true
  try {
    await api.delete(`/api/leagues/${toDelete.value.id}`)
    toDelete.value = null
    await refresh()
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <section class="space-y-4">
    <div>
      <h1 class="text-2xl font-extrabold tracking-tight">Mis ligas</h1>
      <p class="text-sm text-muted">Toca una liga para marcarla como activa; alimenta el resto de pestañas.</p>
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
      <div
        v-for="league in leagues"
        :key="league.id"
        role="button"
        tabindex="0"
        class="card group flex cursor-pointer items-center gap-4 p-4 transition"
        :class="league.isDefault ? 'ring-2 ring-brand' : 'ring-1 ring-transparent hover:ring-ink-900/10'"
        @click="selectLeague(league)"
        @keydown.enter.prevent="selectLeague(league)"
        @keydown.space.prevent="selectLeague(league)"
      >
        <div
          class="grid h-12 w-12 shrink-0 place-items-center rounded-xl text-lg font-extrabold text-white"
          :class="league.isDefault ? 'bg-brand' : 'bg-ink-700'"
        >
          {{ league.name.charAt(0).toUpperCase() }}
        </div>

        <div class="min-w-0 flex-1">
          <h3 class="truncate font-bold text-ink-900">{{ league.name }}</h3>
          <p class="text-xs text-ink-600">ID LaLiga: {{ league.externalId }}</p>
        </div>

        <!-- Borrar -->
        <button
          type="button"
          class="grid h-8 w-8 shrink-0 place-items-center rounded-lg text-ink-600 opacity-0 transition hover:bg-down/10 hover:text-down group-hover:opacity-100"
          title="Borrar liga"
          @click.stop="toDelete = league"
        >
          🗑
        </button>

        <!-- Radio visual -->
        <span
          class="grid h-6 w-6 shrink-0 place-items-center rounded-full border-2"
          :class="league.isDefault ? 'border-brand' : 'border-ink-900/20'"
        >
          <span v-if="busy === league.id" class="h-3 w-3 animate-ping rounded-full bg-brand" />
          <span v-else-if="league.isDefault" class="h-3 w-3 rounded-full bg-brand" />
        </span>
      </div>
    </div>

    <!-- Confirmación de borrado -->
    <Teleport to="body">
      <div
        v-if="toDelete"
        class="fixed inset-0 z-50 grid place-items-center bg-black/60 p-4"
        @click.self="toDelete = null"
      >
        <div class="w-full max-w-sm rounded-2xl border border-white/10 bg-ink-800 p-5">
          <h3 class="text-lg font-bold">Borrar liga</h3>
          <p class="mt-1 text-sm text-muted">
            ¿Seguro que quieres borrar <b class="text-white">{{ toDelete.name }}</b>? Se eliminarán sus
            fichajes y ventas registrados. El histórico de precios no se toca. Esta acción es irreversible.
          </p>
          <div class="mt-4 flex justify-end gap-2">
            <button class="btn-ghost" :disabled="deleting" @click="toDelete = null">Cancelar</button>
            <button
              class="inline-flex items-center gap-2 rounded-xl bg-down px-4 py-2.5 font-semibold text-white transition hover:brightness-110 disabled:opacity-50"
              :disabled="deleting"
              @click="confirmDelete"
            >
              {{ deleting ? 'Borrando…' : 'Borrar' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </section>
</template>
