import { adminApi } from './adminApi'
import { ResizableSplit } from './ResizableSplit'
import { RecordTable, EditorDialog, StatusBadge } from './AdminWidgets'
import { useEffect, useState, type FormEvent } from 'react'
type Item = { id: string; code?: string; name?: string; userName?: string; isActive: boolean }
type Assignment = { id: string; userName: string; roleName: string; roleId: string; branchId: string | null; isActive: boolean }
const get = (path: string, signal?: AbortSignal) => adminApi(path, 'GET', undefined, signal)
const post = (path: string, body: unknown) => adminApi(path, 'POST', body)
export default function RoleAdmin({ userName, onChange }: { userName: string; onChange?: () => Promise<void> }) {
 const [allowed, setAllowed] = useState(false); const [members, setMembers] = useState<Item[]>([]); const [roles, setRoles] = useState<Item[]>([]); const [branches, setBranches] = useState<Item[]>([]); const [assignments, setAssignments] = useState<Assignment[]>([]);
 const [error, setError] = useState(''); const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false); const [reason, setReason] = useState('');
 useEffect(() => { const controller = new AbortController();
  async function load() {
   const capabilities = await get('auth/capabilities', controller.signal);
   if (!capabilities.isSuperAdmin && !['roles.manage', 'members.manage'].every(code => capabilities.tenantWide.includes(code))) return;
   const [m, r, a, b] = await Promise.all([get('organization/members', controller.signal), get('organization/roles', controller.signal), get('organization/role-assignments', controller.signal), (capabilities.isSuperAdmin || capabilities.tenantWide.includes('branches.read')) ? get('organization/branches', controller.signal) : Promise.resolve([])]);
   if (controller.signal.aborted) return;
   setMembers(m); setRoles(r); setAssignments(a); setBranches(b); setAllowed(true);
  }
  void load().catch(e => { if (!controller.signal.aborted) setError(e.message) }); return () => controller.abort();
 }, []);
 async function change(path: string, body: unknown) { setBusy(true); setError(''); setMessage('');
  try { await post(path, body); setAssignments(await get('organization/role-assignments')); setMessage('Đã lưu thay đổi quyền.'); setReason(''); return true }
  catch(e) { setError(e instanceof Error ? e.message : 'Không kết nối được dịch vụ.'); return false } finally { setBusy(false) }
 }
 async function grant(event: FormEvent<HTMLFormElement>) { event.preventDefault(); const data = new FormData(event.currentTarget); if(await change('organization/role-assignments', { membershipId: data.get('member'), roleId: group, branchId: data.get('branch') || null, reason })) setMode('') }
 async function createRole(event: FormEvent<HTMLFormElement>) {
  event.preventDefault(); const form = event.currentTarget; const data = new FormData(form); setBusy(true); setError(''); setMessage('');
  try { if(mode==='edit') await adminApi('organization/roles/'+group,'PUT',{ name:data.get('name'),isActive:data.get('active')==='on',reason:data.get('reason') }); else await post('organization/roles', { code: data.get('code'), name: data.get('name'), reason: data.get('reason') }); setRoles(await get('organization/roles')); await onChange?.(); form.reset(); setMode(''); setMessage(mode==='edit'?'Đã cập nhật nhóm.':'Đã tạo nhóm người dùng.') }
  catch(e) { setError(e instanceof Error ? e.message : 'Không tạo được nhóm.') } finally { setBusy(false) }
 }
 const [mode,setMode]=useState(''); const [group,setGroup]=useState(''); const [employee,setEmployee]=useState('');
 if (!allowed) return error ? <p role="alert" className="form-error">{error}</p> : null;

 const assigned=assignments.filter(a=>a.isActive&&a.roleId===group);const selectedRole=roles.find(r=>r.id===group);
 return <><ResizableSplit className="split-view" label="nhóm người dùng và nhân viên" storageKey="user-groups" stackAt={1100} initialPercent={40}><RecordTable title="Danh sách nhóm người dùng" columns={['Tên nhóm','Mã','Trạng thái']} rows={roles.map(r=>({id:r.id,search:(r.name??'')+' '+(r.code??''),values:[r.name,<code key="code">{r.code}</code>,<StatusBadge key="status" active={r.isActive}/>]}))} selected={group} onSelect={id=>{setGroup(id);setEmployee('')}} onAdd={()=>{setError('');setMode('create')}} onEdit={()=>{setError('');setMode('edit')}}/><RecordTable title={'Nhân viên · '+(selectedRole?.name??'Chọn nhóm')} actions={<><button className="outline-action" disabled={!selectedRole?.isActive||busy} onClick={()=>setMode('member')}>Thêm vào nhóm</button><button className="outline-action" disabled={!employee||busy||assigned.find(a=>a.id===employee)?.userName===userName} onClick={()=>setMode('remove')}>Xoá khỏi nhóm</button></>} columns={['Tài khoản','Phạm vi']} rows={assigned.map(a=>({id:a.id,search:a.userName,values:[a.userName,a.branchId?branches.find(b=>b.id===a.branchId)?.name??'Một cơ sở':'Toàn đơn vị']}))} selected={employee} onSelect={setEmployee}/></ResizableSplit>
 {(mode==='create'||mode==='edit')&&<EditorDialog title={mode==='edit'?'Sửa nhóm người dùng':'Thêm nhóm người dùng'} onClose={()=>setMode('')}><form className="access-form" onSubmit={createRole}><label>Mã nhóm<span className="required-mark" aria-hidden="true"> *</span><input name="code" defaultValue={mode==='edit'?selectedRole?.code:''} disabled={busy||mode==='edit'} required maxLength={50}/></label><label>Tên nhóm<span className="required-mark" aria-hidden="true"> *</span><input name="name" defaultValue={mode==='edit'?selectedRole?.name:''} required maxLength={200} disabled={busy}/></label>{mode==='edit'&&<label className="check-option"><input name="active" type="checkbox" defaultChecked={selectedRole?.isActive} disabled={busy}/>Hoạt động</label>}<button disabled={busy}>Lưu nhóm</button>{mode==='edit'&&<button className="outline-action" type="button" disabled={busy} onClick={async e=>{const form=e.currentTarget.form!;const data=new FormData(form);if(!form.reportValidity())return;setBusy(true);try{await adminApi('organization/roles/'+group,'DELETE',{reason:data.get('reason')});setRoles(await get('organization/roles'));await onChange?.();setMode('')}catch(e){setError(e instanceof Error?e.message:'Không xoá được.')}finally{setBusy(false)}}}>Xoá nhóm</button>}</form>{error&&<p role="alert" className="form-error">{error}</p>}</EditorDialog>}
 {mode==='member'&&<EditorDialog title={'Thêm nhân viên vào '+selectedRole?.name} onClose={()=>setMode('')}><form className="access-form" onSubmit={grant}><label>Nhân viên<span className="required-mark" aria-hidden="true"> *</span><select name="member" required disabled={busy}><option value="">Chọn nhân viên</option>{members.filter(m=>m.isActive&&m.userName!==userName).map(m=><option key={m.id} value={m.id}>{m.userName}</option>)}</select></label><label>Phạm vi<select name="branch" disabled={busy}><option value="">Toàn đơn vị</option>{branches.filter(b=>b.isActive).map(b=><option key={b.id} value={b.id}>{b.name}</option>)}</select></label><button disabled={busy}>Thêm vào nhóm</button></form>{error&&<p role="alert" className="form-error">{error}</p>}</EditorDialog>}
 {mode==='remove'&&<EditorDialog title="Xoá nhân viên khỏi nhóm" onClose={()=>setMode('')}><form className="access-form" onSubmit={async e=>{e.preventDefault();if(await change('organization/role-assignments/'+employee+'/revoke',{reason}))setMode('')}}><button disabled={busy}>Xác nhận thu hồi</button></form>{error&&<p role="alert" className="form-error">{error}</p>}</EditorDialog>}
 {error&&<p role="alert" className="form-error">{error}</p>}{message&&<p role="status">{message}</p>}</>
}
