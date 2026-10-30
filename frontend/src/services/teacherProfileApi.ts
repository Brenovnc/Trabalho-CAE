import { requestApi, requestFormApi } from './authApi'

export type TeacherProfile = { name: string; email: string; education: string | null; institution: string | null; hasPhoto: boolean; photoVersion: number | null }
export const teacherProfileApi = {
  get: () => requestApi<TeacherProfile>('/api/teacher/profile'),
  update: (input: Pick<TeacherProfile, 'name' | 'education' | 'institution'>) => requestApi<TeacherProfile>('/api/teacher/profile', 'PUT', input),
  password: (input: { currentPassword: string; newPassword: string; confirmNewPassword: string }) => requestApi<void>('/api/teacher/profile/password', 'PUT', input),
  photo: (form: FormData) => requestFormApi<void>('/api/teacher/profile/photo', form, 'PUT'),
  deletePhoto: () => requestApi<void>('/api/teacher/profile/photo', 'DELETE'),
}
