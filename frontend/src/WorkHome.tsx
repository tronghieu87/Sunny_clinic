import { useState, type ReactNode } from 'react'
import { menuIcon } from './workNavigation'

type Navigation = { id: string; parentId: string | null; name: string; path: string | null }
type Workplace={tenantId:string;tenantName:string;branchId:string;branchName:string}
type Props = { navigation: Navigation[]; currentPath: string; tenantName: string; branchName: string; workplaces:Workplace[]; selectedBranchId:string; busy:boolean; onSelectBranch:(id:string)=>void; onChooseWorkplace:()=>void; scopeReady:boolean; children: ReactNode }

export default function WorkHome({ navigation, currentPath, tenantName, branchName, workplaces, selectedBranchId, busy, onSelectBranch, onChooseWorkplace, scopeReady, children }: Props) {
 const [collapsed, setCollapsed] = useState(() => localStorage.getItem('clinic-work-sidebar') === 'collapsed')
 const [open, setOpen] = useState<Set<string>>(() => {
  const root=navigation.find(item=>item.parentId===null)
  return new Set([root?.id,...navigation.filter(item=>item.parentId===root?.id&&item.name==='Dữ liệu y tế').map(item=>item.id)].filter((id):id is string=>Boolean(id)))
 })
 const byId = new Map(navigation.map(item => [item.id, item]))
 const active = navigation.find(item => item.path === currentPath)
 const activeAncestors = new Set<string>()
 let parentId = active?.parentId ?? null
 while (parentId && byId.has(parentId) && !activeAncestors.has(parentId)) {
  activeAncestors.add(parentId)
  parentId = byId.get(parentId)?.parentId ?? null
 }
 function toggleSidebar() { const next = !collapsed; setCollapsed(next); localStorage.setItem('clinic-work-sidebar', next ? 'collapsed' : 'expanded') }
 function toggleGroup(id: string) { setOpen(previous => { const next = new Set(previous); if (next.has(id)) next.delete(id); else next.add(id); return next }) }
 return <div className={`work-layout${collapsed ? ' work-layout-collapsed' : ''}`}>
  <aside className="work-sidebar">
   <div className="work-sidebar-head"><button type="button" className="work-sidebar-toggle" onClick={toggleSidebar} aria-label={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'} aria-expanded={!collapsed} title={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'}><svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true"><path d="M4 6h16M4 12h16M4 18h16"/></svg></button>{!collapsed&&<strong>Danh mục</strong>}</div>
   <nav className="work-nav" aria-label="Menu chức năng">
    {navigation.filter(item => item.parentId === null).map(item => <NavigationNode key={item.id} item={item} all={navigation} currentPath={currentPath} collapsed={collapsed} open={open} activeAncestors={activeAncestors} toggleGroup={toggleGroup}/>) }
    {navigation.length === 0 && <p className="work-nav-empty">Chưa có chức năng</p>}
   </nav>
   {!collapsed&&<div className="work-sidebar-footer"><span className="work-status-dot" aria-hidden="true"/><span><strong>{branchName}</strong><small>{tenantName}</small></span></div>}
  </aside>
  <div className="work-main">
   <div className="work-context-bar"><form className="work-context-selector" onSubmit={event=>{event.preventDefault();onChooseWorkplace()}}><label><span className="work-context-label">ĐƠN VỊ / CƠ SỞ</span><select aria-label="Chọn đơn vị và cơ sở làm việc" value={selectedBranchId} onChange={event=>onSelectBranch(event.target.value)} disabled={busy}>{workplaces.map(place=><option key={place.branchId} value={place.branchId}>{place.tenantName} — {place.branchName}</option>)}</select></label><button disabled={busy||scopeReady}>{busy?'Đang chuyển…':'Chọn cơ sở'}</button></form><a className="work-home-link" href="/home" aria-label="Trang chủ"><svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="m3 10 9-7 9 7v10a1 1 0 0 1-1 1h-5v-7h-6v7H4a1 1 0 0 1-1-1z"/></svg><span>Trang chủ</span></a></div>
   {!scopeReady&&<p className="work-scope-pending" role="status">Đã chọn cơ sở khác. Nhấn “Chọn cơ sở” để áp dụng tenant trước khi tiếp tục.</p>}
   <div className="work-content" inert={!scopeReady}>{children}</div>
  </div>
 </div>
}

function NavigationNode({ item, all, currentPath, collapsed, open, activeAncestors, toggleGroup }: { item: Navigation; all: Navigation[]; currentPath: string; collapsed: boolean; open: Set<string>; activeAncestors: Set<string>; toggleGroup: (id: string) => void }) {
 const children = all.filter(child => child.parentId === item.id)
 const expanded = open.has(item.id) || activeAncestors.has(item.id)
 const active = item.path === currentPath
 const icon = menuIcon(item.name)
 return <div className={`work-nav-node${active ? ' is-active' : ''}${children.length ? ' has-children' : ''}`}>
  {item.path ? <a className="work-nav-link" href={'/home'+item.path} aria-current={active ? 'page' : undefined} aria-label={collapsed ? item.name : undefined} data-tooltip={collapsed ? item.name : undefined} onMouseEnter={event=>event.currentTarget.style.setProperty('--tooltip-top',`${event.currentTarget.getBoundingClientRect().top}px`)} onFocus={event=>event.currentTarget.style.setProperty('--tooltip-top',`${event.currentTarget.getBoundingClientRect().top}px`)}><span className="work-nav-icon" aria-hidden="true">{icon}</span>{!collapsed&&<span className="work-nav-name">{item.name}</span>}</a> : children.length ? <button type="button" className="work-nav-group" onClick={() => toggleGroup(item.id)} aria-expanded={expanded} aria-label={collapsed ? item.name : undefined} data-tooltip={collapsed ? item.name : undefined} onMouseEnter={event=>event.currentTarget.style.setProperty('--tooltip-top',`${event.currentTarget.getBoundingClientRect().top}px`)} onFocus={event=>event.currentTarget.style.setProperty('--tooltip-top',`${event.currentTarget.getBoundingClientRect().top}px`)}><span className="work-nav-icon" aria-hidden="true">{icon}</span>{!collapsed&&<><span className="work-nav-name">{item.name}</span><span className="work-nav-chevron" aria-hidden="true">{expanded?'⌄':'›'}</span></>}</button> : <span className="work-nav-label" aria-label={collapsed ? item.name : undefined} data-tooltip={collapsed ? item.name : undefined}><span className="work-nav-icon" aria-hidden="true">{icon}</span>{!collapsed&&<span className="work-nav-name">{item.name}</span>}</span>}
  {children.length>0&&expanded&&<div className="work-nav-children">{children.map(child=><NavigationNode key={child.id} item={child} all={all} currentPath={currentPath} collapsed={collapsed} open={open} activeAncestors={activeAncestors} toggleGroup={toggleGroup}/>)}</div>}
 </div>
}

