<script setup lang="ts">
/** Iconos vectoriales limpios para stats (sustituyen a los emojis ⚽ 🅰 …). */
defineProps<{ name: 'goal' | 'assist' | 'minutes' | 'points' | 'avg' | 'chart'; size?: number }>()
</script>

<template>
  <svg
    :width="size ?? 16" :height="size ?? 16" viewBox="0 0 24 24"
    fill="none" stroke="currentColor" stroke-width="1.8"
    stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"
  >
    <!-- Goles: balón -->
    <template v-if="name === 'goal'">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7.2l3.2 2.3-1.2 3.7h-4L8.8 9.5 12 7.2z" />
      <path d="M12 3v4.2M4.6 8.9l3.5 1.1M6.9 18l2.3-3.2M17.1 18l-2.3-3.2M19.4 8.9l-3.5 1.1" stroke-width="1.4" />
    </template>
    <!-- Asistencias: bota -->
    <template v-else-if="name === 'assist'">
      <path d="M3 7h6l2.5 3.2c2 .3 4 .9 6 2.1 1.6 1 2.5 2 2.5 3.2v1.5H4a1 1 0 0 1-1-1V7z" />
      <path d="M3 13h7" stroke-width="1.4" />
    </template>
    <!-- Minutos: reloj -->
    <template v-else-if="name === 'minutes'">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7.5V12l3 1.8" />
    </template>
    <!-- Puntos: estrella -->
    <template v-else-if="name === 'points'">
      <path d="M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8-5.2-2.7-5.2 2.7 1-5.8-4.3-4.1 5.9-.9z" />
    </template>
    <!-- Media: onda -->
    <template v-else-if="name === 'avg'">
      <path d="M4 15c3-8 5-8 8 0s5 8 8 0" />
    </template>
    <!-- Pestaña stats: barras -->
    <template v-else>
      <path d="M4 20V10M10 20V4M16 20v-7M22 20H2" stroke-width="2" />
    </template>
  </svg>
</template>
