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
export type DistributionStats = { mastered: number; learning: number; notStarted: number }
export type DailyStats = { date: string; activities: number; correctRate: number | null }
export type TypeStats = { type: string; attempts: number; correctAttempts: number; correctRate: number | null }
export type ErrorStats = { conceptId: string; name: string; attempts: number; errorRate: number }
export type ClassroomCompare = { classroomId: string; name: string; activeStudents: number; averageProgress: number }
export type ProgressBand = { range: string; students: number }
export type TeacherDashboardStats = { distribution: DistributionStats; daily: DailyStats[]; classrooms: ClassroomCompare[] }
export type ClassroomStats = TeacherDashboardStats & { activityTypes: TypeStats[]; mostErrors: ErrorStats[]; progressBands: ProgressBand[] }
export type StudentStats = Omit<ClassroomStats, 'progressBands' | 'classrooms'>
export type StatisticsFilters = { days?: number; from?: string; to?: string; classroomId?: string; moduleId?: string; activityType?: string; minAttempts?: number; custom?: boolean }
function statisticsQuery(filters: StatisticsFilters) {
  const query = new URLSearchParams()
  if (filters.days !== undefined && !filters.custom) query.set('days', String(filters.days))
  if (filters.from) query.set('from', filters.from)
  if (filters.to) query.set('to', filters.to)
  if (filters.classroomId) query.set('classroomId', filters.classroomId)
  if (filters.moduleId) query.set('moduleId', filters.moduleId)
  if (filters.activityType) query.set('activityType', filters.activityType)
  if (filters.minAttempts !== undefined) query.set('minAttempts', String(filters.minAttempts))
  return query.size ? '?' + query.toString() : ''
}

export const teacherProgressApi = {
  dashboard: () => requestApi<TeacherDashboard>('/api/teacher/dashboard'),
  classroom: (classroomId: string) => requestApi<ClassroomProgress>('/api/classrooms/' + classroomId + '/progress'),
  student: (classroomId: string, studentId: string) => requestApi<StudentProgress>('/api/classrooms/' + classroomId + '/students/' + studentId + '/progress'),
  dashboardStatistics: (filters: StatisticsFilters) => requestApi<TeacherDashboardStats>('/api/teacher/dashboard/statistics' + statisticsQuery(filters)),
  classroomStatistics: (classroomId: string, filters: StatisticsFilters) => requestApi<ClassroomStats>('/api/classrooms/' + classroomId + '/statistics' + statisticsQuery(filters)),
  studentStatistics: (classroomId: string, studentId: string, filters: StatisticsFilters) => requestApi<StudentStats>('/api/classrooms/' + classroomId + '/students/' + studentId + '/statistics' + statisticsQuery(filters)),
}
