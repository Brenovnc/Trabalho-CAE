import { requestApi } from './authApi'

export type ClassroomSummary = { id: string; name: string; code: string; status: string; studentCount: number; moduleCount: number; createdAtUtc: string; updatedAtUtc: string }
export type ClassroomDetails = { id: string; name: string; code: string; status: string; createdAtUtc: string; updatedAtUtc: string; modules: { id: string; title: string; subject: string; version: number; status: string; assignedAtUtc: string }[]; studentCount: number }
export type Student = { id: string; enrollmentNumber: string; name: string | null; isActive: boolean; isActivated: boolean; createdAtUtc: string }
export type OneTimeCredentials = { studentId: string; enrollmentNumber: string; name: string | null; temporaryAccessCode: string; expiresAtUtc: string }

export const classroomApi = {
  list: () => requestApi<ClassroomSummary[]>('/api/classrooms'),
  get: (id: string) => requestApi<ClassroomDetails>(`/api/classrooms/${id}`),
  create: (name: string, code: string) => requestApi<ClassroomDetails>('/api/classrooms', 'POST', { name, code }),
  update: (id: string, name: string, code: string) => requestApi<ClassroomDetails>(`/api/classrooms/${id}`, 'PUT', { name, code }),
  archive: (id: string) => requestApi<void>(`/api/classrooms/${id}/archive`, 'POST'),
  assign: (classroomId: string, moduleId: string) => requestApi<void>(`/api/classrooms/${classroomId}/modules/${moduleId}`, 'POST'),
  unassign: (classroomId: string, moduleId: string) => requestApi<void>(`/api/classrooms/${classroomId}/modules/${moduleId}`, 'DELETE'),
  students: (id: string) => requestApi<Student[]>(`/api/classrooms/${id}/students`),
  createStudent: (id: string, enrollmentNumber: string, name: string) => requestApi<OneTimeCredentials>(`/api/classrooms/${id}/students`, 'POST', { enrollmentNumber, name: name || null }),
  resetAccess: (classroomId: string, studentId: string) => requestApi<OneTimeCredentials>(`/api/classrooms/${classroomId}/students/${studentId}/reset-access`, 'POST'),
  deactivate: (classroomId: string, studentId: string) => requestApi<void>(`/api/classrooms/${classroomId}/students/${studentId}/deactivate`, 'POST'),
}
