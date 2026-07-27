<script setup lang="ts">
defineProps<{ title?: string; hint?: string; icon?: string }>()
</script>

<template>
  <div class="panel flex flex-col items-center gap-2 px-6 py-12 text-center">
    <div class="text-3xl">{{ icon || '📭' }}</div>
    <h3 class="font-semibold text-white">{{ title || 'Nada por aquí todavía' }}</h3>
    <p v-if="hint" class="max-w-sm text-sm text-muted">{{ hint }}</p>
  </div>
</template>
