import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    strictPort: true,   // API khởi động Vite ở cổng này (SpaProxy); bận thì báo lỗi thay vì tự nhảy cổng
    proxy: {
      '/api': process.env.API_PROXY ?? 'http://localhost:5063',
      '/signalr': {
        target: process.env.API_PROXY ?? 'http://localhost:5063',
        ws: true,
      },
    },
  },
})
