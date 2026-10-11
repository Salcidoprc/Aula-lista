import { defineConfig, devices } from '@playwright/test';

/** Pruebas end-to-end contra la app completa (docker compose en CI). */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? 'github' : 'list',
  use: {
    baseURL: process.env['BASE_URL'] ?? 'http://localhost:8080',
    locale: 'es-MX',
    timezoneId: 'America/Mexico_City',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'celular', use: { ...devices['Pixel 7'] } },
    { name: 'celular-movimiento-reducido', use: { ...devices['Pixel 7'], reducedMotion: 'reduce' } },
    { name: 'escritorio', use: { ...devices['Desktop Chrome'] } },
  ],
});
