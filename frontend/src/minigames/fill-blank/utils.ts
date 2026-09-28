export type SlotAssignment = Record<number, number>
export function assignOption(assignment: SlotAssignment, slotNumber: number, optionIndex: number): SlotAssignment {
  const next = Object.fromEntries(Object.entries(assignment).filter(([, assigned]) => assigned !== optionIndex).map(([slot, assigned]) => [Number(slot), assigned])) as SlotAssignment
  next[slotNumber] = optionIndex
  return next
}
export function fillBlankAnswer(slotNumbers: number[], options: string[], assignment: SlotAssignment) {
  return { answers: [...slotNumbers].sort((a, b) => a - b).map(slotNumber => ({ slotNumber, text: options[assignment[slotNumber]] })) }
}
