import { useEffect, useState, type FormEvent } from 'react'
import AccountMenu from './AccountMenu'
import AdminConsole from './AdminConsole'
import PasswordReset from './PasswordReset'
import CategoryMaintenance from './CategoryMaintenance'
import AdministrativeUnits from './AdministrativeUnits'
import Icd10Catalog from './Icd10Catalog'
import EmployeeDirectory from './EmployeeDirectory'
import ClinicStructure from './ClinicStructure'
import BranchDirectory from './BranchDirectory'
import WorkHome from './WorkHome'
import { menuIcon } from './workNavigation'
import { authMutation, authenticatedFetch, establishBrowserSession, setAccessToken } from './authClient'
type Session = { userName: string; email?: string | null; phoneNumber?: string | null; tenantId: string | null; branchId: string | null; isSuperAdmin: boolean; employee?: {id:string;employeeCode:string;fullName:string}|null }
type Navigation = { id: string; parentId: string | null; name: string; path: string | null }
type Workplace = { tenantId: string; tenantName: string; branchId: string; branchName: string }
const dashboardDate = new Date()
export default function AccessPanel() {
 const [session, setSession] = useState<Session | null | undefined>(undefined)
 const [workplaces, setWorkplaces] = useState<Workplace[]>([])
 const [error, setError] = useState('')
 const [busy, setBusy] = useState(false)
 const [selected, setSelected] = useState('')
 const [navigation, setNavigation] = useState<Navigation[]>([])
 const [canManage, setCanManage] = useState(false)
 const [page, setPage] = useState(window.location.pathname === '/' ? '/home' : window.location.pathname)
 const adminArea = page === '/admin' || page.startsWith('/admin/')
 function navigate(path: string) { window.history.pushState({}, '', path); setPage(path) }
 useEffect(() => { const update = () => setPage(window.location.pathname === '/' ? '/home' : window.location.pathname); window.addEventListener('popstate', update); return () => window.removeEventListener('popstate', update) }, [])
 async function load(signal?: AbortSignal) {
  const response = await authenticatedFetch('/api/auth/session', { signal })
 if (response.status === 401) { setSession(null); setWorkplaces([]); return }
  if (!response.ok) throw new Error('Không tải được phiên làm việc.')
  const current: Session = await response.json()
  const places = await authenticatedFetch('/api/auth/workplaces', { signal })
  if (!places.ok) throw new Error('Không tải được danh sách cơ sở.')
  const list: Workplace[] = await places.json()
  const capabilities = await authenticatedFetch('/api/auth/capabilities', { signal })
  if (!capabilities.ok) throw new Error('Không tải được quyền truy cập.')
  const rights = await capabilities.json()
  const nav = await authenticatedFetch('/api/auth/navigation', { signal })
  if (!nav.ok) throw new Error('Không tải được menu làm việc.')
  const items: Navigation[] = await nav.json()
  if (signal?.aborted) return
  setSession(current); setWorkplaces(list); setNavigation(items); setCanManage(rights.isSuperAdmin || ['roles.manage', 'members.manage'].every(code => rights.tenantWide.includes(code)))
  setSelected(list.find(p => p.branchId === current.branchId)?.branchId ?? list[0]?.branchId ?? '')
 }
 useEffect(() => {
  const controller = new AbortController()
  // Updates in load happen only after awaited network responses, not during effect setup.
  // oxlint-disable-next-line react/set-state-in-effect
  void load(controller.signal).catch(() => { if (!controller.signal.aborted) { setError('Không kết nối được dịch vụ đăng nhập.'); setSession(null) } })
  return () => controller.abort()
 }, [])
 useEffect(() => {
  const refreshProfile = () => { void authenticatedFetch('/api/auth/session').then(async response => { if (response.ok) setSession(await response.json() as Session) }).catch(() => {}) }
  window.addEventListener('clinic:session-refresh', refreshProfile)
  return () => window.removeEventListener('clinic:session-refresh', refreshProfile)
 }, [])
 async function login(event: FormEvent<HTMLFormElement>) {
  event.preventDefault(); setError(''); setBusy(true)
  const form = event.currentTarget; const data = new FormData(form)
  try {
   const result = await authMutation('login', { userName: data.get('userName'), password: data.get('password'), clientType: 'browser' })
   if (!result.ok) throw new Error(result.status === 429 ? 'Bạn thử quá nhiều lần. Vui lòng đợi một phút.' : 'Đăng nhập không thành công. Kiểm tra tài khoản hoặc thử lại sau.')
    await establishBrowserSession(result); form.reset(); await load(); navigate(adminArea ? '/admin/permissions/groups' : '/home')
  } catch (e) { setError(e instanceof Error ? e.message : 'Không kết nối được dịch vụ.') } finally { setBusy(false) }
 }
 async function choose(event?: FormEvent<HTMLFormElement>) {
  event?.preventDefault(); const place = workplaces.find(p => p.branchId === selected); if (!place) return
  setBusy(true); setError('')
    try { const response = await authMutation('workplace', { tenantId: place.tenantId, branchId: place.branchId }); if (!response.ok) throw new Error('Bạn không còn quyền tại cơ sở này.'); const token = await response.json() as {accessToken:string}; setAccessToken(token.accessToken); if (!adminArea) navigate('/home'); await load() }
  catch(e) { setError(e instanceof Error ? e.message : 'Không kết nối được dịch vụ.') } finally { setBusy(false) }
 }
 async function logout() {
  setBusy(true); setError('')
    try { const response = await authMutation('logout'); if (!response.ok) throw new Error('Chưa đăng xuất được.'); setAccessToken(null); setSession(null); setWorkplaces([]); setSelected(''); setCanManage(false); setNavigation([]); navigate(adminArea ? '/admin' : '/home') }
  catch(e) { setError(e instanceof Error ? e.message : 'Không kết nối được dịch vụ.') } finally { setBusy(false) }
 }
 const account = session ? <AccountMenu user={session} workplace={workplaces.find(p=>p.branchId===session.branchId)} busy={busy} onLogout={logout}/> : null
 const resetPasswordRoute = page.startsWith('/reset-password') || window.location.hash.startsWith('#/reset-password')
 const workspacePath = page === '/home' ? '/work' : page.startsWith('/home/') ? page.slice('/home'.length) : ''
 const categoryWorkspace = Boolean(session?.branchId && !adminArea && workspacePath === '/master-data/categories' && navigation.some(m => m.path === workspacePath))
 const administrativeUnitsWorkspace = Boolean(session?.branchId && !adminArea && workspacePath === '/master-data/administrative-units' && navigation.some(m => m.path === workspacePath))
 const icd10Workspace = Boolean(session?.branchId && !adminArea && workspacePath === '/master-data/icd10' && navigation.some(m => m.path === workspacePath))
 const employeeWorkspace = Boolean(session?.branchId && !adminArea && workspacePath === '/master-data/employees' && navigation.some(m => m.path === workspacePath))
 const specialtyWorkspace = Boolean(session?.branchId && !adminArea && workspacePath === '/master-data/specialties' && navigation.some(m => m.path === workspacePath))
 const clinicRoomWorkspace = Boolean(session?.branchId && !adminArea && workspacePath === '/master-data/clinic-rooms' && navigation.some(m => m.path === workspacePath))
 const branchWorkspace = Boolean(session?.branchId && !adminArea && workspacePath === '/master-data/branches' && (navigation.some(m => m.path === workspacePath) || session.isSuperAdmin))
 if(resetPasswordRoute) return <PasswordReset/>
 if(session?.branchId && adminArea && canManage) return <div className="administration-shell">{account}<div className="admin-context"><form onSubmit={choose}><label>Đơn vị / cơ sở<select value={selected} onChange={e=>setSelected(e.target.value)} disabled={busy}>{workplaces.map(p=><option key={p.branchId} value={p.branchId}>{p.tenantName} — {p.branchName}</option>)}</select></label><button disabled={busy}>Chọn cơ sở</button></form></div>{error&&<p role="alert" className="form-error">{error}</p>}<AdminConsole tenantName={workplaces.find(p=>p.branchId===session.branchId)?.tenantName??'Đơn vị hiện tại'} key={session.branchId} userName={session.userName} superAdmin={session.isSuperAdmin} page={page} onConfigChanged={load} onNavigate={navigate}/></div>
 if(session?.branchId && !adminArea && (workspacePath === '/work' || categoryWorkspace || administrativeUnitsWorkspace || icd10Workspace || employeeWorkspace || specialtyWorkspace || clinicRoomWorkspace || branchWorkspace)) return <div className={`work-dashboard-page${categoryWorkspace ? ' category-workspace-page' : ''}${administrativeUnitsWorkspace ? ' administrative-unit-workspace-page' : ''}${icd10Workspace ? ' icd10-workspace-page' : ''}${employeeWorkspace || specialtyWorkspace || clinicRoomWorkspace || branchWorkspace ? ' employee-workspace-page' : ''}`}>{account}<WorkHome navigation={navigation} currentPath={categoryWorkspace || administrativeUnitsWorkspace || icd10Workspace || employeeWorkspace || specialtyWorkspace || clinicRoomWorkspace || branchWorkspace ? workspacePath : '/work'} tenantName={workplaces.find(p=>p.tenantId===session.tenantId)?.tenantName??workplaces.find(p=>p.branchId===session.branchId)?.tenantName??'Phòng khám'} branchName={workplaces.find(p=>p.branchId===session.branchId)?.branchName??'Cơ sở hiện tại'} workplaces={workplaces} selectedBranchId={selected} busy={busy} onSelectBranch={setSelected} onChooseWorkplace={()=>void choose()} scopeReady={selected===session.branchId}>
  {branchWorkspace ? <BranchDirectory tenantId={session.tenantId} tenantName={workplaces.find(p=>p.tenantId===session.tenantId)?.tenantName??'Phòng khám'} superAdmin={session.isSuperAdmin} onBranchesChanged={load}/> : employeeWorkspace ? <EmployeeDirectory tenantName={workplaces.find(p=>p.tenantId===session.tenantId)?.tenantName??'Phòng khám'}/> : specialtyWorkspace ? <ClinicStructure kind="specialties" tenantName={workplaces.find(p=>p.tenantId===session.tenantId)?.tenantName??'Phòng khám'}/> : clinicRoomWorkspace ? <ClinicStructure kind="rooms" tenantName={workplaces.find(p=>p.tenantId===session.tenantId)?.tenantName??'Phòng khám'}/> : categoryWorkspace ? <><div className="work-page-heading"><div><p className="work-eyebrow">DANH MỤC</p><h1>Danh mục chuẩn và danh mục của đơn vị</h1><p>Danh mục chuẩn dùng chung toàn hệ thống; danh mục tự tạo thuộc đơn vị đang chọn.</p></div></div><CategoryMaintenance tenantName={workplaces.find(p=>p.tenantId===session.tenantId)?.tenantName??'Đơn vị hiện tại'} superAdmin={session.isSuperAdmin} scopeReady={selected===session.branchId}/></> : administrativeUnitsWorkspace ? <AdministrativeUnits/> : icd10Workspace ? <Icd10Catalog/> : <><div className="work-page-heading"><div><p className="work-eyebrow">{workplaces.find(p=>p.branchId===session.branchId)?.tenantName??'PHÒNG KHÁM'}</p><h1>Chào mừng trở lại, {session.userName}</h1><p>Không gian làm việc tại <strong>{workplaces.find(p=>p.branchId===session.branchId)?.branchName??'cơ sở hiện tại'}</strong>.</p></div></div><section className="work-welcome-card"><div><span className="work-card-kicker">SUNHEART MEDICAL</span><h2>Quản lý phòng khám, tập trung và rõ ràng</h2><p>Chọn một danh mục ở thanh điều hướng để bắt đầu quản lý dữ liệu cơ sở.</p></div><div className="work-date" aria-label="Ngày hiện tại"><span>{new Intl.DateTimeFormat('vi-VN',{weekday:'long'}).format(dashboardDate)}</span><strong>{new Intl.DateTimeFormat('vi-VN',{day:'2-digit',month:'long',year:'numeric'}).format(dashboardDate)}</strong></div></section><section className="work-quick-section"><div className="work-section-title"><div><p className="work-eyebrow">TRUY CẬP NHANH</p><h2>Chức năng khả dụng</h2></div><span>{navigation.filter(item=>item.path).length} mục</span></div><div className="work-quick-grid">{navigation.filter(item=>item.path).map(item=><a key={item.id} className="work-quick-card" href={'/home'+item.path}><span className="work-quick-icon" aria-hidden="true">{menuIcon(item.name)}</span><span><strong>{item.name}</strong><small>Mở chức năng</small></span><span className="work-arrow" aria-hidden="true">↗</span></a>)}{navigation.filter(item=>item.path).length===0&&<p className="work-empty">Chưa có chức năng nào được gán cho tài khoản này.</p>}</div></section></>}
 </WorkHome></div>
 return <div className={`${session === null ? 'portal-page' : ''} ${categoryWorkspace ? 'category-workspace-page' : ''}`}>{account}<h1>{!session ? (adminArea ? 'Đăng nhập quản trị' : 'Đăng nhập làm việc') : adminArea ? 'Quản trị phân quyền' : categoryWorkspace ? 'Quản lý danh mục' : 'Trang làm việc'}</h1><section className="panel access-panel" aria-labelledby="access-title"><h2 id="access-title">{session ? categoryWorkspace ? 'Danh mục dùng chung trong cơ sở' : 'Nơi làm việc' : (adminArea ? 'Đăng nhập quản trị' : 'Đăng nhập làm việc')}</h2>
  {session === undefined ? <p role="status">Đang kiểm tra phiên làm việc…</p> : session === null ? <form onSubmit={login} className="access-form">
    <p>{adminArea ? 'Đăng nhập tài khoản có quyền quản trị phân quyền.' : 'Sử dụng tài khoản được cấp để truy cập phòng khám.'}</p><label>Tên đăng nhập<span className="required-mark" aria-hidden="true"> *</span><input name="userName" autoComplete="username" maxLength={256} required disabled={busy} /></label>
   <label>Mật khẩu<span className="required-mark" aria-hidden="true"> *</span><input name="password" type="password" autoComplete="current-password" maxLength={1024} required disabled={busy} /></label><button disabled={busy}>{busy ? 'Đang xử lý…' : 'Đăng nhập'}</button>
  </form> : <><p>Xin chào, <strong>{session.userName}</strong>.</p>{workplaces.length ? <form className="access-form" onSubmit={choose}><label>Đơn vị / cơ sở<select value={selected} onChange={e => setSelected(e.target.value)} disabled={busy}>{workplaces.map(p => <option key={p.branchId} value={p.branchId}>{p.tenantName} — {p.branchName}</option>)}</select></label><button disabled={busy}>Chọn nơi làm việc</button></form> : <p>Chưa có cơ sở được cấp quyền. Liên hệ người quản lý.</p>}
   {session.branchId && <p className="message">Cơ sở hiện tại: <strong>{workplaces.find(p => p.branchId === session.branchId)?.branchName ?? 'Cần chọn lại'}</strong></p>}
    {categoryWorkspace ? <CategoryMaintenance tenantName={workplaces.find(p=>p.tenantId===session.tenantId)?.tenantName??'Đơn vị hiện tại'} superAdmin={session.isSuperAdmin} scopeReady={selected===session.branchId}/> : <>
    {session.branchId && adminArea && !canManage && <p role="alert">Bạn không có quyền quản trị phân quyền tại đơn vị này.</p>}
    {session.branchId && !adminArea && workspacePath !== '/work' && <p role="alert">{navigation.some(m => m.path === workspacePath) ? 'Chưa có màn hình cho chức năng này.' : 'Bạn không có quyền truy cập chức năng này.'}</p>}
    </>}
   </>}
  {error && <p className="form-error" role="alert">{error}</p>}
 </section></div>
}

