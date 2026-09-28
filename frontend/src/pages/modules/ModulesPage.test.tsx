// @vitest-environment jsdom
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { moduleApi } from '../../services/moduleApi'
import { ModulesPage } from './ModulesPage'

vi.mock('../../services/moduleApi', () => ({ moduleApi: {
  list: vi.fn(), get: vi.fn(), validate: vi.fn(), updateConcept: vi.fn(),
} }))

const concept = {
  id: 'c1', moduleId: 'm1', name: 'DNS', definition: 'Resolve nomes de domínio.', isActive: true,
  createdAtUtc: '', updatedAtUtc: '', keywords: ['nome', 'endereço'], clues: ['pista 1', 'pista 2', 'pista 3'], prerequisiteIds: [],
  recognitionActivities: [0, 1, 2].map(index => ({ id: `tf${index}`, statement: `Afirmação ${index}`, isCorrect: true, explanation: 'Explicação', isActive: true })),
  fillBlankActivities: [0, 1].map(index => ({ id: `fb${index}`, text: 'O {{1}}.', isActive: true, answers: [{ slotNumber: 1, correctText: 'DNS' }], distractors: [] })),
  orderingActivities: [],
}
const module = { id: 'm1', title: 'Redes', description: null, subject: 'Redes', version: 1, status: 'Draft' as const, createdAtUtc: '', updatedAtUtc: '', concepts: [concept] }
const api = vi.mocked(moduleApi)

beforeEach(() => {
  vi.clearAllMocks()
  api.list.mockResolvedValue([{ ...module, activeConceptCount: 1 }])
  api.get.mockResolvedValue(module)
  api.validate.mockResolvedValue({ isValid: false, errors: [] })
})
afterEach(() => cleanup())

describe('concept editor sections and contrast', () => {
  it('uses a high-contrast selected concept card and collapsible content sections with completeness indicators', async () => {
    const user = userEvent.setup()
    render(<ModulesPage />)
    await user.click(await screen.findByRole('button', { name: /Redes/ }))
    await user.click(await screen.findByRole('button', { name: /DNS/ }))
    const card = screen.getByRole('button', { name: /DNS/ })
    expect(card.getAttribute('aria-pressed')).toBe('true')
    expect(card.className).toContain('selectedConcept')

    const basics = screen.getByText('Informações básicas').closest('details')!
    expect(basics.open).toBe(true)
    expect(screen.getByText(/Keywords —/).closest('details')!.open).toBe(false)
    expect(screen.getByText(/Pistas —/).closest('details')!.open).toBe(false)
    expect(screen.getByText(/Verdadeiro\/Falso/).textContent).toContain('3/3')
    expect(screen.getByText(/Completar lacunas/).textContent).toContain('2/3')
    expect(screen.getByText(/incompleto/).textContent).toContain('2/3')

    await user.click(screen.getByText(/Completar lacunas/))
    expect(screen.getByText(/Completar lacunas/).closest('details')!.open).toBe(true)
    const nameInput = screen.getByRole('textbox', { name: 'Nome' })
    await user.clear(nameInput)
    await user.type(nameInput, 'DNS atualizado')
    api.updateConcept.mockResolvedValue({ ...concept, name: 'DNS atualizado' })
    await user.click(screen.getByRole('button', { name: 'Salvar conceito' }))
    await waitFor(() => expect(api.updateConcept).toHaveBeenCalledWith('m1', 'c1', expect.objectContaining({ name: 'DNS atualizado' })))
  })
})
