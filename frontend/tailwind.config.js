/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './components/**/*.{vue,js,ts}',
    './layouts/**/*.vue',
    './pages/**/*.vue',
    './app.vue',
    './plugins/**/*.{js,ts}'
  ],
  theme: {
    extend: {
      colors: {
        // Paleta inspirada en el Fantasy oficial de LaLiga.
        brand: {
          DEFAULT: '#FF3D4D',
          400: '#FF5C6A',
          500: '#FF3D4D',
          600: '#EC2E40',
          700: '#C82233'
        },
        // Fondos oscuros (casi negro azulado).
        ink: {
          950: '#0A0B0F',
          900: '#0E0F14',
          800: '#14161D',
          700: '#1B1E27',
          600: '#252935'
        },
        muted: '#8B8F9C',
        gold: '#F7C948',
        up: '#18C29C',
        down: '#FF5468'
      },
      fontFamily: {
        sans: ['Sora', 'ui-sans-serif', 'system-ui', 'sans-serif']
      },
      borderRadius: {
        card: '1rem'
      },
      boxShadow: {
        card: '0 6px 20px -8px rgba(0,0,0,0.45)'
      }
    }
  },
  plugins: []
}
