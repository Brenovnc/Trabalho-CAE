import { requestApi } from './authApi'
import type { ActivityAnswer, AnswerResponse, RevealResponse, StartStudySession, StudentModule, StudySession } from '../types/learning'

export const studyApi = {
  listModules: () => requestApi<StudentModule[]>('/api/student/modules'),
  getModule: (moduleId: string) => requestApi<StudentModule>(`/api/student/modules/${moduleId}`),
  startSession: (moduleId: string) => requestApi<StartStudySession>(`/api/student/modules/${moduleId}/sessions`, 'POST'),
  startFreePractice: (moduleId: string, conceptId: string | null) => requestApi<StartStudySession>(
    `/api/student/modules/${moduleId}/free-practice/sessions`, 'POST', { conceptId }),
  resetModuleProgress: (moduleId: string) => requestApi<void>(`/api/student/modules/${moduleId}/reset-progress`, 'POST'),
  getSession: (sessionId: string) => requestApi<StudySession>(`/api/student/sessions/${sessionId}`),
  reveal: (sessionId: string, presentationId: string) =>
    requestApi<RevealResponse>(`/api/student/sessions/${sessionId}/activities/${presentationId}/reveal`, 'POST'),
  submit: (sessionId: string, presentationId: string, type: string, input: ActivityAnswer) =>
    requestApi<AnswerResponse>(`/api/student/sessions/${sessionId}/answer`, 'POST', {
      presentationId,
      type,
      answer: input.answer,
      attemptsUsed: input.attemptsUsed ?? 1,
      hintsUsed: input.hintsUsed ?? 0,
    }),
  abandon: (sessionId: string) => requestApi<void>(`/api/student/sessions/${sessionId}/abandon`, 'POST'),
}
