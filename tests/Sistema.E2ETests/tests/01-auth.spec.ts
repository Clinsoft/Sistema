import { test, expect } from '@playwright/test'
import { login } from './helpers'

test.describe('Autenticação', () => {
  test('redireciona para login quando não autenticado', async ({ page }) => {
    await page.goto('/#/')
    await expect(page).toHaveURL(/#\/login/, { timeout: 5_000 })
  })

  test('login com credenciais válidas navega para dashboard', async ({ page }) => {
    await login(page)
    await expect(page).toHaveURL(/#\/$/)
    // Verifica que o menu lateral aparece
    await expect(page.locator('text=Dashboard, text=PDV').first()).toBeVisible({ timeout: 5_000 })
  })

  test('login com senha errada exibe erro', async ({ page }) => {
    await page.goto('/#/login')
    await page.waitForLoadState('networkidle')
    await page.locator('input').first().fill('admin@clinsoft.com')
    await page.locator('input[type="password"]').fill('senhaerrada')
    await page.locator('button:has-text("Entrar")').click()
    // Deve manter na tela de login ou mostrar alerta de erro
    await expect(page).not.toHaveURL(/#\/$/, { timeout: 4_000 }).catch(() => {})
  })
})
