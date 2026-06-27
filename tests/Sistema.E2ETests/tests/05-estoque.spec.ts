import { test, expect } from '@playwright/test'
import { login, irPara } from './helpers'

test.describe('Estoque', () => {
  test.beforeEach(async ({ page }) => { await login(page) })

  test('painel de estoque exibe cards', async ({ page }) => {
    await irPara(page, '/estoque')
    await page.waitForTimeout(1500)
    const cards = page.locator('.v-card')
    await expect(cards.first()).toBeVisible({ timeout: 8_000 })
  })

  test('posição de estoque carrega tabela', async ({ page }) => {
    await irPara(page, '/estoque/posicao')
    await page.waitForTimeout(2000)
    await expect(page.locator('.v-data-table').first()).toBeVisible({ timeout: 10_000 })
  })

  test('editor de etiquetas carrega', async ({ page }) => {
    await irPara(page, '/estoque/etiquetas')
    await page.waitForTimeout(1000)
    await expect(page.locator('text=Editor de Etiquetas')).toBeVisible({ timeout: 5_000 })
  })

  test('transferências carrega', async ({ page }) => {
    await irPara(page, '/estoque/transferencias')
    await page.waitForTimeout(1500)
    const btn = page.locator('button:has-text("Nova Transferência")')
    await expect(btn).toBeVisible({ timeout: 8_000 })
  })
})
