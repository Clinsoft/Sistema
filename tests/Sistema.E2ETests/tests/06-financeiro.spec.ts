import { test, expect } from '@playwright/test'
import { login, irPara } from './helpers'

test.describe('Financeiro', () => {
  test.beforeEach(async ({ page }) => { await login(page) })

  test('painel financeiro exibe totalizadores', async ({ page }) => {
    await irPara(page, '/financeiro')
    await page.waitForTimeout(1500)
    const cards = page.locator('.v-card')
    await expect(cards.first()).toBeVisible({ timeout: 8_000 })
  })

  test('contas a receber carrega', async ({ page }) => {
    await irPara(page, '/financeiro/contas-receber')
    await page.waitForTimeout(2000)
    await expect(page.locator('.v-data-table').first()).toBeVisible({ timeout: 10_000 })
  })

  test('contas a pagar carrega', async ({ page }) => {
    await irPara(page, '/financeiro/contas-pagar')
    await page.waitForTimeout(2000)
    await expect(page.locator('.v-data-table').first()).toBeVisible({ timeout: 10_000 })
  })

  test('DRE carrega sem erro', async ({ page }) => {
    await irPara(page, '/financeiro/dre')
    await page.waitForTimeout(1500)
    // DRE deve ter algum título visível
    await expect(page.locator('h2, .text-h5, .text-h6').first()).toBeVisible({ timeout: 8_000 })
  })
})
