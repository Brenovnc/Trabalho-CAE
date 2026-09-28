export type StudentModule = {
  id: string
  title: string
  description: string | null
  subject: string
  activeSessionId: string | null
  activeConcepts: number
  masteredConcepts: number
  learningConcepts: number
  notStartedConcepts: number
  pendingReviews: number
  progressPercent: number
  nextReviewAtUtc: string | null
  activeSessionMode: 'NORMAL' | 'FREE_PRACTICE' | null
  concepts: { id: string; name: string }[]
}

export type ExposureSegment = { text: string | null; keywordIndex: number | null }
export type ExposureActivity = {
  presentationId: string
  type: 'EXPOSURE'
  conceptId: string
  conceptName: string
  startedAtUtc: string
  payload: {
    keywordCount: number
    revealedCount: number
    revealedKeywords: string[]
    segments: ExposureSegment[]
  }
}
export type TrueFalseActivity = {
  presentationId: string
  type: 'TRUE_FALSE'
  conceptId: string
  conceptName: string
  startedAtUtc: string
  payload: { statement: string }
}
export type FillBlankActivity = {
  presentationId: string
  type: 'FILL_BLANK'
  conceptId: string
  conceptName: string
  startedAtUtc: string
  payload: { text: string; slotNumbers: number[]; options: string[] }
}
export type GuessConceptActivity = {
  presentationId: string
  type: 'GUESS_CONCEPT'
  conceptId: string
  conceptName: string
  startedAtUtc: string
  payload: { clueCount: number; clues: string[] }
}
export type OrderingActivity = {
  presentationId: string
  type: 'ORDERING'
  conceptId: string
  conceptName: string
  startedAtUtc: string
  payload: { instruction: string; items: { id: string; text: string }[] }
}
export type StudyActivity = ExposureActivity | TrueFalseActivity | FillBlankActivity | GuessConceptActivity | OrderingActivity

export type StudySession = {
  sessionId: string
  moduleId: string
  mode: 'NORMAL' | 'FREE_PRACTICE'
  status: 'ACTIVE' | 'COMPLETED' | 'ABANDONED'
  totalActivities: number
  completedActivities: number
  startedAtUtc: string
  completedAtUtc: string | null
  activity: StudyActivity | null
}
export type StartStudySession = Omit<StudySession, 'startedAtUtc' | 'completedAtUtc'>
export type RevealResponse = { revealedCount: number; totalHints: number; text: string }
export type AnswerResponse = {
  wasCorrect: boolean
  feedback: string
  learningState: string
  completedActivities: number
  totalActivities: number
  sessionStatus: 'ACTIVE' | 'COMPLETED' | 'ABANDONED'
  nextActivity: StudyActivity | null
  correctAnswer: unknown | null
}
export type ActivityAnswer = { answer: unknown; attemptsUsed?: number; hintsUsed?: number }
