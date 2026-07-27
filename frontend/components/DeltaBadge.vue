<script setup lang="ts">
const props = defineProps<{ value?: number | null; compact?: boolean }>()
const { signed } = useFormat()

const tone = computed(() => {
  if (props.value === null || props.value === undefined || props.value === 0) return 'muted'
  return props.value > 0 ? 'up' : 'down'
})
</script>

<template>
  <span
    class="pill"
    :class="{
      'bg-up/10 text-up': tone === 'up',
      'bg-down/10 text-down': tone === 'down',
      'bg-black/5 text-ink-600': tone === 'muted'
    }"
  >
    <span v-if="tone === 'up'">▲</span>
    <span v-else-if="tone === 'down'">▼</span>
    {{ signed(value, { compact }) }}
  </span>
</template>
