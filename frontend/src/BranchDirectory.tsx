import { Fragment, useCallback, useEffect, useState, type FormEvent } from 'react'
import { adminApi } from './adminApi'
import { EditorDialog } from './AdminWidgets'
import { GridFilterRow, GridFilterSummary, GridPager } from './GridEnhancements'
import { useGridPreferences, type GridColumn } from './useGridPreferences'

type Branch = { id:string; tenantId:string; code:string; name:string; address:string|null; timeZone:string; isActive:boolean; createdAtUtc:string }
type Page = { page:number; pageSize:number; total:number; items:Branch[] }
type Tenant = { id:string; code:string; name:string; isActive:boolean }
const columns:GridColumn[]=[{key:'code',title:'Mã chi nhánh'},{key:'name',title:'Tên chi nhánh'},{key:'address',title:'Địa chỉ'},{key:'status',title:'Trạng thái'}]
const blank={name:'',address:''}

export default function BranchDirectory({tenantId,tenantName,superAdmin,onBranchesChanged}:{tenantId:string|null;tenantName:string;superAdmin:boolean;onBranchesChanged:()=>Promise<void>}) {
 const grid=useGridPreferences(columns)
 const [tenants,setTenants]=useState<Tenant[]>([]),[targetTenant,setTargetTenant]=useState(tenantId??'')
 const [data,setData]=useState<Page|null>(null),[filters,setFilters]=useState<Record<string,string>>({}),[page,setPage]=useState(1),[pageSize,setPageSize]=useState(200)
 const [editing,setEditing]=useState<Branch|null>(null),[dialogOpen,setDialogOpen]=useState(false),[form,setForm]=useState(blank)
 const [busy,setBusy]=useState(false),[loading,setLoading]=useState(true),[error,setError]=useState(''),[message,setMessage]=useState('')

 useEffect(()=>{if(!superAdmin)return;const c=new AbortController();void adminApi('platform/tenants','GET',undefined,c.signal).then((v:Tenant[])=>{if(c.signal.aborted)return;setTenants(v.filter(x=>x.isActive));setTargetTenant(old=>old||tenantId||v.find(x=>x.isActive)?.id||'')}).catch(e=>{if(!c.signal.aborted)setError(e instanceof Error?e.message:'Không tải được danh sách Tenant.')});return()=>c.abort()},[superAdmin,tenantId])

 const load=useCallback(async(signal?:AbortSignal)=>{
  if(!targetTenant)return
  setLoading(true);setError('')
  try {
   const q=new URLSearchParams({page:String(page),pageSize:String(pageSize)})
   if(superAdmin)q.set('tenantId',targetTenant)
   Object.entries(filters).forEach(([k,v])=>{if(!v.trim())return;if(k==='status')q.set('isActive',v==='Hoạt động'?'true':'false');else q.set(k,v.trim())})
   const result=await adminApi(`master-data/branches?${q}`,'GET',undefined,signal) as Page
   if(!signal?.aborted)setData(result)
  } catch(e){if(!signal?.aborted)setError(e instanceof Error?e.message:'Không tải được danh sách chi nhánh.')}
  finally{if(!signal?.aborted)setLoading(false)}
 },[targetTenant,superAdmin,page,pageSize,filters])
 // oxlint-disable-next-line react/set-state-in-effect
 useEffect(()=>{const c=new AbortController();void load(c.signal);return()=>c.abort()},[load])

 const rows=data?.items??[]
 const grouped=grid.groupBy?rows.reduce((m,item)=>{const key=grid.groupBy==='status'?(item.isActive?'Hoạt động':'Ngừng'):String(item[grid.groupBy as keyof Branch]??'—');m.set(key,[...(m.get(key)??[]),item]);return m},new Map<string,Branch[]>()):null
 const visible=grid.visibleColumns
 function open(item?:Branch){setEditing(item??null);setForm(item?{name:item.name,address:item.address??''}:blank);setError('');setDialogOpen(true)}
 async function save(event:FormEvent){event.preventDefault();if(!targetTenant)return;setBusy(true);setError('');try{await adminApi(editing?`master-data/branches/${editing.id}`:'master-data/branches',editing?'PUT':'POST',{...form,...(superAdmin?{tenantId:targetTenant}:{})});setDialogOpen(false);setMessage(editing?'Đã cập nhật chi nhánh.':'Đã tạo chi nhánh.');await Promise.all([load(),onBranchesChanged()])}catch(e){setError(e instanceof Error?e.message:'Không lưu được chi nhánh.')}finally{setBusy(false)}}
 async function toggle(item:Branch){setBusy(true);setError('');try{await adminApi(`master-data/branches/${item.id}/status`,'PUT',{isActive:!item.isActive,...(superAdmin?{tenantId:targetTenant}:{})});setMessage(item.isActive?'Đã ngừng chi nhánh.':'Đã kích hoạt chi nhánh.');await Promise.all([load(),onBranchesChanged()])}catch(e){setError(e instanceof Error?e.message:'Không cập nhật được trạng thái.')}finally{setBusy(false)}}
 const activeTenant=tenants.find(item=>item.id===targetTenant)

 return <section className="employee-directory clinic-structure-directory branch-directory">
  <div className="employee-directory-heading"><div><p className="work-eyebrow">DANH MỤC · {superAdmin?(activeTenant?.name??'Chọn Tenant'):tenantName}</p><h1>Chi nhánh</h1><p>Quản lý các chi nhánh thuộc Tenant đang chọn. Mã chi nhánh do hệ thống tự cấp.</p></div><div className="employee-actions">{superAdmin&&<label className="branch-tenant-select">Tenant<span className="required-mark" aria-hidden="true"> *</span><select value={targetTenant} onChange={e=>{setTargetTenant(e.target.value);setData(null);setFilters({});setPage(1)}} disabled={busy}><option value="">Chọn Tenant</option>{tenants.map(item=><option key={item.id} value={item.id}>{item.name} · {item.code}</option>)}</select></label>}<button type="button" disabled={busy||!targetTenant} onClick={()=>open()}>＋ Thêm chi nhánh</button></div></div>
  <section className="employee-grid-panel"><header className="employee-grid-heading"><h2>Danh sách chi nhánh <span>{(data?.total??0).toLocaleString('vi-VN')}</span></h2><small>{activeTenant?.name??tenantName}</small></header>
   <div className="employee-grid-scroll clinic-structure-grid" onContextMenu={grid.onContextMenu}><table><thead><tr>{visible.map(c=><th key={c.key} data-grid-column={c.key}>{c.title}</th>)}<th>Thao tác</th></tr><GridFilterRow columns={visible} filters={filters} onChange={(k,v)=>{setFilters(old=>({...old,[k]:v}));setPage(1)}}/></thead><tbody>
    {grouped?Array.from(grouped,([value,items])=><Fragment key={value}><tr className="data-grid-group-row"><td colSpan={visible.length+1}><button type="button" aria-expanded="true" onClick={()=>grid.setGroupBy(null)}>{columns.find(c=>c.key===grid.groupBy)?.title}: {value} ({items.length}) · Bỏ nhóm</button></td></tr>{items.map(item=><BranchRow key={item.id} item={item} columns={visible} busy={busy} edit={()=>open(item)} toggle={()=>void toggle(item)}/>)}</Fragment>):rows.map(item=><BranchRow key={item.id} item={item} columns={visible} busy={busy} edit={()=>open(item)} toggle={()=>void toggle(item)}/>)}
    {(!targetTenant||!loading)&&rows.length===0&&<tr><td colSpan={visible.length+1} className="employee-grid-empty">{targetTenant?'Tenant chưa có chi nhánh. Hãy thêm chi nhánh đầu tiên.':'Chọn Tenant để xem chi nhánh.'}</td></tr>}
   </tbody></table>{targetTenant&&loading&&<p className="employee-loading" role="status">Đang tải…</p>}</div>
   <footer className="employee-grid-footer data-grid-footer"><GridFilterSummary filters={filters} columns={columns} onRemove={key=>setFilters(old=>({...old,[key]:''}))} onClear={()=>setFilters({})}/><GridPager page={page} pageSize={pageSize} total={data?.total??0} onPage={setPage} onPageSize={value=>{setPageSize(value);setPage(1)}}/></footer>{grid.contextMenu}
  </section>
  {error&&<p className="employee-error" role="alert">{error}</p>}{message&&<p role="status">{message}</p>}
  {dialogOpen&&<EditorDialog className="employee-dialog branch-dialog" title={editing?'Cập nhật chi nhánh':'Thêm chi nhánh'} onClose={()=>{if(!busy)setDialogOpen(false)}}><form className="employee-form" onSubmit={save}><div className="employee-form-grid"><label>Tenant<span className="required-mark" aria-hidden="true"> *</span><input readOnly value={activeTenant?.name??tenantName}/></label><label>Tên chi nhánh<span className="required-mark" aria-hidden="true"> *</span><input required maxLength={200} value={form.name} disabled={busy} onChange={e=>setForm({...form,name:e.target.value})}/></label>{editing&&<label>Mã chi nhánh<input readOnly value={editing.code}/></label>}<label className="span-two">Địa chỉ<input maxLength={500} value={form.address} disabled={busy} onChange={e=>setForm({...form,address:e.target.value})}/></label><p className="employee-form-note span-two">Mã chi nhánh được hệ thống cấp tự động.</p></div>{error&&<p className="employee-error" role="alert">{error}</p>}<div className="dialog-actions"><button type="button" className="secondary-button" disabled={busy} onClick={()=>setDialogOpen(false)}>Đóng</button><button disabled={busy}>{busy?'Đang lưu…':'Lưu'}</button></div></form></EditorDialog>}
 </section>
}

function BranchRow({item,columns,busy,edit,toggle}:{item:Branch;columns:GridColumn[];busy:boolean;edit:()=>void;toggle:()=>void}){
 return <tr>{columns.map(c=><td key={c.key} data-grid-column={c.key}>{c.key==='status'?<span className={`employee-status ${item.isActive?'active':'inactive'}`}>{item.isActive?'Hoạt động':'Ngừng'}</span>:c.key==='code'?<code>{item.code}</code>:String(item[c.key as keyof Branch]??'—')}</td>)}<td className="clinic-structure-actions"><button type="button" disabled={busy} onClick={edit}>Sửa</button><button type="button" disabled={busy} onClick={toggle}>{item.isActive?'Ngừng':'Kích hoạt'}</button></td></tr>
}
