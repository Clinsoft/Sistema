import { test, expect } from '@playwright/test'
import { login, irPara } from './helpers'

test.describe('Dashboard', () => {
  test.beforeEach(async ({ page }) => { await login(page) })

  test('exibe cards de totalizadores', async ({ page }) => {
    await irPara(page, '/')
    // Cards do dashboard devem estar visíveis
    const cards = page.locator('.v-card')
    await expect(cards.first()).toBeVisible()
  })

  test('navegação para PDV via menu', async ({ page }) => {
    await page.locator('text=PDV').first().click()
    await page.waitForTimeout(1000)
    await expect(page).toHaveURL(/#\/pdv/)
  })

  test('navegação para Estoque via menu', async ({ page }) => {
    await page.locator('text=Estoque').first().click()
    await page.waitForTimeout(1000)
    await expect(page).toHaveURL(/#\/estoque/)
  })

  test('navegação para Financeiro via menu', async ({ page }) => {
    await page.locator('text=Financeiro').first().click()
    await page.waitForTimeout(1000)
    await expect(page).toHaveURL(/#\/financeiro/)
  })
})
