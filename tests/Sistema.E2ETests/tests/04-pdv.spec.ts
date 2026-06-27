import { test, expect } from '@playwright/test'
import { login, irPara } from './helpers'

test.describe('PDV — Ponto de Venda', () => {
  test.beforeEach(async ({ page }) => { await login(page) })

  test('tela do PDV carrega corretamente', async ({ page }) => {
    await irPara(page, '/pdv')
    await page.waitForTimeout(1500)
    // Deve haver campo de busca/código de barras
    const campoBusca = page.locator('input[placeholder*="barra"], input[placeholder*="produto"], input[placeholder*="Produto"]')
    await expect(campoBusca.first()).toBeVisible({ timeout: 8_000 })
  })

  test('histórico de vendas carrega', async ({ page }) => {
    await irPara(page, '/pdv/vendas')
    await page.waitForTimeout(2000)
    await expect(page.locator('.v-data-table').first()).toBeVisible({ timeout: 10_000 })
  })

  test('sessões de caixa carrega', async ({ page }) => {
    await irPara(page, '/pdv/sessoes')
    await page.waitForTimeout(1500)
    // Botão "Abrir Caixa" ou "Fechar Caixa" deve aparecer
    const btn = page.locator('button:has-text("Abrir Caixa"), button:has-text("Fechar Caixa")')
    await expect(btn.first()).toBeVisible({ timeout: 8_000 })
  })

  test('devoluções carrega', async ({ page }) => {
    await irPara(page, '/pdv/devolucoes')
    await page.waitForTimeout(1500)
    await expect(page.locator('.v-data-table').first()).toBeVisible({ timeout: 8_000 })
  })
})
