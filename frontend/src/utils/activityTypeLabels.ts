const labels: Record<string,string> = { TRUE_FALSE:'Verdadeiro ou falso', FILL_BLANK:'Completar lacunas', GUESS_CONCEPT:'Jogo das pistas', ORDERING:'Ordenação', EXPOSURE:'Exposição' }
export function activityTypeLabel(type: string) { return labels[type] ?? type }
