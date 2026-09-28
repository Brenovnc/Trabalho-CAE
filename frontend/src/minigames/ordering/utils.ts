export function moveOrderingItem<T>(items: T[], fromIndex: number, direction: -1 | 1): T[] {
  const toIndex = fromIndex + direction
  if (fromIndex < 0 || fromIndex >= items.length || toIndex < 0 || toIndex >= items.length) return items
  const next = [...items]
  ;[next[fromIndex], next[toIndex]] = [next[toIndex], next[fromIndex]]
  return next
}
export const orderingAnswer = (items: { id: string }[]) => ({ itemIds: items.map(item => item.id) })
