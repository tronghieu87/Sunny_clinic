import { useEffect, useState, type FormEvent } from 'react'
import { adminApi as api } from './adminApi'
import { EditorDialog, RecordTable, StatusBadge } from './AdminWidgets'

type Account = { id: string; userName: string; displayName: string; email: string | null; allBranches: boolean; isActive: boolean; isSuperAdmin: boolean; employeeId:string|null; employeeName:string|null }
type EmployeeOption = { id:string; employeeCode:string; fullName:string; membershipId:string|null }
type EditorMode = 'create' | 'display-name' | 'email' | 'password' | 'employee' | 'super-admin' | null

export default function AccountManagement({ userName, superAdmin }: { userName: string; superAdmin: boolean }) {
 const [accounts,setAccounts] = useState<Account[]>([])
 const [employees,setEmployees] = useState<EmployeeOption[]>([])
 const [selected,setSelected] = useState('')
 const [query,setQuery] = useState('')
 const [mode,setMode] = useState<EditorMode>(null)
 const [busy,setBusy] = useState(false)
 const [error,setError] = useState('')
 const [message,setMessage] = useState('')

 async function load() { const [items,staff]=await Promise.all([api('organization/accounts') as Promise<Account[]>,api('organization/accounts/employee-options') as Promise<EmployeeOption[]>]);setAccounts(items);setEmployees(staff);setSelected(current=>items.some(item=>item.id===current)?current:items[0]?.id??'') }
 useEffect(() => { let live=true; void Promise.all([api('organization/accounts') as Promise<Account[]>,api('organization/accounts/employee-options') as Promise<EmployeeOption[]>]).then(([items,staff])=>{if(live){setAccounts(items);setEmployees(staff);setSelected(items[0]?.id??'')}}).catch(e=>{if(live)setError(e.message)}); return()=>{live=false} }, [])

 async function create(event:FormEvent<HTMLFormElement>) {
  event.preventDefault(); const form=event.currentTarget; const data=new FormData(form)
  if(data.get('password')!==data.get('confirmPassword')) { setError('Mật khẩu xác nhận không khớp.'); return }
  setBusy(true); setError(''); setMessage('')
  let createdId:string|null=null
  try { const created=await api('organization/accounts','POST',{userName:data.get('userName'),displayName:data.get('displayName'),email:data.get('email'),password:data.get('password'),allBranches:data.get('allBranches')==='on'}) as {id:string};createdId=created.id;const employeeId=String(data.get('employeeId')??'');if(employeeId)await api(`organization/accounts/${created.id}/employee`,'PUT',{employeeId});await load(); form.reset(); setMode(null); setMessage(employeeId?'Đã tạo tài khoản và liên kết nhân viên. Hãy gán nhóm quyền.':'Đã tạo tài khoản. Hãy gán nhóm quyền trong mục Nhóm người dùng.') }
  catch(e) { if(createdId){setMode(null);await load().catch(()=>{});setError(`Tài khoản đã tạo nhưng chưa liên kết được nhân viên: ${e instanceof Error?e.message:'Lỗi không xác định'}. Chọn tài khoản vừa tạo để liên kết lại.`)}else setError(e instanceof Error?e.message:'Không tạo được tài khoản.') }
  finally { setBusy(false) }
 }

 async function resetPassword(event:FormEvent<HTMLFormElement>) {
  event.preventDefault(); const form=event.currentTarget; const data=new FormData(form)
  if(data.get('password')!==data.get('confirmPassword')) { setError('Mật khẩu xác nhận không khớp.'); return }
  setBusy(true); setError(''); setMessage('')
  try { await api('organization/accounts/'+selected+'/password','POST',{password:data.get('password'),reason:data.get('reason')}); form.reset(); setMode(null); setMessage('Đã đặt lại mật khẩu.') }
  catch(e) { setError(e instanceof Error?e.message:'Không đặt lại được mật khẩu.') }
  finally { setBusy(false) }
 }

 async function updateEmail(event:FormEvent<HTMLFormElement>) {
  event.preventDefault(); const form=event.currentTarget; const data=new FormData(form)
  setBusy(true); setError(''); setMessage('')
  try { await api('organization/accounts/'+selected+'/email','PUT',{email:data.get('email')}); await load(); setMode(null); setMessage('Đã cập nhật email tài khoản.') }
  catch(e) { setError(e instanceof Error?e.message:'Không cập nhật được email.') }
  finally { setBusy(false) }
 }

 async function updateDisplayName(event:FormEvent<HTMLFormElement>) {
  event.preventDefault(); const form=event.currentTarget; const data=new FormData(form)
  setBusy(true); setError(''); setMessage('')
  try { await api('organization/accounts/'+selected+'/display-name','PUT',{displayName:data.get('displayName')}); await load(); setMode(null); setMessage('Đã cập nhật tên tài khoản.') }
  catch(e) { setError(e instanceof Error?e.message:'Không cập nhật được tên tài khoản.') }
  finally { setBusy(false) }
 }

 async function updateEmployee(event:FormEvent<HTMLFormElement>) {
  event.preventDefault();const employeeId=String(new FormData(event.currentTarget).get('employeeId')??'')||null
  setBusy(true);setError('');setMessage('')
  try { await api(`organization/accounts/${selected}/employee`,'PUT',{employeeId});await load();setMode(null);setMessage(employeeId?'Đã liên kết nhân viên với tài khoản.':'Đã gỡ liên kết nhân viên.') }
  catch(e){setError(e instanceof Error?e.message:'Không cập nhật được liên kết nhân viên.')}
  finally{setBusy(false)}
 }

 async function setStatus(active:boolean,id=selected) {
  setBusy(true); setError(''); setMessage('')
  try { await api('organization/accounts/'+id+'/status','PUT',{isActive:active}); await load(); setMessage(active?'Đã kích hoạt tài khoản tại đơn vị.':'Đã ngừng tài khoản tại đơn vị.') }
  catch(e) { setError(e instanceof Error?e.message:'Không cập nhật được trạng thái.') }
  finally { setBusy(false) }
 }

 async function setSuperAdmin() {
  if(!account || !superAdmin) return
  setBusy(true);setError('');setMessage('')
  try { await api(`organization/accounts/${account.id}/super-admin`,'PUT',{isSuperAdmin:!account.isSuperAdmin});await load();setMode(null);setMessage(account.isSuperAdmin?'Đã thu hồi Super Admin toàn hệ thống.':'Đã cấp Super Admin toàn hệ thống.') }
  catch(e) { setError(e instanceof Error?e.message:'Không cập nhật được Super Admin.') }
  finally { setBusy(false) }
 }

 async function sendResetEmail(id=selected) {
  setBusy(true); setError(''); setMessage('')
  try { await api('organization/accounts/'+id+'/password-reset-email','POST'); setMessage('Đã gửi liên kết đặt lại mật khẩu đến email đã đăng ký.') }
  catch(e) { setError(e instanceof Error?e.message:'Không gửi được email đặt lại mật khẩu.') }
  finally { setBusy(false) }
 }

 const account=accounts.find(item=>item.id===selected)
 const visible=accounts.filter(item=>(item.userName+' '+item.displayName+' '+(item.email??'')).toLocaleLowerCase('vi').includes(query.toLocaleLowerCase('vi')))
 const rows=accounts.map(item=>({id:item.id,search:item.userName+' '+item.displayName+' '+(item.email??'')+' '+(item.employeeName??''),values:[item.userName,item.displayName,item.employeeName??'—',item.allBranches?'Tất cả cơ sở':'Chưa cấp phạm vi cơ sở',item.isSuperAdmin?<strong key="global-access" className="super-admin-badge">Super Admin</strong>:'—',<StatusBadge key="status" active={item.isActive}/>]}))
 return <>
  <details className="account-smtp-help"><summary>Cấu hình gửi email</summary><p>SMTP là cấu hình dùng chung của backend, không lưu trong tenant hay giao diện. Local dùng .NET User Secrets; server dùng environment variables. Tên key và lệnh cấu hình có trong README.md.</p></details>
    <div className="account-table"><RecordTable title="Tài khoản trong đơn vị" columns={['Tài khoản','Tên tài khoản','Nhân viên','Phạm vi cơ sở','Quyền hệ thống','Trạng thái']} rows={rows} selected={selected} onSelect={setSelected} onAdd={()=>{setError('');setMode('create')}} onEdit={()=>{setError('');setMode('display-name')}} onRefresh={()=>void load().catch(e=>setError(e.message))} actions={account&&<><button className="outline-action" disabled={busy} onClick={()=>{setError('');setMode('employee')}}>♧ Liên kết nhân viên</button><button className="outline-action" disabled={busy} onClick={()=>{setError('');setMode('email')}}>✉ Sửa email</button><button className="outline-action" disabled={busy||!account.email} onClick={()=>void sendResetEmail()}>↻ Gửi link đặt lại</button><button className="outline-action" disabled={busy} onClick={()=>{setError('');setMode('password')}}>⌑ Đặt mật khẩu</button>{superAdmin&&<button className="outline-action" disabled={busy||account.isSuperAdmin&&account.userName===userName||!account.isActive&&!account.isSuperAdmin} onClick={()=>{setError('');setMode('super-admin')}}>{account.isSuperAdmin?'Thu hồi Super Admin':'Cấp Super Admin'}</button>}<button className="outline-action" disabled={busy||account.userName===userName&&account.isActive} onClick={()=>void setStatus(!account.isActive)}>{account.isActive?'Ngừng tại đơn vị':'Kích hoạt'}</button></>}/></div>
    {error&&<p role="alert" className="form-error account-list-message">{error}</p>}{message&&<p role="status" className="account-detail-message">{message}</p>}
  <section className="account-mobile" aria-label="Tài khoản trong đơn vị">
   <div className="account-mobile-heading"><h2>Tài khoản <span className="count-badge">{visible.length}</span></h2><button onClick={()=>{setError('');setMode('create')}}>＋ Tạo tài khoản</button></div>
   <label className="account-mobile-search">Tìm tài khoản<input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Tên đăng nhập"/></label>
   {visible.length===0?<p className="account-empty">{accounts.length?'Không có tài khoản phù hợp.':'Chưa có tài khoản trong đơn vị.'}</p>:<ul className="account-mobile-list">{visible.map(item=><li key={item.id} className={'account-mobile-card'+(selected===item.id?' selected':'')}>
    <button className="account-card-select" aria-pressed={selected===item.id} onClick={()=>setSelected(item.id)}><strong>{item.userName} · {item.displayName}</strong><StatusBadge active={item.isActive}/></button>
    <dl><dt>Email</dt><dd>{item.email??'—'}</dd><dt>Nhân viên</dt><dd>{item.employeeName??'—'}</dd><dt>Phạm vi cơ sở</dt><dd>{item.allBranches?'Tất cả cơ sở':'Chưa cấp'}</dd><dt>Quyền hệ thống</dt><dd>{item.isSuperAdmin?'Super Admin':'Không'}</dd></dl>
    <div className="account-card-actions"><button className="outline-action" disabled={busy} onClick={()=>{setSelected(item.id);setError('');setMode('employee')}}>Liên kết nhân viên</button><button className="outline-action" disabled={busy} onClick={()=>{setSelected(item.id);setError('');setMode('display-name')}}>Sửa tên tài khoản</button><button className="outline-action" disabled={busy} onClick={()=>{setSelected(item.id);setError('');setMode('email')}}>Sửa email</button><button className="outline-action" disabled={busy||!item.email} onClick={()=>void sendResetEmail(item.id)}>Gửi link đặt lại</button>{superAdmin&&<button className="outline-action" disabled={busy||item.isSuperAdmin&&item.userName===userName||!item.isActive&&!item.isSuperAdmin} onClick={()=>{setSelected(item.id);setError('');setMode('super-admin')}}>{item.isSuperAdmin?'Thu hồi Super Admin':'Cấp Super Admin'}</button>}<button className="outline-action" disabled={busy||item.userName===userName&&item.isActive} onClick={()=>{setSelected(item.id);void setStatus(!item.isActive,item.id)}}>{item.isActive?'Ngừng tại đơn vị':'Kích hoạt'}</button></div>
   </li>)}</ul>}
  </section>
  {mode==='create'&&<EditorDialog title="Tạo tài khoản" onClose={()=>{if(!busy)setMode(null)}}><form className="access-form" onSubmit={create}>
   <label>Tên đăng nhập<span className="required-mark" aria-hidden="true"> *</span><input name="userName" minLength={3} maxLength={256} autoComplete="username" required disabled={busy}/></label>
   <label>Tên tài khoản<span className="required-mark" aria-hidden="true"> *</span><input name="displayName" minLength={2} maxLength={200} autoComplete="name" required disabled={busy}/></label>
    <label>Email nhận liên kết đặt lại<span className="required-mark" aria-hidden="true"> *</span><input name="email" type="email" maxLength={256} autoComplete="email" required disabled={busy}/></label>
   <label>Mật khẩu ban đầu<span className="required-mark" aria-hidden="true"> *</span><input name="password" type="password" minLength={8} maxLength={128} autoComplete="new-password" required disabled={busy}/></label>
   <label>Xác nhận mật khẩu<span className="required-mark" aria-hidden="true"> *</span><input name="confirmPassword" type="password" minLength={8} maxLength={128} autoComplete="new-password" required disabled={busy}/></label>
   <label>Nhân viên trong tenant<select name="employeeId" disabled={busy}><option value="">Chưa liên kết nhân viên</option>{employees.filter(employee=>!employee.membershipId).map(employee=><option key={employee.id} value={employee.id}>{employee.employeeCode} · {employee.fullName}</option>)}</select></label>
   <label className="check-option"><input name="allBranches" type="checkbox" defaultChecked disabled={busy}/>Được chọn tất cả cơ sở trong đơn vị</label>
   <p>Tài khoản mới chưa được gán nhóm quyền. Hãy cấp quyền riêng trong mục Nhóm người dùng.</p>
   {error&&<p role="alert" className="form-error">{error}</p>}
   <div className="dialog-actions"><button type="button" className="secondary-button" disabled={busy} onClick={()=>setMode(null)}>Hủy</button><button type="submit" disabled={busy}>{busy?'Đang tạo…':'Tạo tài khoản'}</button></div>
  </form></EditorDialog>}
  {mode==='employee'&&<EditorDialog title={'Liên kết nhân viên · '+(account?.userName??'')} onClose={()=>{if(!busy)setMode(null)}}><form className="access-form" onSubmit={updateEmployee}><label>Nhân viên trong tenant<select name="employeeId" defaultValue={account?.employeeId??''} disabled={busy}><option value="">Chưa liên kết nhân viên</option>{employees.filter(employee=>!employee.membershipId||employee.membershipId===selected).map(employee=><option key={employee.id} value={employee.id}>{employee.employeeCode} · {employee.fullName}</option>)}</select></label>{error&&<p role="alert" className="form-error">{error}</p>}<div className="dialog-actions"><button type="button" className="secondary-button" disabled={busy} onClick={()=>setMode(null)}>Đóng</button><button type="submit" disabled={busy}>{busy?'Đang lưu…':'Lưu liên kết'}</button></div></form></EditorDialog>}
  {mode==='super-admin'&&account&&<EditorDialog title={(account.isSuperAdmin?'Thu hồi':'Cấp')+' Super Admin · '+account.userName} onClose={()=>{if(!busy)setMode(null)}}><div className="access-form"><p><strong>{account.userName}</strong> {account.isSuperAdmin?'sẽ mất':'sẽ có'} quyền quản trị toàn hệ thống, áp dụng trên tất cả tenant. Nhóm “Quản lý đơn vị” chỉ cấp quyền trong tenant và độc lập với quyền này.</p>{error&&<p role="alert" className="form-error">{error}</p>}<div className="dialog-actions"><button type="button" className="secondary-button" disabled={busy} onClick={()=>setMode(null)}>Hủy</button><button type="button" disabled={busy} onClick={()=>void setSuperAdmin()}>{busy?'Đang lưu…':account.isSuperAdmin?'Xác nhận thu hồi':'Xác nhận cấp quyền'}</button></div></div></EditorDialog>}
  {mode==='display-name'&&<EditorDialog title={'Cập nhật tên tài khoản · '+(account?.userName??'')} onClose={()=>{if(!busy)setMode(null)}}><form className="access-form" onSubmit={updateDisplayName}>
   <label>Tên tài khoản<span className="required-mark" aria-hidden="true"> *</span><input name="displayName" minLength={2} maxLength={200} defaultValue={account?.displayName??''} required disabled={busy}/></label>
   {error&&<p role="alert" className="form-error">{error}</p>}
   <div className="dialog-actions"><button type="button" className="secondary-button" disabled={busy} onClick={()=>setMode(null)}>Hủy</button><button type="submit" disabled={busy}>{busy?'Đang lưu…':'Lưu tên tài khoản'}</button></div>
  </form></EditorDialog>}
  {mode==='email'&&<EditorDialog title={'Cập nhật email · '+(account?.userName??'')} onClose={()=>{if(!busy)setMode(null)}}><form className="access-form" onSubmit={updateEmail}>
   <p>Email này dùng chung cho đăng nhập và đặt lại mật khẩu ở mọi đơn vị liên kết.</p>
   <label>Email<span className="required-mark" aria-hidden="true"> *</span><input name="email" type="email" maxLength={256} autoComplete="email" defaultValue={account?.email??''} required disabled={busy}/></label>
   {error&&<p role="alert" className="form-error">{error}</p>}
   <div className="dialog-actions"><button type="button" className="secondary-button" disabled={busy} onClick={()=>setMode(null)}>Hủy</button><button type="submit" disabled={busy}>{busy?'Đang lưu…':'Lưu email'}</button></div>
  </form></EditorDialog>}
  {mode==='password'&&<EditorDialog title={'Đặt lại mật khẩu · '+(account?.userName??'')} onClose={()=>{if(!busy)setMode(null)}}><form className="access-form" onSubmit={resetPassword}>
   <p>Mật khẩu đăng nhập của tài khoản này thay đổi ở mọi đơn vị liên kết.</p>
   <label>Mật khẩu mới<span className="required-mark" aria-hidden="true"> *</span><input name="password" type="password" minLength={8} maxLength={128} autoComplete="new-password" required disabled={busy}/></label>
   <label>Xác nhận mật khẩu<span className="required-mark" aria-hidden="true"> *</span><input name="confirmPassword" type="password" minLength={8} maxLength={128} autoComplete="new-password" required disabled={busy}/></label>
   <label>Lý do<input name="reason" maxLength={500} disabled={busy}/></label>
   {error&&<p role="alert" className="form-error">{error}</p>}
   <div className="dialog-actions"><button type="button" className="secondary-button" disabled={busy} onClick={()=>setMode(null)}>Hủy</button><button type="submit" disabled={busy}>{busy?'Đang cập nhật…':'Đặt lại mật khẩu'}</button></div>
  </form></EditorDialog>}
 </>
}
