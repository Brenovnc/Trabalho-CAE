export type CrosswordEntry = { id: string; word: string; clue: string }
export type CrosswordDirection = 'across' | 'down'
export type CrosswordPlacement = CrosswordEntry & {
  number: number
  normalizedWord: string
  direction: CrosswordDirection
  row: number
  col: number
}
export type CrosswordCell = {
  row: number
  col: number
  letter: string
  acrossWordIds: string[]
  downWordIds: string[]
  starts: { id: string; number: number; direction: CrosswordDirection }[]
}
export type CrosswordGrid = {
  rows: number
  cols: number
  cells: (CrosswordCell | null)[][]
  words: CrosswordPlacement[]
}
