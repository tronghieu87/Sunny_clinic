import { Fragment, useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { adminApi } from './adminApi'
import { EditorDialog } from './AdminWidgets'
import { GridFilterRow, GridFilterSummary, GridPager } from './GridEnhancements'
import { useGridPreferences, type GridColumn } from './useGridPreferences'

type Employee = { id:string; employeeCode:string; fullName:string; positionCategoryId:string|null; position:string|null; genderCategoryId:string|null; gender:string|null; dateOfBirth:string|null; phoneNumber:string|null; email:string|null; occupationCategoryId:string|null; occupation:string|null; membershipId:string|null; startDate:string|null; isActive:boolean }
type EmployeePage = { page:number; pageSize:number; total:number; items:Employee[] }
type CategoryGroup = { code:string; name:string; items:{id:string;code:string;name:string}[] }
type EmployeeDetail = Employee & { identityDocumentTypeId:string|null; identityDocumentNumber:string|null; createdAtUtc:string; updatedAtUtc:string }
const columns:GridColumn[]=[{key:'employeeCode',title:'Mã nhân viên'},{key:'fullName',title:'Họ và tên'},{key:'position',title:'Chức vụ'},{key:'dateOfBirth',title:'Ngày sinh'},{key:'phone',title:'Số điện thoại'},{key:'occupation',title:'Nghề nghiệp'},{key:'status',title:'Trạng thái'}]
const blank = {employeeCode:'',fullName:'',positionCategoryId:'',genderCategoryId:'',dateOfBirth:'',phoneNumber:'',email:'',identityDocumentTypeId:'',identityDocumentNumber:'',occupationCategoryId:'',startDate:'',isActive:true}
const maxBirthDate = new Date().toISOString().slice(0,10)

export default function EmployeeDirectory({ tenantName }: {tenantName:string}) {
 const grid=useGridPreferences(columns)
 const [rows,setRows]=useState<EmployeePage|null>(null)
 const [lookups,setLookups]=useState<CategoryGroup[]>([])
 const [filters,setFilters]=useState<Record<string,string>>({})
 const [page,setPage]=useState(1)
 const [pageSize,setPageSize]=useState(200)
 const [selected,setSelected]=useState('')
 const [dialog,setDialog]=useState(false)

 const [detail,setDetail]=useState<EmployeeDetail|null>(null)
 const [form,setForm]=useState(blank)
 const [busy,setBusy]=useState(false)
 const [loading,setLoading]=useState(true)
 const [error,setError]=useState('')
 const [message,setMessage]=useState('')
 const [collapsed,setCollapsed]=useState<string[]>([])




 const load=useCallback(async (signal?:AbortSignal) => {
  setLoading(true);setError('')
  try {
   const query=new URLSearchParams({page:String(page),pageSize:String(pageSize)})
   Object.entries(filters).forEach(([key,value])=>{if(value.trim()){const parameter=key==='employeeCode'?'code':key==='fullName'?'name':key==='status'?'isActive':key;if(key==='status'&&value==='Hoạt động')query.set(parameter,'true');else if(key==='status'&&value==='Ngừng')query.set(parameter,'false');else if(key!=='status')query.set(parameter,value.trim())}})
   const result=await adminApi(`master-data/employees?${query}`,'GET',undefined,signal) as EmployeePage
   if(!signal?.aborted){setRows(result);setSelected(current=>result.items.some(item=>item.id===current)?current:'')}
  } catch(e){if(!signal?.aborted)setError(e instanceof Error?e.message:'Không tải được danh sách nhân viên.')}
  finally{if(!signal?.aborted)setLoading(false)}
 },[page,pageSize,filters])
 useEffect(()=>{const controller=new AbortController();void adminApi('master-data/employees/lookups','GET',undefined,controller.signal).then((data:unknown)=>{if(!controller.signal.aborted)setLookups(data as CategoryGroup[])}).catch(e=>{if(!controller.signal.aborted)setError(e instanceof Error?e.message:'Không tải được danh mục hồ sơ.')});return()=>controller.abort()},[])
 // oxlint-disable-next-line react/set-state-in-effect
 useEffect(()=>{const controller=new AbortController();void load(controller.signal);return()=>controller.abort()},[load])
 const categories=useMemo(()=>Object.fromEntries(lookups.map(group=>[group.code,group.items])),[lookups])
 const grouped=grid.groupBy?rows?.items.reduce((map,item)=>{const key=grid.groupBy!;const value=String(key==='phone'?item.phoneNumber:key==='status'?(item.isActive?'Hoạt động':'Ngừng'):item[key as keyof Employee]??'—');map.set(value,[...(map.get(value)??[]),item]);return map},new Map<string,Employee[]>()):null
 const selectedEmployee=rows?.items.find(item=>item.id===selected)

 async function openEditor(id?:string){setError('');setMessage('');setDetail(null);setForm(blank);if(id){try{const item=await adminApi(`master-data/employees/${id}`) as EmployeeDetail;setDetail(item);setForm({employeeCode:item.employeeCode,fullName:item.fullName,positionCategoryId:item.positionCategoryId??'',genderCategoryId:item.genderCategoryId??'',dateOfBirth:item.dateOfBirth??'',phoneNumber:item.phoneNumber??'',email:item.email??'',identityDocumentTypeId:item.identityDocumentTypeId??'',identityDocumentNumber:item.identityDocumentNumber??'',occupationCategoryId:item.occupationCategoryId??'',startDate:item.startDate??'',isActive:item.isActive});setDialog(true)}catch(e){setError(e instanceof Error?e.message:'Không tải được hồ sơ nhân viên.')}}else setDialog(true)}
 async function save(event:FormEvent<HTMLFormElement>){event.preventDefault();setBusy(true);setError('');setMessage('');try{const payload={...form,positionCategoryId:form.positionCategoryId||null,genderCategoryId:form.genderCategoryId||null,dateOfBirth:form.dateOfBirth||null,phoneNumber:form.phoneNumber||null,email:form.email||null,identityDocumentTypeId:form.identityDocumentTypeId||null,identityDocumentNumber:form.identityDocumentNumber||null,occupationCategoryId:form.occupationCategoryId||null,startDate:form.startDate||null};if(detail){await adminApi(`master-data/employees/${detail.id}`,'PUT',payload);setMessage('Đã cập nhật hồ sơ nhân viên.')}else{await adminApi('master-data/employees','POST',payload);setMessage('Đã tạo hồ sơ nhân viên. Liên kết tài khoản tại Quản lý tài khoản.')}setDialog(false);await load()}catch(e){setError(e instanceof Error?e.message:'Không lưu được hồ sơ nhân viên.')}finally{setBusy(false)}}
 async function toggleStatus(item:Employee){setBusy(true);setError('');setMessage('');try{await adminApi(`master-data/employees/${item.id}/status`,'PUT',{isActive:!item.isActive});setMessage(item.isActive?'Đã ngừng nhân viên.':'Đã kích hoạt nhân viên.');await load()}catch(e){setError(e instanceof Error?e.message:'Không cập nhật được trạng thái.')}finally{setBusy(false)}}
 const renderRow=(item:Employee)=><tr key={item.id} className={selected===item.id?'is-selected':''} onClick={()=>setSelected(item.id)}>{grid.visibleColumns.map(column=><td key={column.key} data-grid-column={column.key}>{column.key==='employeeCode'?item.employeeCode:column.key==='fullName'?<button type="button" className="employee-name-button" onClick={e=>{e.stopPropagation();void openEditor(item.id)}}>{item.fullName}</button>:column.key==='position'?item.position??'—':column.key==='dateOfBirth'?item.dateOfBirth??'—':column.key==='phone'?item.phoneNumber??'—':column.key==='occupation'?item.occupation??'—':<span className={`employee-status ${item.isActive?'active':''}`}>{item.isActive?'Hoạt động':'Ngừng'}</span>}</td>)}</tr>

 return <section className="employee-directory" aria-labelledby="employee-directory-title">
  <header className="employee-directory-heading"><div><p className="work-eyebrow">TỔ CHỨC PHÒNG KHÁM</p><h1 id="employee-directory-title">Danh mục nhân viên</h1><p>Quản lý hồ sơ nhân viên thuộc {tenantName}.</p></div><div className="employee-actions"><button type="button" className="employee-secondary" disabled={busy} onClick={()=>void load()}>Nạp dữ liệu</button><button type="button" disabled={busy} onClick={()=>void openEditor()}>＋ Thêm nhân viên</button></div></header>
  {message&&<p className="employee-message" role="status">{message}</p>}{error&&<p className="employee-error" role="alert">{error}</p>}
  <section className="employee-grid-panel"><div className="employee-grid-heading"><h2>Danh sách nhân viên <span>{(rows?.total??0).toLocaleString('vi-VN')}</span></h2><small>{loading?'Đang tải…':`${rows?.total.toLocaleString('vi-VN')??0} nhân viên`}</small></div>
   <div className="employee-grid-scroll" onContextMenu={grid.onContextMenu}><table><thead><tr>{grid.visibleColumns.map(column=><th key={column.key} data-grid-column={column.key}>{column.title}</th>)}</tr><GridFilterRow columns={grid.visibleColumns} filters={filters} onChange={(key,value)=>{setFilters(old=>({...old,[key]:value}));setPage(1)}}/></thead><tbody>
    {grouped?Array.from(grouped,([value,items])=><Fragment key={value}><tr className="data-grid-group-row"><td colSpan={grid.visibleColumns.length}><button type="button" aria-expanded={!collapsed.includes(value)} onClick={()=>setCollapsed(old=>old.includes(value)?old.filter(x=>x!==value):[...old,value])}>{collapsed.includes(value)?'＋':'−'} {columns.find(column=>column.key===grid.groupBy)?.title}: {value} ({items.length} trong trang)</button></td></tr>{!collapsed.includes(value)&&items.map(renderRow)}</Fragment>):rows?.items.map(renderRow)}
    {!loading&&rows?.items.length===0&&<tr><td colSpan={grid.visibleColumns.length} className="employee-empty">{rows.total===0?'Chưa có hồ sơ nhân viên. Hãy thêm nhân viên đầu tiên.':'Không có nhân viên phù hợp.'}</td></tr>}
   </tbody></table>{loading&&<p className="employee-loading" role="status">Đang tải danh sách nhân viên…</p>}</div>
   <footer className="data-grid-footer employee-grid-footer"><GridFilterSummary filters={filters} columns={columns} onRemove={key=>setFilters(old=>({...old,[key]:''}))} onClear={()=>{setFilters({});setPage(1)}}/><GridPager page={page} pageSize={pageSize} total={rows?.total??0} onPage={setPage} onPageSize={value=>{setPageSize(value);setPage(1)}}/></footer>
   {selectedEmployee&&<div className="employee-selected-tools"><span>Đã chọn: <strong>{selectedEmployee.fullName}</strong></span><button type="button" className="employee-secondary" disabled={busy} onClick={()=>void openEditor(selectedEmployee.id)}>Sửa hồ sơ</button><button type="button" className="employee-secondary" disabled={busy} onClick={()=>void toggleStatus(selectedEmployee)}>{selectedEmployee.isActive?'Ngừng hoạt động':'Kích hoạt'}</button></div>}
  </section>
  {grid.contextMenu}
  {dialog&&<EditorDialog className="employee-dialog" title={detail?'Cập nhật hồ sơ nhân viên':'Thêm hồ sơ nhân viên'} onClose={()=>{if(!busy)setDialog(false)}}><form className="employee-form" onSubmit={save}><div className="employee-form-grid">
   <label><span className="employee-field-label">Mã nhân viên (tự cấp)</span><input value={form.employeeCode} placeholder={detail?'':'Tự động cấp khi lưu'} readOnly aria-readonly="true" disabled={busy}/></label>
   <label><span className="employee-field-label">Họ và tên<span className="required-mark" aria-hidden="true"> *</span></span><input value={form.fullName} required maxLength={200} disabled={busy} onChange={e=>setForm({...form,fullName:e.target.value})}/></label>
   <label>Chức vụ<select value={form.positionCategoryId} disabled={busy} onChange={e=>setForm({...form,positionCategoryId:e.target.value})}><option value="">Chọn chức vụ</option>{(categories.EMPLOYEE_POSITION??[]).map(item=><option key={item.id} value={item.id}>{item.name}</option>)}{detail?.positionCategoryId&&!(categories.EMPLOYEE_POSITION??[]).some(item=>item.id===detail.positionCategoryId)&&<option value={detail.positionCategoryId}>{detail.position??'Chức vụ hiện tại'} (đã ngừng)</option>}</select></label>
   <label>Giới tính<select value={form.genderCategoryId} disabled={busy} onChange={e=>setForm({...form,genderCategoryId:e.target.value})}><option value="">Chọn giới tính</option>{(categories.GENDER??[]).map(item=><option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
   <label>Ngày sinh<input type="date" value={form.dateOfBirth} max={maxBirthDate} disabled={busy} onChange={e=>setForm({...form,dateOfBirth:e.target.value})}/></label>
   <label>Số điện thoại<input type="tel" value={form.phoneNumber} maxLength={30} disabled={busy} onChange={e=>setForm({...form,phoneNumber:e.target.value})}/></label>
   <label>Email<input type="email" value={form.email} maxLength={254} disabled={busy} onChange={e=>setForm({...form,email:e.target.value})}/></label>
   <label>Nghề nghiệp<select value={form.occupationCategoryId} disabled={busy} onChange={e=>setForm({...form,occupationCategoryId:e.target.value})}><option value="">Chọn nghề nghiệp</option>{(categories.OCCUPATION??[]).map(item=><option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
   <label>Ngày bắt đầu làm<input type="date" value={form.startDate} disabled={busy} onChange={e=>setForm({...form,startDate:e.target.value})}/></label>
   <label>Loại giấy tờ<select value={form.identityDocumentTypeId} disabled={busy} onChange={e=>setForm({...form,identityDocumentTypeId:e.target.value})}><option value="">Chọn loại giấy tờ</option>{(categories.IDENTITY_DOCUMENT??[]).map(item=><option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
   <label>Số giấy tờ<input value={form.identityDocumentNumber} maxLength={50} disabled={busy} onChange={e=>setForm({...form,identityDocumentNumber:e.target.value})}/></label>
  </div>{error&&<p role="alert" className="employee-error">{error}</p>}<div className="dialog-actions"><button type="button" className="secondary-button" disabled={busy} onClick={()=>setDialog(false)}>Đóng</button><button disabled={busy}>{busy?'Đang lưu…':'Lưu hồ sơ'}</button></div></form></EditorDialog>}
 </section>
}
