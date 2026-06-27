import { test, expect } from '@playwright/test'
import { login, irPara } from './helpers'

test.describe('Cadastros', () => {
  test.beforeEach(async ({ page }) => { await login(page) })

  test('lista clientes', async ({ page }) => {
    await irPara(page, '/cadastros/clientes')
    await expect(page.locator('h2, .text-h5').first()).toBeVisible()
    // Tabela ou lista deve carregar
    await page.waitForTimeout(1500)
    const tabela = page.locator('.v-data-table, .v-list')
    await expect(tabela.first()).toBeVisible({ timeout: 8_000 })
  })

  test('abre dialog de novo cliente', async ({ page }) => {
    await irPara(page, '/cadastros/clientes')
    await page.locator('button:has-text("Novo Cliente"), button:has-text("Novo")').first().click()
    await expect(page.locator('.v-dialog')).toBeVisible({ timeout: 3_000 })
    const inputs = page.locator('.v-dialog input')
    await expect(inputs.first()).toBeVisible()
  })

  test('lista categorias', async ({ page }) => {
    await irPara(page, '/cadastros/categorias')
    await page.waitForTimeout(1500)
    await expect(page.locator('.v-data-table').first()).toBeVisible({ timeout: 8_000 })
  })

  test('lista produtos', async ({ page }) => {
    await irPara(page, '/estoque/produtos')
    await page.waitForTimeout(1500)
    await expect(page.locator('.v-data-table').first()).toBeVisible({ timeout: 8_000 })
  })
})
