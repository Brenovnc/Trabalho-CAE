import { requestApi } from './authApi'

export type TeacherDashboard = { modules: number; activeClassrooms: number; activeStudents: number }
export type ClassroomStudentProgress = {
  studentId: string; enrollmentNumber: string; name: string | null; isActive: boolean
  masteredConcepts: number; learningConcepts: number; notStartedConcepts: number
  pendingReviews: number; progressPercent: number; lastAccessAtUtc: string | null
}
export type ClassroomProgress = {
  classroomId: string; classroomName: string; totalActiveConcepts: number; students: ClassroomStudentProgress[]
}
export type StudentModuleProgress = {
  moduleId: string; title: string; subject: string; activeConcepts: number; masteredConcepts: number
  learningConcepts: number; notStartedConcepts: number; pendingReviews: number; progressPercent: number
}
export type StudentProgress = {
  studentId: string; enrollmentNumber: string; name: string | null; isActive: boolean; modules: StudentModuleProgress[]
}

export const teacherProgressApi = {
  dashboard: () => requestApi<TeacherDashboard>('/api/teacher/dashboard'),
  classroom: (classroomId: string) => requestApi<ClassroomProgress>('/api/classrooms/' + classroomId + '/progress'),
  student: (classroomId: string, studentId: string) => requestApi<StudentProgress>('/api/classrooms/' + classroomId + '/students/' + studentId + '/progress'),
}
