export type AuthenticatedUser = {
  id: string
  role: 'TEACHER' | 'STUDENT'
  name: string | null
  email: string | null
  classroomId: string | null
  enrollmentNumber: string | null
}

export type AuthMode = 'teacher-register' | 'teacher-login' | 'student-activate' | 'student-login'

export type ApiErrorBody = {
  status: number
  code: string
  message: string
  errors: Record<string, string[]>
}
