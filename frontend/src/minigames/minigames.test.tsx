import { describe, expect, it } from 'vitest'
import { assignOption, fillBlankAnswer } from './fill-blank/utils'
import { moveOrderingItem, orderingAnswer } from './ordering/utils'
import { guessHintCount } from './guess-concept/GuessConceptGame'

describe('learning minigame helpers', () => {
  it('maps a selected word into a slot and allows replacing its previous assignment', () => {
    const assigned = assignOption({ 1: 0, 2: 1 }, 1, 2)
    expect(assigned).toEqual({ 1: 2, 2: 1 })
    expect(fillBlankAnswer([2, 1], ['DNS', 'IP', 'domain'], assigned)).toEqual({ answers: [
      { slotNumber: 1, text: 'domain' }, { slotNumber: 2, text: 'IP' },
    ] })
  })

  it('reorders using bounded accessible move operations and submits item IDs only', () => {
    const items = [{ id: 'a' }, { id: 'b' }, { id: 'c' }]
    const moved = moveOrderingItem(items, 2, -1)
    expect(moved.map(item => item.id)).toEqual(['a', 'c', 'b'])
    expect(orderingAnswer(moved)).toEqual({ itemIds: ['a', 'c', 'b'] })
    expect(moveOrderingItem(items, 0, -1)).toBe(items)
  })

  it('counts only additional clues as hints', () => {
    expect(guessHintCount(1)).toBe(0)
    expect(guessHintCount(3)).toBe(2)
  })
})

