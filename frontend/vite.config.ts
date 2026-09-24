import path from 'path';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  server: { proxy: { '/api': { target: 'http://127.0.0.1:5026', changeOrigin: true } } },
  preview: { proxy: { '/api': { target: 'http://127.0.0.1:5026', changeOrigin: true } } },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  optimizeDeps: {
    exclude: ['lucide-react'],
  },
});
