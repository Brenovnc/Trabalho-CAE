import { useEffect, useMemo, useRef, useState, type CSSProperties, type KeyboardEvent } from 'react'
import { generateCrossword, normalizeCrosswordWord } from './utils'
import type { CrosswordCell, CrosswordDirection, CrosswordEntry, CrosswordPlacement } from './types'
import styles from './CrosswordGame.module.css'

type Props = { entries: CrosswordEntry[]; onNew: () => void; onBack: () => void }
const cellKey = (row: number, col: number) => row + ',' + col
const containsCell = (word: CrosswordPlacement, cell: CrosswordCell) => word.direction === 'across'
  ? word.row === cell.row && cell.col >= word.col && cell.col < word.col + word.normalizedWord.length
  : word.col === cell.col && cell.row >= word.row && cell.row < word.row + word.normalizedWord.length

export function CrosswordGame({ entries, onNew, onBack }: Props) {
  const [grid, setGrid] = useState(() => generateCrossword(entries))
  const [letters, setLetters] = useState<Record<string, string>>({})
  const [activeWordId, setActiveWordId] = useState('')
  const [focusedKey, setFocusedKey] = useState('')
  const [tooltipWordId, setTooltipWordId] = useState('')
  const [solvedIds, setSolvedIds] = useState<Set<string>>(() => new Set())
  const [wrongWordId, setWrongWordId] = useState('')
  const [message, setMessage] = useState('')
  const inputs = useRef(new Map<string, HTMLInputElement>())
  const clueItems = useRef(new Map<string, HTMLLIElement>())
  const wrongTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const words = useMemo(() => [...grid.words].sort((a, b) => a.number - b.number || a.direction.localeCompare(b.direction)), [grid])
  const wordById = useMemo(() => new Map(grid.words.map(word => [word.id, word])), [grid])
  const activeWord = wordById.get(activeWordId)
  const displayedWord = activeWord ?? words.find(word => !solvedIds.has(word.id)) ?? words[0]

  useEffect(() => () => { if (wrongTimer.current) clearTimeout(wrongTimer.current) }, [])
  useEffect(() => {
    if (!activeWordId && displayedWord) selectWordById(displayedWord.id, false)
  // The initial selection follows the generated grid and stays stable as letters change.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeWordId, displayedWord?.id])
  useEffect(() => {
    if (activeWordId) {
      clueItems.current.get(activeWordId)?.scrollIntoView?.({ block: 'nearest' })
      setMessage('Palavra ' + (activeWord?.number ?? '') + ', ' + (activeWord?.direction === 'across' ? 'horizontal' : 'vertical') + ': ' + (activeWord?.clue ?? ''))
    }
  }, [activeWordId, activeWord?.number, activeWord?.direction, activeWord?.clue])

  function resetGrid() {
    setGrid(generateCrossword(entries)); setLetters({}); setActiveWordId(''); setFocusedKey('')
    setTooltipWordId(''); setSolvedIds(new Set()); setMessage(''); onNew()
  }
  function wordCells(word: CrosswordPlacement) {
    return Array.from({ length: word.normalizedWord.length }, (_, index) => ({
      row: word.row + (word.direction === 'down' ? index : 0),
      col: word.col + (word.direction === 'across' ? index : 0),
    }))
  }
  function selectWordById(id: string, focus = true) {
    const word = wordById.get(id)
    if (!word) return
    setActiveWordId(id)
    const coords = wordCells(word)
    const firstEmpty = coords.find(coord => !letters[cellKey(coord.row, coord.col)] && !inputs.current.get(cellKey(coord.row, coord.col))?.disabled)
    const target = firstEmpty ?? coords[0]
    setFocusedKey(cellKey(target.row, target.col))
    if (focus) inputs.current.get(cellKey(target.row, target.col))?.focus()
  }
  function selectAtCell(cell: CrosswordCell, toggle = false) {
    const across = cell.acrossWordIds.find(id => !solvedIds.has(id))
    const down = cell.downWordIds.find(id => !solvedIds.has(id))
    let chosen = activeWordId === across || activeWordId === down ? activeWordId : across ?? down
    if (toggle && across && down) chosen = activeWordId === across ? down : across
    if (chosen) selectWordById(chosen)
    else {
      const solved = cell.acrossWordIds.concat(cell.downWordIds).find(id => wordById.has(id))
      if (solved) selectWordById(solved)
    }
  }
  function focusCell(row: number, col: number, direction?: CrosswordDirection) {
    const cell = grid.cells[row]?.[col]
    if (!cell) return
    const id = direction === 'across' ? cell.acrossWordIds.find(word => !solvedIds.has(word))
      : direction === 'down' ? cell.downWordIds.find(word => !solvedIds.has(word)) : undefined
    if (id) selectWordById(id, false)
    setFocusedKey(cellKey(row, col))
    const input = inputs.current.get(cellKey(row, col))
    if (input && !input.disabled) input.focus()
    else selectAtCell(cell)
  }
  function focusNextEditable(word: CrosswordPlacement, fromIndex: number, step: number) {
    const coords = wordCells(word)
    for (let index = fromIndex + step; index >= 0 && index < coords.length; index += step) {
      const coord = coords[index]; const input = inputs.current.get(cellKey(coord.row, coord.col))
      if (input && !input.disabled) { input.focus(); setFocusedKey(cellKey(coord.row, coord.col)); return }
    }
  }
  function changeLetter(cell: CrosswordCell, value: string) {
    const letter = normalizeCrosswordWord(value).slice(-1); const id = cellKey(cell.row, cell.col)
    setLetters(current => ({ ...current, [id]: letter }))
    if (activeWord && letter) {
      const index = wordCells(activeWord).findIndex(coord => coord.row === cell.row && coord.col === cell.col)
      focusNextEditable(activeWord, index, 1)
    }
  }
  function checkWord() {
    if (!activeWord) return
    const coords = wordCells(activeWord)
    if (coords.some(coord => !(letters[cellKey(coord.row, coord.col)] ?? ''))) return
    const value = coords.map(coord => letters[cellKey(coord.row, coord.col)] ?? '').join('')
    if (value === activeWord.normalizedWord) {
      setSolvedIds(current => new Set(current).add(activeWord.id)); setWrongWordId(''); setMessage('Palavra correta')
    } else {
      setWrongWordId(activeWord.id); setMessage('Palavra incorreta')
      if (wrongTimer.current) clearTimeout(wrongTimer.current)
      wrongTimer.current = setTimeout(() => setWrongWordId(''), 1500)
    }
  }
  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>, cell: CrosswordCell) {
    if (event.key === 'Enter') { event.preventDefault(); checkWord(); return }
    if (event.key === 'Backspace' && activeWord) {
      const coords = wordCells(activeWord); const index = coords.findIndex(coord => coord.row === cell.row && coord.col === cell.col)
      if (!letters[cellKey(cell.row, cell.col)]) { event.preventDefault(); focusNextEditable(activeWord, index, -1) }
      return
    }
    const directions: Record<string, [number, number, CrosswordDirection]> = { ArrowUp: [-1, 0, 'down'], ArrowDown: [1, 0, 'down'], ArrowLeft: [0, -1, 'across'], ArrowRight: [0, 1, 'across'] }
    const move = directions[event.key]
    if (move) { event.preventDefault(); focusCell(cell.row + move[0], cell.col + move[1], move[2]) }
  }
  function navigateWord(step: number) {
    if (!words.length) return
    const index = Math.max(0, words.findIndex(word => word.id === (activeWord?.id ?? displayedWord?.id)))
    selectWordById(words[(index + step + words.length) % words.length].id)
  }
  function renderClueList(direction: CrosswordDirection, title: string) {
    const list = words.filter(word => word.direction === direction)
    return <section className={styles.clueSection} aria-label={title}><h3>{title}</h3><ul className={styles.clueList}>{list.map(word => <li key={word.id} ref={node => { if (node) clueItems.current.set(word.id, node); else clueItems.current.delete(word.id) }}>
      <button type="button" className={styles.clueButton} aria-current={activeWordId === word.id ? 'true' : undefined} data-solved={solvedIds.has(word.id)} onClick={() => selectWordById(word.id)}>
        <span className={styles.clueNumber}>{word.number}</span><span className={styles.clueText}>{word.clue}</span>{solvedIds.has(word.id) && <span aria-label="resolvida">✓</span>}
      </button>
    </li>)}</ul></section>
  }
  const tooSmall = grid.words.length < 2
  return <section className={styles.game} aria-label="Cruzadinha">
    {tooSmall ? <div className={styles.empty}><p>Ainda não há palavras suficientes para montar a cruzadinha.</p><button type="button" onClick={onBack}>Voltar ao módulo</button></div> : <>
      <p className={styles.instructions}>Foque uma célula para selecionar a palavra. Em cruzamentos, clique novamente para alternar a direção. Pressione Enter para conferir.</p>
      <div className={styles.activeBar}>
        <button type="button" aria-label="Palavra anterior" onClick={() => navigateWord(-1)}>‹</button>
        <span className={styles.activeNumber}>{displayedWord?.number ?? 1}</span><span aria-label={displayedWord?.direction === 'down' ? 'vertical' : 'horizontal'}>{displayedWord?.direction === 'down' ? '↓' : '→'}</span>
        <p>{displayedWord?.clue ?? 'Selecione uma palavra'}</p>
        <button type="button" aria-label="Próxima palavra" onClick={() => navigateWord(1)}>›</button>
      </div>
      <p className={styles.srOnly} aria-live="polite">{message}</p>
      <div className={styles.puzzleLayout}>
        <div className={styles.boardScroll} role="group" aria-label="Grade da cruzadinha">
          <div className={styles.board} style={{ '--columns': grid.cols } as CSSProperties}>
            {grid.cells.flatMap((row, rowIndex) => row.map((cell, colIndex) => {
              if (!cell) return <span key={cellKey(rowIndex, colIndex)} className={styles.blank} aria-hidden="true" />
              const id = cellKey(cell.row, cell.col)
              const locked = [...cell.acrossWordIds, ...cell.downWordIds].some(wordId => solvedIds.has(wordId))
              const wrong = Boolean(wrongWordId && !locked && (cell.acrossWordIds.includes(wrongWordId) || cell.downWordIds.includes(wrongWordId)))
              const label = 'Linha ' + cell.row + ', coluna ' + cell.col + (cell.starts.length ? ', palavra ' + cell.starts.map(item => item.number).join(' e ') : '')
              return <span key={id} className={styles.cell + (wrong ? ' ' + styles.incorrectCell : '')} data-active={activeWord ? containsCell(activeWord, cell) : undefined} data-focused={focusedKey === id || undefined} onClick={() => selectAtCell(cell, focusedKey === id)}>
                {cell.starts.map(start => {
                  const clueWord = wordById.get(start.id); if (!clueWord) return null
                  const open = tooltipWordId === start.id
                  return <span key={start.id} className={styles.numberWrap}>
                    <button type="button" className={styles.number} aria-label={'Palavra ' + start.number + ', ' + (start.direction === 'across' ? 'horizontal' : 'vertical') + ': ' + clueWord.clue} aria-describedby={open ? 'crossword-clue-' + start.id : undefined}
                      onFocus={() => setTooltipWordId(start.id)} onBlur={() => setTooltipWordId('')} onMouseEnter={() => setTooltipWordId(start.id)} onMouseLeave={() => setTooltipWordId('')} onClick={event => { event.stopPropagation(); selectWordById(start.id) }}>{start.number}</button>
                    {open && <span id={'crossword-clue-' + start.id} role="tooltip" className={styles.tooltip}>{clueWord.clue}</span>}
                  </span>
                })}
                {cell.starts.some(item => solvedIds.has(item.id)) && <span className={styles.correctMark} aria-label="palavra correta">✓</span>}
                <input ref={input => { if (input) inputs.current.set(id, input); else inputs.current.delete(id) }} type="text" inputMode="text" autoComplete="off" maxLength={1} value={letters[id] ?? ''} disabled={locked} aria-label={label}
                  onChange={event => changeLetter(cell, event.target.value)} onKeyDown={event => handleKeyDown(event, cell)} onFocus={() => { setFocusedKey(id); if (activeWord && !containsCell(activeWord, cell)) selectAtCell(cell) }} />
              </span>
            }))}
          </div>
        </div>
        <aside className={styles.clues} aria-label="Pistas da cruzadinha">{renderClueList('across', 'Horizontais')}{renderClueList('down', 'Verticais')}</aside>
      </div>
      {solvedIds.size === grid.words.length && <div className={styles.complete} role="status"><p>Cruzadinha concluída!</p><button type="button" onClick={resetGrid}>Nova cruzadinha</button><button type="button" onClick={onBack}>Voltar ao módulo</button></div>}
    </>}
  </section>
}
