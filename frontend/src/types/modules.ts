export type ModuleStatus = 0 | 1 | 2 | 'Draft' | 'Published' | 'Archived'

export type ModuleSummary = {
  id: string; title: string; description: string | null; subject: string; version: number
  status: ModuleStatus; createdAtUtc: string; updatedAtUtc: string; activeConceptCount: number
}

export type RecognitionActivity = { id: string; statement: string; isCorrect: boolean; explanation: string; isActive: boolean }
export type FillBlankActivity = {
  id: string; text: string; isActive: boolean
  answers: { slotNumber: number; correctText: string }[]; distractors: string[]
}
export type OrderingActivity = { id: string; instruction: string; isActive: boolean; items: string[] }
export type Concept = {
  id: string; moduleId: string; name: string; definition: string; isActive: boolean
  createdAtUtc: string; updatedAtUtc: string; keywords: string[]; clues: string[]; prerequisiteIds: string[]
  recognitionActivities: RecognitionActivity[]; fillBlankActivities: FillBlankActivity[]; orderingActivities: OrderingActivity[]
}
export type ModuleDetails = {
  id: string; title: string; description: string | null; subject: string; version: number
  status: ModuleStatus; createdAtUtc: string; updatedAtUtc: string; concepts: Concept[]
}
export type PublicationIssue = { conceptId: string | null; code: string; message: string }
export type PublicationValidation = { isValid: boolean; errors: PublicationIssue[] }
export type ModuleInput = { title: string; description: string | null; subject: string; version: number }
export type ConceptInput = {
  name: string; definition: string; keywords: string[]; clues: string[]; prerequisiteIds: string[]
  recognitionActivities: { id?: string; statement: string; isCorrect: boolean; explanation: string }[]
  fillBlankActivities: { id?: string; text: string; answers: { slotNumber: number; correctText: string }[]; distractors: string[] }[]
  orderingActivities: { id?: string; instruction: string; items: string[] }[]
}

export function moduleStatusLabel(status: ModuleStatus): string {
  if (status === 0 || status === 'Draft') return 'Rascunho'
  if (status === 1 || status === 'Published') return 'Publicado'
  return 'Arquivado'
}
