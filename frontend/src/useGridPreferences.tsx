import { useState, type MouseEvent, type ReactNode } from 'react'

export type GridColumn = { key: string; title: string }

export function useGridPreferences(columns: GridColumn[]) {
 const [hidden, setHidden] = useState<string[]>([])
 const [groupBy, setGroupBy] = useState<string | null>(null)
 const [menu, setMenu] = useState<{ x: number; y: number; key: string | null } | null>(null)
 const visibleColumns = columns.filter(column => !hidden.includes(column.key))
 function onContextMenu(event: MouseEvent) {
  event.preventDefault()
  const target = event.target as HTMLElement
  const cell = target.closest<HTMLElement>('[data-grid-column]')
  setMenu({ x: Math.min(event.clientX, window.innerWidth - 230), y: Math.min(event.clientY, window.innerHeight - 280), key: cell?.dataset.gridColumn ?? null })
 }
 const contextMenu: ReactNode = menu && <div className="data-grid-context" role="menu" onMouseLeave={() => setMenu(null)}>
  {menu.key && <>
   <button type="button" role="menuitem" onClick={() => { setGroupBy(menu.key); setMenu(null) }}>Nhóm theo “{columns.find(column => column.key === menu.key)?.title}”</button>
   {groupBy && <button type="button" role="menuitem" onClick={() => { setGroupBy(null); setMenu(null) }}>Bỏ nhóm</button>}
   <button type="button" role="menuitem" disabled={visibleColumns.length <= 1} onClick={() => { setHidden(old => [...old, menu.key!]); if (groupBy === menu.key) setGroupBy(null); setMenu(null) }}>Ẩn cột “{columns.find(column => column.key === menu.key)?.title}”</button>
  </>}
  <div className="data-grid-menu-title">Hiện cột</div>
  {columns.map(column => <button type="button" key={column.key} role="menuitemcheckbox" aria-checked={!hidden.includes(column.key)} onClick={() => { setHidden(old => old.includes(column.key) ? old.filter(key => key !== column.key) : [...old, column.key]); setMenu(null) }}>{hidden.includes(column.key) ? '□' : '☑'} {column.title}</button>)}
 </div>
 return { visibleColumns, groupBy, setGroupBy, onContextMenu, contextMenu }
}
