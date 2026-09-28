import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ActivityGame } from './ActivityGame'
import { formatElapsed } from '../components/SessionTimer'
import type { StudyActivity } from '../types/learning'

afterEach(() => vi.useRealTimers())
const base = { presentationId: 'presentation', conceptId: 'concept', conceptName: 'DNS', startedAtUtc: '2026-09-27T12:00:00Z' }
const props = { busy: false, onReveal: async () => {}, onSubmit: () => {} }

describe('student minigame rendering and timer', () => {
  it('keeps exposure keywords hidden until the API says they were revealed', () => {
    const activity: StudyActivity = { ...base, type: 'EXPOSURE', payload: {
      keywordCount: 1, revealedCount: 0, revealedKeywords: [], segments: [{ text: 'DNS maps ', keywordIndex: null }, { text: null, keywordIndex: 0 }],
    } }
    const markup = renderToStaticMarkup(createElement(ActivityGame, { ...props, activity }))
    expect(markup).toContain('DNS maps')
    expect(markup).toContain('Revelar próxima palavra')
    expect(markup).not.toContain('domain name')
    expect(markup).toContain('disabled=""')
  })

  it('shows only the true/false statement before submission', () => {
    const activity: StudyActivity = { ...base, type: 'TRUE_FALSE', payload: { statement: 'DNS resolves names.' } }
    const markup = renderToStaticMarkup(createElement(ActivityGame, { ...props, activity }))
    expect(markup).toContain('DNS resolves names.')
    expect(markup).toContain('Verdadeiro')
    expect(markup).not.toContain('isCorrect')
    expect(markup).not.toContain('Resposta correta')
  })

  it('renders fill blank slots and accessible ordering controls', () => {
    const fill: StudyActivity = { ...base, type: 'FILL_BLANK', payload: { text: 'O {{1}} resolve.', slotNumbers: [1], options: ['DNS', 'TCP'] } }
    const fillMarkup = renderToStaticMarkup(createElement(ActivityGame, { ...props, activity: fill }))
    expect(fillMarkup).toContain('Opções de resposta')
    expect(fillMarkup).toContain('Lacuna 1, vazia')
    expect(fillMarkup).toContain('Enviar resposta')

    const order: StudyActivity = { ...base, type: 'ORDERING', payload: { instruction: 'Ordene', items: [{ id: 'a', text: 'Primeiro' }, { id: 'b', text: 'Segundo' }] } }
    const orderMarkup = renderToStaticMarkup(createElement(ActivityGame, { ...props, activity: order }))
    expect(orderMarkup).toContain('Mover Segundo para cima')
    expect(orderMarkup).toContain('Enviar sequência')
  })

  it('shows only returned guess clues and formats the elapsed timer deterministically', () => {
    const activity: StudyActivity = { ...base, type: 'GUESS_CONCEPT', payload: { clueCount: 3, clues: ['Opera na camada de aplicação.'] } }
    const markup = renderToStaticMarkup(createElement(ActivityGame, { ...props, activity }))
    expect(markup).toContain('Opera na camada de aplicação.')
    expect(markup).toContain('Revelar próxima pista')
    expect(markup).not.toContain('Resposta correta')

    vi.useFakeTimers()
    const started = '2026-09-27T12:00:00.000Z'
    vi.setSystemTime(new Date('2026-09-27T12:00:17.000Z'))
    expect(formatElapsed(started, Date.now())).toBe('00:17')
    vi.advanceTimersByTime(1_000)
    expect(formatElapsed(started, Date.now())).toBe('00:18')
  })
})
