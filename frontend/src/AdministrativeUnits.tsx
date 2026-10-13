import { useEffect, useState } from 'react'
import { adminApi } from './adminApi'
import { Fragment } from 'react'
import { GridFilterRow, GridFilterSummary, GridPager } from './GridEnhancements'
import { useGridPreferences, type GridColumn } from './useGridPreferences'
import { ResizableSplit } from './ResizableSplit'

type Province = { code: string; name: string; unitType: string; legacyProvinceCode: string; communeCount: number }
type Commune = { code: string; name: string; unitType: string; legacyProvinceCode: string; legacyDistrictCode: string; legacyDistrictName: string; sourceRowNumber: number; reviewStatus: string | null }
type Catalog = { version: string; sourceFileName: string; importedAtUtc: string; provinceCount: number; communeCount: number }
type CommunePage = { province: { code: string; name: string }; page: number; pageSize: number; total: number; items: Commune[] }

export default function AdministrativeUnits() {
 const provinceColumns:GridColumn[]=[{key:'code',title:'Mã tỉnh BNV'},{key:'name',title:'Tên tỉnh / thành'},{key:'communeCount',title:'Số phường/xã'}]
 const communeColumns:GridColumn[]=[{key:'code',title:'Mã phường/xã'},{key:'name',title:'Tên'},{key:'legacyDistrictName',title:'Quận / huyện cũ theo tệp'},{key:'unitType',title:'Loại'}]
 const provinceGrid=useGridPreferences(provinceColumns);const communeGrid=useGridPreferences(communeColumns)
 const [catalog, setCatalog] = useState<Catalog | null>(null)
 const [provinces, setProvinces] = useState<Province[]>([])
 const [selectedCode, setSelectedCode] = useState('')
 const [search, setSearch] = useState('')
 const [debouncedSearch, setDebouncedSearch] = useState('')
 const [page, setPage] = useState(1)
 const [pageSize,setPageSize]=useState(200)
 const [provincePage,setProvincePage]=useState(1)
 const [provinceFilters,setProvinceFilters]=useState<Record<string,string>>({})
 const [communeFilters,setCommuneFilters]=useState<Record<string,string>>({})
 const [collapsedProvince,setCollapsedProvince]=useState<string[]>([])
 const [collapsedCommune,setCollapsedCommune]=useState<string[]>([])
 const [communes, setCommunes] = useState<CommunePage | null>(null)
 const [loading, setLoading] = useState(true)
 const [loadedListKey, setLoadedListKey] = useState('')
 const [error, setError] = useState('')

 useEffect(() => {
  const controller = new AbortController()
  void Promise.all([
   adminApi('master-data/administrative-units/catalog', 'GET', undefined, controller.signal) as Promise<Catalog>,
   adminApi('master-data/administrative-units/provinces', 'GET', undefined, controller.signal) as Promise<Province[]>
  ]).then(([info, rows]) => { if (controller.signal.aborted) return; setCatalog(info); setProvinces(rows); setSelectedCode(rows[0]?.code ?? '') })
   .catch(e => { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Không tải được danh mục địa giới hành chính.') })
   .finally(() => { if (!controller.signal.aborted) setLoading(false) })
  return () => controller.abort()
 }, [])

 useEffect(() => { const timer = window.setTimeout(() => { setDebouncedSearch(search.trim()); setPage(1) }, 250); return () => window.clearTimeout(timer) }, [search])
 const communeFilterKey=JSON.stringify(communeFilters)
 useEffect(() => {
  if (!selectedCode) return
  const controller = new AbortController()
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (debouncedSearch) query.set('search', debouncedSearch)
  Object.entries(communeFilters).forEach(([key,value])=>{if(value.trim())query.set(key,value.trim())})
  void (adminApi(`master-data/administrative-units/provinces/${encodeURIComponent(selectedCode)}/communes?${query}`, 'GET', undefined, controller.signal) as Promise<CommunePage>)
   .then(result => { if (!controller.signal.aborted) { setCommunes(result); setLoadedListKey(`${selectedCode}|${debouncedSearch}|${page}|${pageSize}|${communeFilterKey}`) } })
   .catch(e => { if (!controller.signal.aborted) { setError(e instanceof Error ? e.message : 'Không tải được danh sách phường/xã.'); setCommunes(null); setLoadedListKey(`${selectedCode}|${debouncedSearch}|${page}|${pageSize}|${communeFilterKey}`) } })
  return () => controller.abort()
 }, [selectedCode, debouncedSearch, page, pageSize, communeFilterKey, communeFilters])

 const listKey = `${selectedCode}|${debouncedSearch}|${page}|${pageSize}|${communeFilterKey}`
 const loadingList = Boolean(selectedCode) && loadedListKey !== listKey
 const filteredProvinces=provinces.filter(row=>Object.entries(provinceFilters).every(([key,value])=>!value.trim()||String(row[key as keyof Province]??'').toLocaleLowerCase('vi').includes(value.trim().toLocaleLowerCase('vi'))))
 const shownProvinces=filteredProvinces.slice((provincePage-1)*pageSize,provincePage*pageSize)
 const provinceGrouped=provinceGrid.groupBy?shownProvinces.reduce((map,row)=>{const value=String(row[provinceGrid.groupBy as keyof Province]??'—');map.set(value,[...(map.get(value)??[]),row]);return map},new Map<string,Province[]>()):null
 const communeGrouped=communeGrid.groupBy?communes?.items.reduce((map,row)=>{const value=String(row[communeGrid.groupBy as keyof Commune]??'—');map.set(value,[...(map.get(value)??[]),row]);return map},new Map<string,Commune[]>()):null
 const summaryFilters={...communeFilters,...(debouncedSearch?{search:debouncedSearch}:{})}
 const summaryColumns=[...communeColumns,{key:'search',title:'Từ khóa'}]
 return <section className="administrative-unit-page" aria-labelledby="administrative-unit-title">
  <header className="administrative-unit-heading"><div><p className="work-eyebrow">DỮ LIỆU Y TẾ</p><h1 id="administrative-unit-title">Địa giới hành chính</h1><p>Tra cứu tỉnh/thành và phường/xã trong dữ liệu được cung cấp.</p></div>
   {catalog && <div className="administrative-unit-source"><span>{catalog.provinceCount} tỉnh/thành</span><span>{catalog.communeCount.toLocaleString('vi-VN')} phường/xã</span><small>Nguồn: {catalog.sourceFileName}</small></div>}
  </header>
  {error && <p className="administrative-unit-alert" role="alert">{error}</p>}
  <ResizableSplit className="administrative-unit-panels" label="tỉnh thành và phường xã" storageKey="administrative-units" stackAt={650} initialPercent={30}>
   <section className="administrative-unit-panel administrative-province-panel" aria-labelledby="province-list-title">
    <header className="administrative-unit-panel-head"><h2 id="province-list-title">Tỉnh / thành</h2><span className="administrative-unit-count">{provinces.length}</span></header>
    <div className="administrative-unit-table-wrap" onContextMenu={provinceGrid.onContextMenu}><table className="administrative-unit-table"><thead><tr>{provinceGrid.visibleColumns.map(column=><th key={column.key} data-grid-column={column.key}>{column.title}</th>)}</tr><GridFilterRow columns={provinceGrid.visibleColumns} filters={provinceFilters} onChange={(key,value)=>{setProvinceFilters(old=>({...old,[key]:value}));setProvincePage(1)}}/></thead><tbody>
     {provinceGrouped?Array.from(provinceGrouped,([value,items])=><Fragment key={value}><tr className="data-grid-group-row"><td colSpan={provinceGrid.visibleColumns.length}><button type="button" aria-expanded={!collapsedProvince.includes(value)} onClick={()=>setCollapsedProvince(old=>old.includes(value)?old.filter(x=>x!==value):[...old,value])}>{collapsedProvince.includes(value)?'＋':'−'} {provinceColumns.find(x=>x.key===provinceGrid.groupBy)?.title}: {value} ({items.length})</button></td></tr>{!collapsedProvince.includes(value)&&items.map(row=><tr key={row.code} className={selectedCode===row.code?'is-selected':''} onClick={()=>{setSelectedCode(row.code);setPage(1)}}>{provinceGrid.visibleColumns.map(column=><td key={column.key} data-grid-column={column.key}>{column.key==='name'?<button type="button" className="administrative-province-name" aria-pressed={selectedCode===row.code}>{row.name}</button>:String(row[column.key as keyof Province])}</td>)}</tr>)}</Fragment>):shownProvinces.map(row=><tr key={row.code} className={selectedCode===row.code?'is-selected':''} onClick={()=>{setSelectedCode(row.code);setPage(1)}}>{provinceGrid.visibleColumns.map(column=><td key={column.key} data-grid-column={column.key}>{column.key==='name'?<button type="button" className="administrative-province-name" aria-pressed={selectedCode===row.code}>{row.name}</button>:String(row[column.key as keyof Province])}</td>)}</tr>)}
     {!loading && filteredProvinces.length === 0 && <tr><td colSpan={provinceGrid.visibleColumns.length} className="administrative-unit-empty">Không có tỉnh/thành phù hợp.</td></tr>}
    </tbody></table>{loading && <p className="administrative-unit-state" role="status">Đang tải danh mục…</p>}</div>
    <footer className="administrative-unit-panel-foot data-grid-footer"><GridFilterSummary filters={provinceFilters} columns={provinceColumns} onRemove={key=>setProvinceFilters(old=>({...old,[key]:''}))} onClear={()=>setProvinceFilters({})}/><GridPager page={provincePage} pageSize={pageSize} total={filteredProvinces.length} onPage={setProvincePage} onPageSize={value=>{setPageSize(value);setPage(1);setProvincePage(1)}}/></footer>
   </section>
   <section className="administrative-unit-panel administrative-commune-panel" aria-labelledby="commune-list-title">
    <header className="administrative-unit-panel-head"><div><h2 id="commune-list-title">{communes?.province.name ?? provinces.find(x => x.code === selectedCode)?.name ?? 'Phường / xã'}</h2><small>Phường, xã và đặc khu</small></div><span className="administrative-unit-count">{(communes?.total ?? 0).toLocaleString('vi-VN')}</span></header>
    <div className="administrative-unit-toolbar"><label htmlFor="administrative-unit-search">Tìm tên hoặc mã<input id="administrative-unit-search" value={search} maxLength={100} onChange={e => setSearch(e.target.value)} placeholder="Nhập tên hoặc mã…" /></label></div>
    <div className="administrative-unit-table-wrap" onContextMenu={communeGrid.onContextMenu}><table className="administrative-unit-table administrative-commune-table"><thead><tr>{communeGrid.visibleColumns.map(column=><th key={column.key} data-grid-column={column.key}>{column.title}</th>)}</tr><GridFilterRow columns={communeGrid.visibleColumns} filters={communeFilters} onChange={(key,value)=>{setCommuneFilters(old=>({...old,[key]:value}));setPage(1)}}/></thead><tbody>
     {communeGrouped?Array.from(communeGrouped,([value,items])=><Fragment key={value}><tr className="data-grid-group-row"><td colSpan={communeGrid.visibleColumns.length}><button type="button" aria-expanded={!collapsedCommune.includes(value)} onClick={()=>setCollapsedCommune(old=>old.includes(value)?old.filter(x=>x!==value):[...old,value])}>{collapsedCommune.includes(value)?'＋':'−'} {communeColumns.find(x=>x.key===communeGrid.groupBy)?.title}: {value} ({items.length} trong trang hiện tại)</button></td></tr>{!collapsedCommune.includes(value)&&items.map(row=><tr key={row.code}>{communeGrid.visibleColumns.map(column=><td key={column.key} data-grid-column={column.key}>{column.key==='legacyDistrictName'?<>{row.legacyDistrictName}<small className="administrative-unit-subcode">Mã cũ: {row.legacyDistrictCode}</small></>:column.key==='unitType'?<span className="administrative-unit-type">{row.unitType}</span>:column.key==='name'?<strong>{row.name}</strong>:row.code}</td>)}</tr>)}</Fragment>):communes?.items.map(row => <tr key={row.code}>{communeGrid.visibleColumns.map(column=><td key={column.key} data-grid-column={column.key}>{column.key==='legacyDistrictName'?<>{row.legacyDistrictName}<small className="administrative-unit-subcode">Mã cũ: {row.legacyDistrictCode}</small></>:column.key==='unitType'?<span className="administrative-unit-type">{row.unitType}</span>:column.key==='name'?<strong>{row.name}</strong>:row.code}</td>)}</tr>)}
     {!loadingList && communes?.items.length === 0 && <tr><td colSpan={communeGrid.visibleColumns.length} className="administrative-unit-empty">Không tìm thấy phường/xã phù hợp.</td></tr>}
    </tbody></table>{loadingList && <p className="administrative-unit-state" role="status">Đang tải danh sách…</p>}{loading && !communes && <p className="administrative-unit-state" role="status">Đang tải dữ liệu…</p>}</div>
    <footer className="administrative-unit-panel-foot data-grid-footer"><GridFilterSummary filters={summaryFilters} columns={summaryColumns} onRemove={key=>{if(key==='search')setSearch('');else setCommuneFilters(old=>({...old,[key]:''}))}} onClear={()=>{setCommuneFilters({});setSearch('');setPage(1)}}/><GridPager page={page} pageSize={pageSize} total={communes?.total??0} onPage={setPage} onPageSize={value=>{setPageSize(value);setPage(1)}}/></footer>
    {provinceGrid.contextMenu}{communeGrid.contextMenu}
   </section>
  </ResizableSplit>
  <p className="administrative-unit-note">Mã tỉnh/phường xã và thông tin quận huyện cũ được lưu theo đúng các cột trong tệp nguồn.</p>
 </section>
}
