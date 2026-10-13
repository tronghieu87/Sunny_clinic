import { Fragment, useEffect, useState } from 'react'
import { adminApi } from './adminApi'
import { GridFilterRow, GridFilterSummary, GridPager } from './GridEnhancements'
import { useGridPreferences, type GridColumn } from './useGridPreferences'
import { ResizableSplit } from './ResizableSplit'

type Catalog = { version: string; sourceFileName: string; sourceSheet: string; documentTitle: string; documentNote: string; sourceRecordCount: number; duplicateRowsConsolidated: number; codeCount: number; chapterCount: number; importedAtUtc: string }
type Chapter = { chapterNumber: string; chapterCode: string; chapterName: string; codeCount: number }
type Code = { code: string; normalizedCode: string; diseaseName: string; diseaseNameEn: string | null; categoryCode: string | null; categoryName: string | null; chapterNumber: string; chapterCode: string; chapterName: string; mainGroupCode: string | null; mainGroupName: string | null; subGroup1Code: string | null; subGroup1Name: string | null; subGroup2Code: string | null; subGroup2Name: string | null; note: string | null }
type PageResult = { page: number; pageSize: number; total: number; items: Code[] }

export default function Icd10Catalog() {
 const gridColumns:GridColumn[]=[{key:'code',title:'Mã ICD-10'},{key:'diseaseName',title:'Tên bệnh'},{key:'categoryCode',title:'Mã loại'},{key:'mainGroupName',title:'Nhóm chính'}]
 const grid=useGridPreferences(gridColumns)
 const [catalog, setCatalog] = useState<Catalog | null>(null)
 const [chapters, setChapters] = useState<Chapter[]>([])
 const [chapterCode, setChapterCode] = useState('')
 const [search, setSearch] = useState('')
 const [debouncedSearch, setDebouncedSearch] = useState('')
 const [page, setPage] = useState(1)
 const [pageSize,setPageSize]=useState(200)
 const [columnFilters,setColumnFilters]=useState<Record<string,string>>({})
 const [collapsedGroups,setCollapsedGroups]=useState<string[]>([])
 const [results, setResults] = useState<PageResult | null>(null)
 const [loadedKey, setLoadedKey] = useState('')
 const [loading, setLoading] = useState(true)
 const [error, setError] = useState('')

 useEffect(() => {
  const controller = new AbortController()
  void Promise.all([
   adminApi('master-data/icd10/catalog', 'GET', undefined, controller.signal) as Promise<Catalog>,
   adminApi('master-data/icd10/chapters', 'GET', undefined, controller.signal) as Promise<Chapter[]>
  ]).then(([info, rows]) => { if (!controller.signal.aborted) { setCatalog(info); setChapters(rows) } })
   .catch(e => { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Không tải được danh mục ICD-10.') })
   .finally(() => { if (!controller.signal.aborted) setLoading(false) })
  return () => controller.abort()
 }, [])

 useEffect(() => { const timer = window.setTimeout(() => { setDebouncedSearch(search.trim()); setPage(1) }, 250); return () => window.clearTimeout(timer) }, [search])
 const filterKey=JSON.stringify(columnFilters)
 const requestKey = `${chapterCode}|${debouncedSearch}|${filterKey}|${page}|${pageSize}`
 const loadingResults = loadedKey !== requestKey
 useEffect(() => {
  const controller = new AbortController()
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (chapterCode) query.set('chapterCode', chapterCode)
  if (debouncedSearch) query.set('search', debouncedSearch)
  Object.entries(columnFilters).forEach(([key,value])=>{if(value.trim())query.set(key,value.trim())})
  void (adminApi(`master-data/icd10/codes?${query}`, 'GET', undefined, controller.signal) as Promise<PageResult>)
   .then(result => { if (!controller.signal.aborted) { setResults(result); setLoadedKey(requestKey) } })
   .catch(e => { if (!controller.signal.aborted) { setError(e instanceof Error ? e.message : 'Không tải được mã bệnh.'); setResults(null); setLoadedKey(requestKey) } })
  return () => controller.abort()
 }, [requestKey, chapterCode, debouncedSearch, page, pageSize, filterKey, columnFilters])

 const title = catalog?.documentTitle?.split('\n')[0] ?? 'Danh mục mã bệnh theo ICD-10'
 const allFilters={...columnFilters,...(debouncedSearch?{search:debouncedSearch}:{}),...(chapterCode?{chapterCode}: {})}
 const summaryColumns=[...gridColumns,{key:'search',title:'Từ khóa'},{key:'chapterCode',title:'Chương'}]
 const grouped=grid.groupBy?results?.items.reduce((map,row)=>{const value=String(row[grid.groupBy as keyof Code]??'—');map.set(value,[...(map.get(value)??[]),row]);return map},new Map<string,Code[]>()):null
 function cell(row:Code,key:string){switch(key){case'code':return <><strong>{row.code}</strong>{row.normalizedCode!==row.code&&<small>Mã không dấu: {row.normalizedCode}</small>}</>;case'diseaseName':return <><strong>{row.diseaseName}</strong>{row.diseaseNameEn&&<small lang="en">{row.diseaseNameEn}</small>}</>;case'categoryCode':return <>{row.categoryCode}<small>{row.categoryName}</small></>;default:return <>{row.mainGroupCode}<small>{row.mainGroupName}</small></>}}
 function dataRow(row:Code){return <tr key={row.code}>{grid.visibleColumns.map(column=><td key={column.key} data-grid-column={column.key}>{cell(row,column.key)}</td>)}</tr>}
 return <section className="icd10-page" aria-labelledby="icd10-title">
  <header className="icd10-heading"><div><p className="work-eyebrow">DỮ LIỆU Y TẾ</p><h1 id="icd10-title">ICD-10</h1><p>{title}</p></div>
   {catalog && <div className="icd10-source"><span>{catalog.chapterCount} chương</span><span>{catalog.codeCount.toLocaleString('vi-VN')} mã bệnh</span><small>Nguồn: {catalog.sourceFileName}</small></div>}
  </header>
  {error && <p className="icd10-alert" role="alert">{error}</p>}
  <ResizableSplit className="icd10-panels" label="chương và danh sách mã ICD-10" storageKey="icd10" stackAt={650} initialPercent={30}>
   <section className="icd10-panel icd10-chapter-panel" aria-labelledby="icd10-chapters-title">
    <header className="icd10-panel-head"><h2 id="icd10-chapters-title">Chương ICD-10</h2><span className="icd10-count">{chapters.length}</span></header>
    <div className="icd10-chapter-list">
     <button type="button" className={!chapterCode ? 'is-selected' : ''} aria-pressed={!chapterCode} onClick={() => { setChapterCode(''); setPage(1) }}><strong>Tất cả chương</strong><span>{catalog?.codeCount.toLocaleString('vi-VN') ?? '—'}</span></button>
     {chapters.map(row => <button type="button" key={row.chapterCode} className={chapterCode === row.chapterCode ? 'is-selected' : ''} aria-pressed={chapterCode === row.chapterCode} onClick={() => { setChapterCode(row.chapterCode); setPage(1) }}><span className="icd10-chapter-number">{row.chapterNumber}</span><span className="icd10-chapter-name"><strong>{row.chapterName}</strong><small>{row.chapterCode}</small></span><span className="icd10-chapter-count">{row.codeCount}</span></button>)}
     {!loading && chapters.length === 0 && <p className="icd10-empty">Không có chương ICD-10.</p>}
    </div>
    <footer className="icd10-panel-foot">Chọn chương để lọc mã bệnh</footer>
   </section>
   <section className="icd10-panel icd10-code-panel" aria-labelledby="icd10-codes-title">
    <header className="icd10-panel-head"><div><h2 id="icd10-codes-title">Danh sách mã bệnh</h2><small>{chapterCode ? chapters.find(x => x.chapterCode === chapterCode)?.chapterName : 'Tra cứu toàn bộ danh mục'}</small></div><span className="icd10-count">{(results?.total ?? 0).toLocaleString('vi-VN')}</span></header>
    <div className="icd10-toolbar"><label htmlFor="icd10-search">Tìm mã hoặc tên bệnh<input id="icd10-search" value={search} maxLength={100} onChange={e => setSearch(e.target.value)} placeholder="Ví dụ: J13, viêm phổi…" /></label></div>
    <div className="icd10-table-wrap" onContextMenu={grid.onContextMenu}><table className="icd10-table"><thead><tr>{grid.visibleColumns.map(column=><th key={column.key} data-grid-column={column.key}>{column.title}</th>)}</tr><GridFilterRow columns={grid.visibleColumns} filters={columnFilters} onChange={(key,value)=>{setColumnFilters(old=>({...old,[key]:value}));setPage(1)}}/></thead><tbody>
     {grouped?Array.from(grouped,([value,items])=><Fragment key={value}><tr className="data-grid-group-row"><td colSpan={grid.visibleColumns.length}><button type="button" aria-expanded={!collapsedGroups.includes(value)} onClick={()=>setCollapsedGroups(old=>old.includes(value)?old.filter(x=>x!==value):[...old,value])}>{collapsedGroups.includes(value)?'＋':'−'} {gridColumns.find(column=>column.key===grid.groupBy)?.title}: {value} ({items.length} trong trang hiện tại)</button></td></tr>{!collapsedGroups.includes(value)&&items.map(dataRow)}</Fragment>):results?.items.map(dataRow)}
     {!loadingResults && results?.items.length === 0 && <tr><td colSpan={grid.visibleColumns.length} className="icd10-empty">Không tìm thấy mã bệnh phù hợp.</td></tr>}
    </tbody></table>{loadingResults && <p className="icd10-state" role="status">Đang tải mã bệnh…</p>}{loading && !catalog && <p className="icd10-state" role="status">Đang tải dữ liệu…</p>}</div>
    <footer className="icd10-panel-foot data-grid-footer"><GridFilterSummary filters={allFilters} columns={summaryColumns} onRemove={key=>{if(key==='search')setSearch('');else if(key==='chapterCode'){setChapterCode('');setPage(1)}else setColumnFilters(old=>({...old,[key]:''}))}} onClear={()=>{setColumnFilters({});setSearch('');setChapterCode('');setPage(1)}}/><GridPager page={page} pageSize={pageSize} total={results?.total??0} onPage={setPage} onPageSize={value=>{setPageSize(value);setPage(1)}}/></footer>
    {grid.contextMenu}
   </section>
  </ResizableSplit>
  <p className="icd10-note">Tên bệnh, mã phân cấp và ghi chú được giữ theo sheet ICD10 của tệp nguồn. Các sheet phụ lục khác chưa nhập vào danh mục tra cứu này.</p>
 </section>
}
