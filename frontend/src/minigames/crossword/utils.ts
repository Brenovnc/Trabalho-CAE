import type { CrosswordCell, CrosswordDirection, CrosswordEntry, CrosswordGrid, CrosswordPlacement } from './types'

type Occupied = { letter: string; across: Set<string>; down: Set<string> }
type NormalizedEntry = CrosswordEntry & { normalizedWord: string }
type Candidate = NormalizedEntry & { direction: CrosswordDirection; row: number; col: number }
const key = (row: number, col: number) => row + ',' + col

export function normalizeCrosswordWord(value: string) {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toUpperCase().replace(/[^A-Z]/g, '')
}

export function generateCrossword(entries: CrosswordEntry[], random: () => number = Math.random): CrosswordGrid {
  const normalized: NormalizedEntry[] = entries.map(entry => ({ ...entry, normalizedWord: normalizeCrosswordWord(entry.word) }))
    .filter(entry => entry.normalizedWord.length > 0)
  let best: Candidate[] = []
  for (let attempt = 0; attempt < 30; attempt++) {
    const groups = new Map<number, typeof normalized>()
    for (const entry of normalized) {
      const group = groups.get(entry.normalizedWord.length) ?? []
      group.push(entry)
      groups.set(entry.normalizedWord.length, group)
    }
    const order = [...groups.keys()].sort((a, b) => b - a)
      .flatMap(length => shuffle(groups.get(length)!, random))
    const placed: Candidate[] = []
    for (const entry of order) {
      const direction: CrosswordDirection = placed.length === 0 || placed.length % 2 === 0 ? 'across' : 'down'
      if (placed.length === 0) {
        placed.push({ ...entry, direction, row: 0, col: -Math.floor(entry.normalizedWord.length / 2) })
        continue
      }
      const options = findPlacements(entry, direction, placed)
      if (options.length === 0) continue
      const maxCrossings = Math.max(...options.map(option => option.crossings))
      const tied = options.filter(option => option.crossings === maxCrossings)
      placed.push(tied[Math.floor(random() * tied.length)].candidate)
    }
    if (placed.length > best.length) best = placed
  }
  return toGrid(best)
}

function shuffle<T>(items: T[], random: () => number): T[] {
  const result = [...items]
  for (let i = result.length - 1; i > 0; i--) {
    const j = Math.floor(random() * (i + 1))
    ;[result[i], result[j]] = [result[j], result[i]]
  }
  return result
}

function findPlacements(entry: NormalizedEntry, direction: CrosswordDirection, placed: Candidate[]) {
  const occupied = buildOccupied(placed)
  const options: { candidate: Candidate; crossings: number }[] = []
  const seen = new Set<string>()
  for (const existing of placed) {
    if (existing.direction === direction) continue
    for (let i = 0; i < entry.normalizedWord.length; i++) {
      for (let j = 0; j < existing.normalizedWord.length; j++) {
        if (entry.normalizedWord[i] !== existing.normalizedWord[j]) continue
        const crossingRow = existing.row + (existing.direction === 'down' ? j : 0)
        const crossingCol = existing.col + (existing.direction === 'across' ? j : 0)
        const row = crossingRow - (direction === 'down' ? i : 0)
        const col = crossingCol - (direction === 'across' ? i : 0)
        const position = key(row, col)
        if (seen.has(position)) continue
        seen.add(position)
        const candidate = { ...entry, direction, row, col }
        if (fits(candidate, occupied)) options.push({ candidate, crossings: countCrossings(candidate, occupied) })
      }
    }
  }
  return options
}

function buildOccupied(words: Candidate[]) {
  const occupied = new Map<string, Occupied>()
  for (const word of words) {
    for (let i = 0; i < word.normalizedWord.length; i++) {
      const row = word.row + (word.direction === 'down' ? i : 0)
      const col = word.col + (word.direction === 'across' ? i : 0)
      const position = key(row, col)
      const cell = occupied.get(position) ?? { letter: word.normalizedWord[i], across: new Set<string>(), down: new Set<string>() }
      if (word.direction === 'across') cell.across.add(word.id)
      else cell.down.add(word.id)
      occupied.set(position, cell)
    }
  }
  return occupied
}

