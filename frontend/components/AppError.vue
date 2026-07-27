<script setup lang="ts">
defineProps<{ message?: string }>()
const api = useApi()
</script>

<template>
  <div class="panel flex flex-col items-center gap-2 px-6 py-12 text-center">
    <div class="text-3xl">🔌</div>
    <h3 class="font-semibold text-white">No se pudo cargar</h3>
    <p class="max-w-sm text-sm text-muted">
      {{ message || `¿Está el backend arrancado en ${api.base}? Ejecuta el backend y pulsa «Sincronizar».` }}
    </p>
  </div>
</template>
