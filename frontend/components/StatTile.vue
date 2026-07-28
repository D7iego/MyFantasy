<script setup lang="ts">
defineProps<{
  label: string
  value: string
  sub?: string
  tone?: 'default' | 'up' | 'down' | 'brand' | 'gold'
}>()
</script>

<template>
  <div class="panel p-4">
    <div class="section-label">{{ label }}</div>
    <div
      class="mt-1 text-2xl font-extrabold tracking-tight tabular-nums"
      :class="{
        'text-white': !tone || tone === 'default',
        'text-up': tone === 'up',
        'text-down': tone === 'down',
        'text-brand': tone === 'brand',
        'text-gold': tone === 'gold'
      }"
    >
      {{ value }}
    </div>
    <div v-if="sub" class="mt-0.5 text-xs text-muted">{{ sub }}</div>
  </div>
</template>