function fits(candidate: Candidate, occupied: Map<string, Occupied>) {
  const horizontal = candidate.direction === 'across'
  const length = candidate.normalizedWord.length
  const before = key(candidate.row - (horizontal ? 0 : 1), candidate.col - (horizontal ? 1 : 0))
  const after = key(candidate.row + (horizontal ? 0 : length), candidate.col + (horizontal ? length : 0))
  if (occupied.has(before) || occupied.has(after)) return false

  for (let i = 0; i < length; i++) {
    const row = candidate.row + (horizontal ? 0 : i)
    const col = candidate.col + (horizontal ? i : 0)
    const cell = occupied.get(key(row, col))
    if (cell && (cell.letter !== candidate.normalizedWord[i] || (horizontal ? cell.across.size > 0 : cell.down.size > 0))) return false
    const crossingIds = horizontal ? cell?.down : cell?.across
    for (const [sideRow, sideCol] of horizontal ? [[row - 1, col], [row + 1, col]] : [[row, col - 1], [row, col + 1]]) {
      const side = occupied.get(key(sideRow, sideCol))
      if (!side) continue
      if (!crossingIds?.size) return false
      const permitted = horizontal ? side.down : side.across
      const conflicting = horizontal ? side.across : side.down
      if (conflicting.size > 0 || [...permitted].some(id => !crossingIds.has(id))) return false
    }
  }
  return true
}

function countCrossings(candidate: Candidate, occupied: Map<string, Occupied>) {
  let count = 0
  for (let i = 0; i < candidate.normalizedWord.length; i++) {
    const row = candidate.row + (candidate.direction === 'down' ? i : 0)
    const col = candidate.col + (candidate.direction === 'across' ? i : 0)
    const cell = occupied.get(key(row, col))
    if (cell && (candidate.direction === 'across' ? cell.down.size : cell.across.size)) count++
  }
  return count
}

function toGrid(words: Candidate[]): CrosswordGrid {
  if (words.length === 0) return { rows: 0, cols: 0, cells: [], words: [] }
  const minRow = Math.min(...words.map(word => word.row))
  const minCol = Math.min(...words.map(word => word.col))
  const maxRow = Math.max(...words.map(word => word.row + (word.direction === 'down' ? word.normalizedWord.length - 1 : 0)))
  const maxCol = Math.max(...words.map(word => word.col + (word.direction === 'across' ? word.normalizedWord.length - 1 : 0)))
  const rowOffset = 1 - minRow
  const colOffset = 1 - minCol
  const rows = maxRow - minRow + 3
  const cols = maxCol - minCol + 3
  const placements: CrosswordPlacement[] = words.map((word, index) => ({ ...word, number: index + 1, row: word.row + rowOffset, col: word.col + colOffset }))
  const occupied = buildOccupied(placements)
  const cells: (CrosswordCell | null)[][] = Array.from({ length: rows }, () => Array.from({ length: cols }, () => null))
  for (const placement of placements) {
    for (let i = 0; i < placement.normalizedWord.length; i++) {
      const row = placement.row + (placement.direction === 'down' ? i : 0)
      const col = placement.col + (placement.direction === 'across' ? i : 0)
      const position = occupied.get(key(row, col))!
      const cell = cells[row][col] ?? { row, col, letter: position.letter, acrossWordIds: [], downWordIds: [], starts: [] }
      if (placement.direction === 'across') cell.acrossWordIds.push(placement.id)
      else cell.downWordIds.push(placement.id)
      if (i === 0) cell.starts.push({ id: placement.id, number: placement.number, direction: placement.direction })
      cells[row][col] = cell
    }
  }
  return { rows, cols, cells, words: placements }
}
