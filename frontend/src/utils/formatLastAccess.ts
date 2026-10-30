export function formatLastAccess(value: string | null) {
  if (!value) return 'Nunca'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return 'Nunca'
  const today = new Date(); const yesterday = new Date(today); yesterday.setDate(today.getDate() - 1)
  const sameDay = (left: Date, right: Date) => left.getFullYear() === right.getFullYear() && left.getMonth() === right.getMonth() && left.getDate() === right.getDate()
  if (sameDay(date,today)) return 'Hoje'
  if (sameDay(date,yesterday)) return 'Ontem'
  return new Intl.DateTimeFormat('pt-BR',{day:'2-digit',month:'2-digit',year:'numeric'}).format(date)
}
