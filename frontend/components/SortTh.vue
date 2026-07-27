<script setup lang="ts">
defineProps<{
  colKey: string
  active: string
  dir: 'asc' | 'desc'
  align?: 'left' | 'right'
}>()
const emit = defineEmits<{ (e: 'sort', key: string): void }>()
</script>

<template>
  <th class="px-4 py-3" :class="align === 'right' ? 'text-right' : 'text-left'">
    <button
      class="inline-flex items-center gap-1 text-[11px] font-semibold uppercase tracking-wide text-ink-600 transition hover:text-ink-900"
      @click="emit('sort', colKey)"
    >
      <slot />
      <span class="text-[9px]" :class="active === colKey ? 'text-brand' : 'text-ink-900/25'">
        {{ active === colKey ? (dir === 'asc' ? '▲' : '▼') : '↕' }}
      </span>
    </button>
  </th>
</template>
