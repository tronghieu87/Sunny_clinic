import type { GridColumn } from './useGridPreferences'

export function GridFilterRow({ columns, filters, onChange }: { columns: GridColumn[]; filters: Record<string, string>; onChange: (key: string, value: string) => void }) {
 return <tr className="data-grid-filter-row">{columns.map(column => <th key={column.key} data-grid-column={column.key}><input aria-label={'Lọc cột ' + column.title} placeholder="Lọc…" value={filters[column.key] ?? ''} onChange={event => onChange(column.key, event.target.value)} /></th>)}</tr>
}

export function GridFilterSummary({ filters, columns, onRemove, onClear }: { filters: Record<string, string>; columns: GridColumn[]; onRemove: (key: string) => void; onClear: () => void }) {
 const active = Object.entries(filters).filter(([, value]) => value.trim())
 if (!active.length) return null
 return <div className="data-grid-filter-summary" aria-live="polite">{active.map(([key, value]) => <button type="button" key={key} title={`${columns.find(column => column.key === key)?.title}: ${value}`} onClick={() => onRemove(key)}>{columns.find(column => column.key === key)?.title}: {value} ×</button>)}<button type="button" className="data-grid-clear-filter" onClick={onClear}>Xóa lọc</button></div>
}

export function GridPager({ page, pageSize, total, onPage, onPageSize }: { page: number; pageSize: number; total: number; onPage: (page: number) => void; onPageSize: (size: number) => void }) {
 const pageCount = Math.max(1, Math.ceil(total / pageSize))
 return <div className="data-grid-pager"><label>Dòng/trang<select aria-label="Số dòng mỗi trang" value={pageSize} onChange={event => onPageSize(Number(event.target.value))}>{[50, 100, 200].map(size => <option key={size}>{size}</option>)}</select></label><span>Tổng: {total.toLocaleString('vi-VN')}</span><button type="button" aria-label="Trang trước" disabled={page <= 1} onClick={() => onPage(page - 1)}>‹</button><span>{page} / {pageCount}</span><button type="button" aria-label="Trang sau" disabled={page >= pageCount} onClick={() => onPage(page + 1)}>›</button></div>
}
