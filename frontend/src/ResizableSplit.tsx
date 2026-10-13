import { Children, type KeyboardEvent, type PointerEvent, type ReactNode, useEffect, useRef, useState } from 'react'

type Props = {
 children: ReactNode
 className?: string
 label: string
 storageKey: string
 initialPercent?: number
 stackAt?: number
 verticalMinHeight?: number
}

const clamp = (value: number) => Math.max(22, Math.min(78, value))

export function ResizableSplit({ children, className = '', label, storageKey, initialPercent = 50, stackAt = 900, verticalMinHeight = 280 }: Props) {
 const panes = Children.toArray(children).filter(Boolean)
 const [percent, setPercent] = useState(() => {
  try { const stored = Number(localStorage.getItem(`split:${storageKey}`)); if (Number.isFinite(stored) && stored >= 22 && stored <= 78) return stored } catch { /* Storage may be disabled. */ }
  return clamp(initialPercent)
 })
 const [stacked, setStacked] = useState(() => typeof window !== 'undefined' && window.innerWidth <= stackAt)
 const root = useRef<HTMLDivElement>(null)
 const start = useRef<{ pointer: number; percent: number } | null>(null)
 const currentPercent = useRef(percent)

 useEffect(() => {
  const media = window.matchMedia(`(max-width: ${stackAt}px)`)
  const update = () => setStacked(media.matches)
  media.addEventListener('change', update)
  return () => media.removeEventListener('change', update)
 }, [stackAt])

 const update = (value: number, persist = false) => {
  const next = clamp(value)
  setPercent(next)
  currentPercent.current = next
  if (persist) try { localStorage.setItem(`split:${storageKey}`, String(next)) } catch { /* Resizing remains available without persistence. */ }
 }
 const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
  event.currentTarget.setPointerCapture(event.pointerId)
  start.current = { pointer: stacked ? event.clientY : event.clientX, percent: currentPercent.current }
 }
 const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
  if (!start.current || !root.current) return
  const rect = root.current.getBoundingClientRect()
  const span = stacked ? rect.height : rect.width
  const coordinate = stacked ? event.clientY : event.clientX
  update(start.current.percent + (coordinate - start.current.pointer) / span * 100)
 }
 const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
  const decrement = stacked ? event.key === 'ArrowUp' : event.key === 'ArrowLeft'
  const increment = stacked ? event.key === 'ArrowDown' : event.key === 'ArrowRight'
  if (decrement || increment) { event.preventDefault(); update(percent + (increment ? 2 : -2), true) }
  else if (event.key === 'Home') { event.preventDefault(); update(22, true) }
  else if (event.key === 'End') { event.preventDefault(); update(78, true) }
 }

 return <div ref={root} className={`resizable-split ${className}`} data-layout={stacked ? 'vertical' : 'horizontal'} style={stacked ? { gridTemplateColumns: 'minmax(0, 1fr)', gridTemplateRows: `minmax(${verticalMinHeight}px, ${percent}fr) 12px minmax(${verticalMinHeight}px, ${100-percent}fr)`, alignItems: 'stretch' } : { gridTemplateRows: 'minmax(0, 1fr)', gridTemplateColumns: `minmax(0, ${percent}fr) 12px minmax(0, ${100-percent}fr)`, alignItems: 'stretch' }}>
  <div className="resizable-split-pane">{panes[0]}</div>
  <div className="resizable-splitter" role="separator" tabIndex={0} aria-label={`Điều chỉnh tỷ lệ ${label}`} aria-orientation={stacked ? 'horizontal' : 'vertical'} aria-valuemin={22} aria-valuemax={78} aria-valuenow={Math.round(percent)} onPointerDown={onPointerDown} onPointerMove={onPointerMove} onPointerUp={() => { if (start.current) update(currentPercent.current, true); start.current = null }} onPointerCancel={() => { if (start.current) update(currentPercent.current, true); start.current = null }} onKeyDown={onKeyDown}><span /></div>
  <div className="resizable-split-pane">{panes[1]}</div>
 </div>
}
