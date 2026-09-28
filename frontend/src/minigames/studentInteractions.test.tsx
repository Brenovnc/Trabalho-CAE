// @vitest-environment jsdom
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ExposureGame } from './exposure/ExposureGame'
import { TrueFalseGame } from './true-false/TrueFalseGame'
import { FillBlankGame } from './fill-blank/FillBlankGame'
import { GuessConceptGame } from './guess-concept/GuessConceptGame'
import { OrderingGame } from './ordering/OrderingGame'
import type { ExposureActivity, FillBlankActivity, GuessConceptActivity, OrderingActivity, TrueFalseActivity } from '../types/learning'

const base = { presentationId: 'p1', conceptId: 'c1', conceptName: 'DNS', startedAtUtc: '2026-09-27T12:00:00Z' }

describe('minigame interactions', () => {
  it('reveals exposure through API callback and only enables completion after all keywords are returned', async () => {
    const user = userEvent.setup()
    const onReveal = vi.fn().mockResolvedValue(undefined)
    const onSubmit = vi.fn()
    const hidden: ExposureActivity = { ...base, type: 'EXPOSURE', payload: {
      keywordCount: 1, revealedCount: 0, revealedKeywords: [], segments: [{ text: 'The ', keywordIndex: null }, { text: null, keywordIndex: 0 }],
    } }
    const view = render(<ExposureGame activity={hidden} disabled={false} onReveal={onReveal} onSubmit={onSubmit} />)
    expect(screen.getByText('????')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Concluir exposição' }).hasAttribute('disabled')).toBe(true)
    await user.click(screen.getByRole('button', { name: 'Revelar próxima palavra' }))
    expect(onReveal).toHaveBeenCalledOnce()
    const revealed: ExposureActivity = { ...hidden, payload: {
      keywordCount: 1, revealedCount: 1, revealedKeywords: ['DNS'], segments: [{ text: 'The ', keywordIndex: null }, { text: 'DNS', keywordIndex: 0 }],
    } }
    view.rerender(<ExposureGame activity={revealed} disabled={false} onReveal={onReveal} onSubmit={onSubmit} />)
    expect(screen.getByText('DNS')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Concluir exposição' }))
    expect(onSubmit).toHaveBeenCalledWith({ completed: true })
  })

  it('sends the selected true or false answer and disables both actions while waiting', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    const activity: TrueFalseActivity = { ...base, type: 'TRUE_FALSE', payload: { statement: 'DNS resolves names.' } }
    const view = render(<TrueFalseGame activity={activity} disabled={false} onSubmit={onSubmit} />)
    expect(screen.queryByText(/correct|explanation/i)).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Verdadeiro' }))
    expect(onSubmit).toHaveBeenCalledWith({ choice: true })
    view.rerender(<TrueFalseGame activity={activity} disabled onSubmit={onSubmit} />)
    expect(screen.getByRole('button', { name: 'Falso' }).hasAttribute('disabled')).toBe(true)
  })

  it('supports fill blank entirely by touch/click, blocks incomplete answers, replaces and removes words', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    const activity: FillBlankActivity = { ...base, type: 'FILL_BLANK', payload: { text: 'O {{1}} resolve {{2}}.', slotNumbers: [1, 2], options: ['DNS', 'nomes', 'TCP'] } }
    render(<FillBlankGame activity={activity} disabled={false} onSubmit={onSubmit} />)
    const submit = screen.getByRole('button', { name: 'Enviar resposta' })
    expect(submit.hasAttribute('disabled')).toBe(true)
    await user.click(screen.getByRole('button', { name: 'DNS' }))
    await user.click(screen.getByRole('button', { name: 'Lacuna 1, vazia' }))
    await user.click(screen.getByRole('button', { name: 'nomes' }))
    await user.click(screen.getByRole('button', { name: 'Lacuna 2, vazia' }))
    expect(submit.hasAttribute('disabled')).toBe(false)
    await user.click(screen.getByRole('button', { name: 'Remover palavra da lacuna 1' }))
    expect(submit.hasAttribute('disabled')).toBe(true)
    await user.click(screen.getByRole('button', { name: 'TCP' }))
    await user.click(screen.getByRole('button', { name: /Lacuna 1/ }))
    await user.click(submit)
    expect(onSubmit).toHaveBeenCalledWith({ answers: [{ slotNumber: 1, text: 'TCP' }, { slotNumber: 2, text: 'nomes' }] })
  })

  it('reveals guess clues from the service and submits with Enter without exposing the answer', async () => {
    const user = userEvent.setup()
    const onReveal = vi.fn().mockResolvedValue(undefined)
    const onSubmit = vi.fn()
    const activity: GuessConceptActivity = { ...base, type: 'GUESS_CONCEPT', payload: { clueCount: 2, clues: ['Opera na camada de aplicação.'] } }
    const view = render(<GuessConceptGame activity={activity} disabled={false} onReveal={onReveal} onSubmit={onSubmit} />)
    expect(screen.queryByText('DNS', { selector: 'p' })).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Revelar próxima pista' }))
    expect(onReveal).toHaveBeenCalledOnce()
    view.rerender(<GuessConceptGame activity={{ ...activity, payload: { ...activity.payload, clues: [...activity.payload.clues, 'Relaciona domínio e IP.'] } }} disabled={false} onReveal={onReveal} onSubmit={onSubmit} />)
    const answer = screen.getByRole('textbox', { name: 'Sua resposta' })
    await user.type(answer, 'dns{enter}')
    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith({ text: 'dns' }, 1))
  })

  it('reorders with accessible buttons and sends IDs in their new sequence', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    const activity: OrderingActivity = { ...base, type: 'ORDERING', payload: { instruction: 'Ordene os passos', items: [{ id: 'a', text: 'Primeiro' }, { id: 'b', text: 'Segundo' }] } }
    render(<OrderingGame activity={activity} disabled={false} onSubmit={onSubmit} />)
    await user.click(screen.getByRole('button', { name: 'Mover Segundo para cima' }))
    await user.click(screen.getByRole('button', { name: 'Enviar sequência' }))
    expect(onSubmit).toHaveBeenCalledWith({ itemIds: ['b', 'a'] })
  })
})

