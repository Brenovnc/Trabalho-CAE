import { useEffect, useMemo, useRef, useState, type CSSProperties, type KeyboardEvent, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { generateCrossword, normalizeCrosswordWord } from './utils'
import type { CrosswordCell, CrosswordDirection, CrosswordEntry, CrosswordPlacement } from './types'
import styles from './CrosswordGame.module.css'

type Props = { entries: CrosswordEntry[]; onNew: () => void; onBack: () => void; title?: ReactNode }
const cellKey = (row: number, col: number) => row + ',' + col
const containsCell = (word: CrosswordPlacement, cell: CrosswordCell) => word.direction === 'across'
  ? word.row === cell.row && cell.col >= word.col && cell.col < word.col + word.normalizedWord.length
  : word.col === cell.col && cell.row >= word.row && cell.row < word.row + word.normalizedWord.length

export function CrosswordGame({ entries, onNew, onBack, title }: Props) {
  const [grid, setGrid] = useState(() => generateCrossword(entries))
  const [letters, setLetters] = useState<Record<string, string>>({})
  const [activeWordId, setActiveWordId] = useState('')
  const [focusedKey, setFocusedKey] = useState('')
  const [tooltipWordId, setTooltipWordId] = useState('')
  const [solvedIds, setSolvedIds] = useState<Set<string>>(() => new Set())
  const [wrongWordId, setWrongWordId] = useState('')
  const [message, setMessage] = useState('')
  const [revealedKeys, setRevealedKeys] = useState<Set<string>>(() => new Set())
  const [revealedWordIds, setRevealedWordIds] = useState<Set<string>>(() => new Set())
  const [revealOpen, setRevealOpen] = useState(false)
  const [helpOpen, setHelpOpen] = useState(false)
  const inputs = useRef(new Map<string, HTMLInputElement>())
  const activeWordIdRef = useRef('')
  const focusedKeyRef = useRef('')
  const wasFocusedOnPointerDown = useRef(false)
  const revealMenuRef = useRef<HTMLDivElement>(null)
  const helpButtonRef = useRef<HTMLButtonElement>(null)
  const closeHelpRef = useRef<HTMLButtonElement>(null)
  const clueItems = useRef(new Map<string, HTMLLIElement>())
  const wrongTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const words = useMemo(() => [...grid.words].sort((a, b) => a.number - b.number || a.direction.localeCompare(b.direction)), [grid])
  const wordById = useMemo(() => new Map(grid.words.map(word => [word.id, word])), [grid])
  const activeWord = wordById.get(activeWordId)
  const displayedWord = activeWord ?? words.find(word => !solvedIds.has(word.id)) ?? words[0]

  useEffect(() => () => { if (wrongTimer.current) clearTimeout(wrongTimer.current) }, [])
  useEffect(() => { if (helpOpen) closeHelpRef.current?.focus() }, [helpOpen])
  useEffect(() => {
    if (!revealOpen) return
    const outside = (event: PointerEvent) => { if (!revealMenuRef.current?.contains(event.target as Node)) setRevealOpen(false) }
    document.addEventListener('pointerdown', outside)
    return () => document.removeEventListener('pointerdown', outside)
  }, [revealOpen])
  useEffect(() => {
    if (helpOpen) {
      const oldOverflow = document.body.style.overflow
      document.body.style.overflow = 'hidden'
      return () => { document.body.style.overflow = oldOverflow }
    }
  }, [helpOpen])
  useEffect(() => {
    if (!helpOpen && !revealOpen) return
    const keydown = (event: globalThis.KeyboardEvent) => {
      if (event.key !== 'Escape') return
      if (helpOpen) { setHelpOpen(false); helpButtonRef.current?.focus() }
      if (revealOpen) setRevealOpen(false)
    }
    document.addEventListener('keydown', keydown)
    return () => document.removeEventListener('keydown', keydown)
  }, [helpOpen, revealOpen])
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
    setGrid(generateCrossword(entries)); setLetters({}); activeWordIdRef.current = ''; setActiveWordId(''); updateFocusedKey('')
    setTooltipWordId(''); setSolvedIds(new Set()); setMessage(''); onNew()
    setRevealedKeys(new Set()); setRevealedWordIds(new Set())
  }
  function wordCells(word: CrosswordPlacement) {
    return Array.from({ length: word.normalizedWord.length }, (_, index) => ({
      row: word.row + (word.direction === 'down' ? index : 0),
      col: word.col + (word.direction === 'across' ? index : 0),
    }))
  }
  function updateFocusedKey(key: string) { focusedKeyRef.current = key; setFocusedKey(key) }
  function selectWordById(id: string, focus = true, targetCell?: { row: number; col: number }) {
    let word = wordById.get(id)
    if (!word) return
    let target = targetCell
    if (targetCell && inputs.current.get(cellKey(targetCell.row, targetCell.col))?.disabled) {
      const cell = grid.cells[targetCell.row]?.[targetCell.col]
      const alternateId = cell && [...cell.acrossWordIds, ...cell.downWordIds].find(otherId => otherId !== word!.id && !solvedIds.has(otherId))
      const alternate = alternateId ? wordById.get(alternateId) : undefined
      if (alternate) {
        word = alternate
        const coords = wordCells(word)
        target = coords.find(coord => !letters[cellKey(coord.row, coord.col)] && !inputs.current.get(cellKey(coord.row, coord.col))?.disabled)
          ?? coords.find(coord => !inputs.current.get(cellKey(coord.row, coord.col))?.disabled)
          ?? coords[0]
      } else {
        const coords = wordCells(word)
        target = coords.find(coord => !inputs.current.get(cellKey(coord.row, coord.col))?.disabled) ?? coords[0]
      }
    }
    activeWordIdRef.current = word.id
    setActiveWordId(word.id)
    const coords = wordCells(word)
    if (!target) {
      const firstEmpty = coords.find(coord => !letters[cellKey(coord.row, coord.col)] && !inputs.current.get(cellKey(coord.row, coord.col))?.disabled)
      target = firstEmpty ?? coords[0]
    }
    updateFocusedKey(cellKey(target.row, target.col))
    if (focus) inputs.current.get(cellKey(target.row, target.col))?.focus()
  }
  function selectAtCell(cell: CrosswordCell, toggle = false) {
    const across = cell.acrossWordIds.find(id => !solvedIds.has(id))
    const down = cell.downWordIds.find(id => !solvedIds.has(id))
    const currentId = activeWordIdRef.current
    const startsHere = cell.starts.map(item => item.id)
    let chosen = currentId === across || currentId === down ? currentId : across ?? down
    if (toggle && startsHere.length > 1 && focusedKeyRef.current === cellKey(cell.row, cell.col))
      chosen = startsHere.find(id => id !== currentId && !solvedIds.has(id)) ?? currentId
    else if (toggle && across && down) chosen = currentId === across ? down : across
    if (chosen) selectWordById(chosen, true, { row: cell.row, col: cell.col })
    else {
      const solved = cell.acrossWordIds.concat(cell.downWordIds).find(id => wordById.has(id))
      if (solved) selectWordById(solved, true, { row: cell.row, col: cell.col })
    }
  }
  function focusCell(row: number, col: number, direction?: CrosswordDirection) {
    const cell = grid.cells[row]?.[col]
    if (!cell) return
    const id = direction === 'across' ? cell.acrossWordIds.find(word => !solvedIds.has(word))
      : direction === 'down' ? cell.downWordIds.find(word => !solvedIds.has(word)) : undefined
    if (id) selectWordById(id, false)
    updateFocusedKey(cellKey(row, col))
    const input = inputs.current.get(cellKey(row, col))
    if (input && !input.disabled) input.focus()
    else selectAtCell(cell)
  }
  function focusNextEditable(word: CrosswordPlacement, fromIndex: number, step: number) {
    const coords = wordCells(word)
    for (let index = fromIndex + step; index >= 0 && index < coords.length; index += step) {
      const coord = coords[index]; const input = inputs.current.get(cellKey(coord.row, coord.col))
      if (input && !input.disabled) { input.focus(); updateFocusedKey(cellKey(coord.row, coord.col)); return }
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
  function revealCell() {
    const active = wordById.get(activeWordIdRef.current) ?? displayedWord
    if (!active) return
    const coords = wordCells(active)
    const focused = focusedKey ? focusedKey.split(',').map(Number) : null
    const target = focused && grid.cells[focused[0]]?.[focused[1]]
      ? { row: focused[0], col: focused[1] }
      : coords.find(item => !letters[cellKey(item.row, item.col)])
    if (!target) return
    const cell = grid.cells[target.row]?.[target.col]
    if (!cell) return
    const word = [...cell.acrossWordIds, ...cell.downWordIds].map(id => wordById.get(id)).find(Boolean)
    if (!word) return
    const index = wordCells(word).findIndex(item => item.row === target.row && item.col === target.col)
    const key = cellKey(target.row, target.col)
    setLetters(current => ({ ...current, [key]: word.normalizedWord[index] }))
    setRevealedKeys(current => new Set(current).add(key)); updateFocusedKey(key); setMessage('Célula revelada')
  }
  function revealWord() {
    const word = wordById.get(activeWordIdRef.current) ?? displayedWord
    if (!word) return
    const coords = wordCells(word)
    setLetters(current => { const next = { ...current }; coords.forEach((coord, index) => { next[cellKey(coord.row, coord.col)] = word.normalizedWord[index] }); return next })
    setRevealedKeys(current => new Set([...current, ...coords.map(coord => cellKey(coord.row, coord.col))]))
    setRevealedWordIds(current => new Set(current).add(word.id)); setSolvedIds(current => new Set(current).add(word.id)); setMessage('Palavra revelada')
  }
  function revealCrossword() {
    if (!window.confirm('Revelar toda a cruzadinha?')) return
    const next: Record<string, string> = {}; const keys = new Set<string>()
    words.forEach(word => wordCells(word).forEach((coord, index) => { const key = cellKey(coord.row, coord.col); next[key] = word.normalizedWord[index]; keys.add(key) }))
    setLetters(next); setRevealedKeys(keys); setSolvedIds(new Set(words.map(word => word.id))); setRevealedWordIds(new Set(words.map(word => word.id))); setMessage('Cruzadinha concluída!')
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
    if (event.key === 'Backspace') {
      event.preventDefault()
      const currentKey = cellKey(cell.row, cell.col)
      const input = inputs.current.get(currentKey)
      if (input?.disabled) return
      if (letters[currentKey]) {
        setLetters(current => ({ ...current, [currentKey]: '' }))
        updateFocusedKey(currentKey)
        input?.focus()
        return
      }
      if (!activeWord) return
      const coords = wordCells(activeWord); const index = coords.findIndex(coord => coord.row === cell.row && coord.col === cell.col)
      for (let previous = index - 1; previous >= 0; previous--) {
        const coord = coords[previous]; const previousKey = cellKey(coord.row, coord.col)
        const previousInput = inputs.current.get(previousKey)
        if (!previousInput || previousInput.disabled) continue
        setLetters(current => ({ ...current, [previousKey]: '' }))
        updateFocusedKey(previousKey)
        previousInput.focus()
        break
      }
      return
    }
    if (event.key === 'Delete') {
      event.preventDefault()
      const key = cellKey(cell.row, cell.col)
      if (!inputs.current.get(key)?.disabled) setLetters(current => ({ ...current, [key]: '' }))
      return
    }
    const directions: Record<string, [number, number, CrosswordDirection]> = { ArrowUp: [-1, 0, 'down'], ArrowDown: [1, 0, 'down'], ArrowLeft: [0, -1, 'across'], ArrowRight: [0, 1, 'across'] }
    const move = directions[event.key]
    if (move) { event.preventDefault(); focusCell(cell.row + move[0], cell.col + move[1], move[2]) }
  }
  function navigateWord(step: number) {
    if (!words.length) return
    const index = Math.max(0, words.findIndex(word => word.id === (activeWordIdRef.current || displayedWord?.id)))
    selectWordById(words[(index + step + words.length) % words.length].id)
  }
  function chooseReveal(action: () => void) { setRevealOpen(false); action() }
  function closeHelp() { setHelpOpen(false); helpButtonRef.current?.focus() }
  function renderClueList(direction: CrosswordDirection, title: string) {
    const list = words.filter(word => word.direction === direction)
    return <section className={styles.clueSection} aria-label={title}><h3>{title}</h3><ul className={styles.clueList}>{list.map(word => <li key={word.id} ref={node => { if (node) clueItems.current.set(word.id, node); else clueItems.current.delete(word.id) }}>
      <button type="button" className={styles.clueButton} aria-current={activeWordId === word.id ? 'true' : undefined} data-solved={solvedIds.has(word.id)} onClick={() => selectWordById(word.id)}>
        <span className={styles.clueNumber}>{word.number}</span><span className={styles.clueText}>{word.clue}{revealedWordIds.has(word.id) && ' (revelada)'}</span>{solvedIds.has(word.id) && !revealedWordIds.has(word.id) && <span aria-label="resolvida">✓</span>}
      </button>
    </li>)}</ul></section>
  }
  const tooSmall = grid.words.length < 2
  return <section className={styles.game} aria-label="Cruzadinha">
    <header className={styles.header}><div>{title}</div><div className={styles.tools}>
      <div className={styles.revealWrap} ref={revealMenuRef}>
        <button type="button" aria-haspopup="menu" aria-expanded={revealOpen} disabled={solvedIds.size === words.length} onClick={() => setRevealOpen(value => !value)}>Revelar ▾</button>
        {revealOpen && <div className={styles.revealMenu} role="menu" onKeyDown={event => { if (event.key === 'Escape') setRevealOpen(false) }}>
          <button type="button" role="menuitem" onClick={() => chooseReveal(revealCell)}>Revelar célula</button>
          <button type="button" role="menuitem" onClick={() => chooseReveal(revealWord)}>Revelar palavra</button>
          <button type="button" role="menuitem" onClick={() => chooseReveal(revealCrossword)}>Revelar cruzadinha</button>
        </div>}
      </div>
      <button ref={helpButtonRef} type="button" className={styles.helpButton} aria-label="Como funciona a cruzadinha" onClick={() => setHelpOpen(true)}>?</button>
    </div></header>
    {tooSmall ? <div className={styles.empty}><p>Ainda não há palavras suficientes para montar a cruzadinha.</p><button type="button" onClick={onBack}>Voltar ao módulo</button></div> : <>
      <p className={styles.instructions}>Foque uma célula para selecionar a palavra. Em cruzamentos, clique novamente para alternar a direção. Pressione Enter para conferir.</p>
      <div className={styles.activeBar}>
        <button type="button" aria-label="Palavra anterior" onClick={() => navigateWord(-1)}>‹</button>
        <span className={styles.activeNumber}>{displayedWord?.number ?? 1}</span><span>{displayedWord?.direction === 'down' ? '↓' : '→'}<span className={styles.srOnly}>{displayedWord?.direction === 'down' ? 'vertical' : 'horizontal'}</span></span>
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
              const locked = revealedKeys.has(id) || [...cell.acrossWordIds, ...cell.downWordIds].some(wordId => solvedIds.has(wordId))
              const wrong = Boolean(wrongWordId && !locked && (cell.acrossWordIds.includes(wrongWordId) || cell.downWordIds.includes(wrongWordId)))
              const label = 'Linha ' + cell.row + ', coluna ' + cell.col + (cell.starts.length ? ', palavra ' + cell.starts.map(item => item.number).join(' e ') : '')
              return <span key={id} className={styles.cell + (wrong ? ' ' + styles.incorrectCell : '')} data-active={activeWord ? containsCell(activeWord, cell) : undefined} data-focused={focusedKey === id || undefined} data-revealed={revealedKeys.has(id) || undefined} onPointerDown={() => { wasFocusedOnPointerDown.current = focusedKeyRef.current === id }} onClick={() => selectAtCell(cell, wasFocusedOnPointerDown.current)}>
                <span className={styles.numberGroup}>{cell.starts.map((start, startIndex) => {
                  const clueWord = wordById.get(start.id); if (!clueWord) return null
                  const open = tooltipWordId === start.id
                  return <span key={start.id} className={styles.numberWrap}>
                    {startIndex > 0 && <span aria-hidden="true">,</span>}<button type="button" className={styles.number} data-active={activeWordId === start.id || undefined} data-multiple={cell.starts.length > 1 || undefined} aria-label={'Palavra ' + start.number + ', ' + (start.direction === 'across' ? 'horizontal' : 'vertical') + ': ' + clueWord.clue} aria-describedby={open ? 'crossword-clue-' + start.id : undefined}
                      onFocus={() => setTooltipWordId(start.id)} onBlur={() => setTooltipWordId('')} onMouseEnter={() => setTooltipWordId(start.id)} onMouseLeave={() => setTooltipWordId('')} onClick={event => { event.stopPropagation(); selectWordById(start.id, true, { row: cell.row, col: cell.col }) }}>{start.number}</button>
                    {open && <span id={'crossword-clue-' + start.id} role="tooltip" className={styles.tooltip}>{clueWord.clue}</span>}
                  </span>
                })}</span>
                {cell.starts.some(item => solvedIds.has(item.id)) && <span className={styles.correctMark} aria-label="palavra correta">✓</span>}
                <input ref={input => { if (input) inputs.current.set(id, input); else inputs.current.delete(id) }} type="text" inputMode="text" autoComplete="off" maxLength={1} value={letters[id] ?? ''} disabled={locked} aria-label={label}
                  onChange={event => changeLetter(cell, event.target.value)} onKeyDown={event => handleKeyDown(event, cell)} onFocus={() => { updateFocusedKey(id); const focusedWord = wordById.get(activeWordIdRef.current); if (!focusedWord || !containsCell(focusedWord, cell)) selectAtCell(cell, false); else selectWordById(focusedWord.id, false, { row: cell.row, col: cell.col }) }} />
              </span>
            }))}
          </div>
        </div>
        <aside className={styles.clues} aria-label="Pistas da cruzadinha">{renderClueList('across', 'Horizontais')}{renderClueList('down', 'Verticais')}</aside>
      </div>
      {solvedIds.size === grid.words.length && <div className={styles.complete} role="status"><p>Cruzadinha concluída!</p><button type="button" onClick={resetGrid}>Nova cruzadinha</button><button type="button" onClick={onBack}>Voltar ao módulo</button></div>}
    </>}
    {helpOpen && createPortal(<div className={styles.modalOverlay} onMouseDown={event => { if (event.target === event.currentTarget) closeHelp() }}><section className={styles.helpDialog} role="dialog" aria-modal="true" aria-labelledby="crossword-help-title">
      <div className={styles.helpHeader}><h2 id="crossword-help-title">Como funciona a cruzadinha</h2><button ref={closeHelpRef} type="button" onClick={closeHelp}>Fechar</button></div>
      <h3>Objetivo</h3><p>Preencha a grade com as palavras descritas nas pistas.</p>
      <h3>Escolher uma palavra</h3><p>Clique ou foque uma célula, clique no número da palavra (a definição aparece num balão), clique numa pista da lista lateral ou use as setas ‹ e › acima da grade para navegar na ordem dos números.</p>
      <h3>Cruzamentos</h3><p>Onde duas palavras se cruzam, clique de novo na mesma célula para alternar entre horizontal e vertical. Se duas palavras começam na mesma célula, os dois números aparecem juntos (ex.: 1, 10) e cada clique alterna entre elas.</p>
      <h3>Digitar</h3><p>Digite as letras; o cursor avança sozinho. Backspace volta, e as setas do teclado movem entre células. Maiúsculas, acentos, espaços e hífens não importam.</p>
      <h3>Conferir</h3><p>Com a palavra completa, pressione Enter. Se estiver certa, ela fica verde com ✓ e bloqueada; se estiver errada, fica vermelha por um instante e você pode corrigir. Com a palavra incompleta, nada acontece.</p>
      <h3>Revelar</h3><p>No botão Revelar você pode mostrar uma célula, uma palavra inteira ou a cruzadinha toda. Letras reveladas ficam em destaque âmbar.</p>
      <h3>Observação</h3><p>A cruzadinha é um jogo livre e não altera seu progresso oficial.</p>
    </section></div>, document.body)}
  </section>
}
