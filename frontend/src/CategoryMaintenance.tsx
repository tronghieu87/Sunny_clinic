import { Fragment, useEffect, useMemo, useState, type FormEvent } from 'react'
import { adminApi } from './adminApi'
import { GridFilterRow, GridFilterSummary, GridPager } from './GridEnhancements'
import { useGridPreferences } from './useGridPreferences'
import { ResizableSplit } from './ResizableSplit'

type CategoryGroup = { id: string; tenantId:string|null; isSystem:boolean; code: string; name: string; description: string | null; sortOrder: number; isActive: boolean }
type CategoryItem = { id: string; categoryGroupId: string; code: string; name: string; sortOrder: number; isActive: boolean }

export default function CategoryMaintenance({tenantName,superAdmin,scopeReady=true}:{tenantName:string;superAdmin:boolean;scopeReady?:boolean}) {
 const groupColumns = useMemo(() => [{key:'name',title:'Tên nhóm'},{key:'code',title:'Mã hệ thống'},{key:'scope',title:'Phạm vi'},{key:'status',title:'Trạng thái'},{key:'actions',title:'Thao tác'}], [])
 const itemColumns = useMemo(() => [{key:'name',title:'Tên giá trị'},{key:'code',title:'Mã'},{key:'status',title:'Trạng thái'},{key:'actions',title:'Thao tác'}], [])
 const groupGrid = useGridPreferences(groupColumns)
 const itemGrid = useGridPreferences(itemColumns)
 const [groupFilters, setGroupFilters] = useState<Record<string,string>>({})
 const [itemFilters, setItemFilters] = useState<Record<string,string>>({})
 const [groupPage, setGroupPage] = useState(1)
 const [itemPage, setItemPage] = useState(1)
 const [groupPageSize, setGroupPageSize] = useState(200)
 const [itemPageSize, setItemPageSize] = useState(200)
 const [collapsedGroups, setCollapsedGroups] = useState<string[]>([])
 const [collapsedItems, setCollapsedItems] = useState<string[]>([])
 const [groups, setGroups] = useState<CategoryGroup[]>([])
 const [items, setItems] = useState<CategoryItem[]>([])
 const [itemGroupId, setItemGroupId] = useState('')
 const [selected, setSelected] = useState('')
 const [groupEditing, setGroupEditing] = useState<CategoryGroup | null>(null)
 const [itemEditing, setItemEditing] = useState<CategoryItem | null>(null)
 const [groupName, setGroupName] = useState('')
 const [groupGlobal, setGroupGlobal] = useState(false)
 const [groupDescription, setGroupDescription] = useState('')
 const [itemName, setItemName] = useState('')
 const [message, setMessage] = useState('')
 const [busy, setBusy] = useState(false)
 const [groupsLoading, setGroupsLoading] = useState(true)

 async function loadGroups(preferred = selected) {
  const rows = await adminApi('master-data/categories') as CategoryGroup[]
  setGroups(rows)
  const next = rows.some(x => x.id === preferred) ? preferred : rows[0]?.id ?? ''
  setSelected(next)
  return next
 }
 async function loadItems(groupId: string) {
  const rows = groupId ? await adminApi(`master-data/categories/${groupId}/items?includeInactive=true`) as CategoryItem[] : []
  setItems(rows); setItemGroupId(groupId)
 }
 useEffect(() => { let active = true; void adminApi('master-data/categories').then((data: unknown) => { if (!active) return; const rows = data as CategoryGroup[]; setGroups(rows); setSelected(rows[0]?.id ?? '') }).catch(e => { if (active) setMessage(e instanceof Error ? e.message : 'Không tải được danh mục.') }).finally(() => { if (active) setGroupsLoading(false) }); return () => { active = false } }, [])
 useEffect(() => { let active = true; if (!selected) return () => { active = false }; void adminApi(`master-data/categories/${selected}/items?includeInactive=true`).then((data: unknown) => { if (active) { setItems(data as CategoryItem[]); setItemGroupId(selected) } }).catch(e => { if (active) { setMessage(e instanceof Error ? e.message : 'Không tải được giá trị danh mục.'); setItems([]); setItemGroupId(selected) } }); return () => { active = false } }, [selected])

 async function saveGroup(event: FormEvent<HTMLFormElement>) {
  event.preventDefault(); setBusy(true); setMessage('')
  try {
   const body = { name: groupName, description: groupDescription || null, sortOrder: groupEditing?.sortOrder ?? 0, isActive: groupEditing?.isActive ?? true, isGlobal:groupEditing?groupEditing.tenantId===null:groupGlobal }
   let preferredId = groupEditing?.id
   if (groupEditing) await adminApi(`master-data/categories/${groupEditing.id}`, 'PUT', body)
   else preferredId = (await adminApi('master-data/categories', 'POST', body) as CategoryGroup).id
   const preferred = await loadGroups(preferredId); setGroupEditing(null); setGroupGlobal(false); setGroupName(''); setGroupDescription('')
   if (preferred && preferred === selected) await loadItems(preferred)
  } catch (e) { setMessage(e instanceof Error ? e.message : 'Không lưu được nhóm danh mục.') } finally { setBusy(false) }
 }
 async function saveItem(event: FormEvent<HTMLFormElement>) {
  event.preventDefault(); if (!selected) return; setBusy(true); setMessage('')
  try {
   const body = { name: itemName, sortOrder: itemEditing?.sortOrder ?? 0, isActive: itemEditing?.isActive ?? true }
   if (itemEditing) await adminApi(`master-data/categories/${selected}/items/${itemEditing.id}`, 'PUT', body)
   else await adminApi(`master-data/categories/${selected}/items`, 'POST', body)
   await loadItems(selected); setItemEditing(null); setItemName('')
  } catch (e) { setMessage(e instanceof Error ? e.message : 'Không lưu được giá trị danh mục.') } finally { setBusy(false) }
 }
 function editGroup(group: CategoryGroup) { setGroupEditing(group); setGroupGlobal(group.tenantId===null); setGroupName(group.name); setGroupDescription(group.description ?? '') }
 function newGroup() { setGroupEditing(null); setGroupGlobal(false); setGroupName(''); setGroupDescription('') }
 function editItem(item: CategoryItem) { setItemEditing(item); setItemName(item.name) }
 function newItem() { setItemEditing(null); setItemName('') }

  const visibleItems = itemGroupId === selected ? items : []
 const selectedGroup=groups.find(x=>x.id===selected)
 const canEditSelected=Boolean(selectedGroup&&(selectedGroup.tenantId!==null||superAdmin))
  const itemsLoading = Boolean(selected && itemGroupId !== selected)
 const filteredGroups = groups.filter(group => Object.entries(groupFilters).every(([key,value]) => !value.trim() || (key==='name'?group.name:key==='code'?group.code:key==='scope'?(group.tenantId===null?'Toàn hệ thống':'Đơn vị'):key==='status'?(group.isActive?'Hoạt động':'Ngừng'):'').toLocaleLowerCase('vi').includes(value.trim().toLocaleLowerCase('vi'))))
 const filteredItems = visibleItems.filter(item => Object.entries(itemFilters).every(([key,value]) => !value.trim() || (key==='name'?item.name:key==='code'?item.code:key==='status'?(item.isActive?'Hoạt động':'Ngừng'):'').toLocaleLowerCase('vi').includes(value.trim().toLocaleLowerCase('vi'))))
 const pagedGroups = filteredGroups.slice((groupPage-1)*groupPageSize, groupPage*groupPageSize)
 const pagedItems = filteredItems.slice((itemPage-1)*itemPageSize, itemPage*itemPageSize)
 const groupedGroups = groupGrid.groupBy ? pagedGroups.reduce<Record<string,CategoryGroup[]>>((result,group)=>{const label=groupGrid.groupBy==='status'?(group.isActive?'Hoạt động':'Ngừng'):groupGrid.groupBy==='scope'?(group.tenantId===null?'Toàn hệ thống':'Đơn vị'):groupGrid.groupBy==='code'?group.code:group.name;(result[label]??=[]).push(group);return result},{}) : null
 const groupedItems = itemGrid.groupBy ? pagedItems.reduce<Record<string,CategoryItem[]>>((result,item)=>{const label=itemGrid.groupBy==='status'?(item.isActive?'Hoạt động':'Ngừng'):itemGrid.groupBy==='code'?item.code:item.name;(result[label]??=[]).push(item);return result},{}) : null
 const renderGroup = (group: CategoryGroup) => <tr key={group.id} className={group.id===selected?'selected':''} onClick={()=>{setSelected(group.id);newItem()}}>{groupGrid.visibleColumns.map(column=><td key={column.key} data-grid-column={column.key}>{column.key==='name'?group.name:column.key==='code'?group.code:column.key==='scope'?(group.tenantId===null?'Toàn hệ thống':tenantName):column.key==='status'?(group.isActive?'Hoạt động':'Ngừng'):group.tenantId===null&&!superAdmin?'Chỉ xem':<button className="category-text-button" type="button" onClick={event=>{event.stopPropagation();editGroup(group)}}>Sửa</button>}</td>)}</tr>
 const renderItem = (item: CategoryItem) => <tr key={item.id} className={!item.isActive?'inactive':''}>{itemGrid.visibleColumns.map(column=><td key={column.key} data-grid-column={column.key}>{column.key==='name'?item.name:column.key==='code'?item.code:column.key==='status'?(item.isActive?'Hoạt động':'Ngừng'):<button className="category-text-button" type="button" onClick={()=>editItem(item)}>Sửa</button>}</td>)}</tr>
  return <section className="category-maintenance" aria-label="Quản lý danh mục">
  <ResizableSplit className="category-panels" label="danh sách nhóm và giá trị danh mục" storageKey="categories" stackAt={900} verticalMinHeight={540}>
   <section className="category-panel" aria-labelledby="category-groups-title">
    <div className="category-panel-heading"><h2 id="category-groups-title">Danh mục chuẩn và danh mục đơn vị <span>{groups.length}</span></h2><button type="button" disabled={!scopeReady} onClick={newGroup}>Thêm nhóm</button></div>
    <div className="category-list-scroll" onContextMenu={groupGrid.onContextMenu}><table><thead><tr>{groupGrid.visibleColumns.map(column=><th key={column.key} data-grid-column={column.key}>{column.title}</th>)}</tr><GridFilterRow columns={groupGrid.visibleColumns} filters={groupFilters} onChange={(key,value)=>{setGroupFilters(old=>({...old,[key]:value}));setGroupPage(1)}}/></thead><tbody>{groupGrid.groupBy&&groupedGroups?Object.entries(groupedGroups).map(([label,rows])=><Fragment key={label}><tr className="data-grid-group-row"><td colSpan={groupGrid.visibleColumns.length}><button type="button" aria-expanded={!collapsedGroups.includes(label)} onClick={()=>setCollapsedGroups(old=>old.includes(label)?old.filter(x=>x!==label):[...old,label])}>{collapsedGroups.includes(label)?'＋':'−'} {groupGrid.groupBy==='status'?'Trạng thái':groupGrid.groupBy==='code'?'Mã':'Tên nhóm'}: {label} ({rows.length})</button></td></tr>{!collapsedGroups.includes(label)&&rows.map(renderGroup)}</Fragment>):pagedGroups.map(renderGroup)}</tbody></table>{groupsLoading&&<p className="category-empty" role="status">Đang tải nhóm danh mục…</p>}{!groupsLoading&&filteredGroups.length===0&&<p className="category-empty">{groups.length?'Không có nhóm phù hợp.':'Chưa có nhóm danh mục. Tạo nhóm đầu tiên để khai báo các giá trị.'}</p>}</div>
    {groupGrid.contextMenu}
    <div className="data-grid-footer category-grid-footer"><GridFilterSummary filters={groupFilters} columns={groupColumns} onRemove={key=>setGroupFilters(old=>({...old,[key]:''}))} onClear={()=>setGroupFilters({})}/><GridPager page={groupPage} pageSize={groupPageSize} total={filteredGroups.length} onPage={setGroupPage} onPageSize={size=>{setGroupPageSize(size);setGroupPage(1)}}/></div>
    <form className="category-form" onSubmit={saveGroup}>
     <h3>{groupEditing?'Sửa nhóm danh mục':'Tạo nhóm danh mục'}</h3>
     <label>Tên nhóm<span className="required-mark" aria-hidden="true"> *</span><input value={groupName} maxLength={120} required disabled={busy} onChange={e=>setGroupName(e.target.value)}/></label>
     {groupEditing?<label>Phạm vi<input readOnly value={groupEditing.tenantId===null?'Danh mục chuẩn · toàn hệ thống':`Đơn vị · ${tenantName}`}/></label>:superAdmin?<label className="category-checkbox"><input type="checkbox" checked={groupGlobal} disabled={busy||!scopeReady} onChange={e=>setGroupGlobal(e.target.checked)}/> Danh mục chuẩn dùng chung toàn hệ thống</label>:<p className="category-form-note">Phạm vi: đơn vị hiện tại · {tenantName}</p>}
     {groupEditing&&<label>Mã hệ thống cấp<input readOnly value={groupEditing.code}/></label>}
     <label>Mô tả<input value={groupDescription} maxLength={500} disabled={busy} onChange={e=>setGroupDescription(e.target.value)}/></label>
     {groupEditing&&<label className="category-checkbox"><input type="checkbox" checked={groupEditing.isActive} disabled={busy} onChange={e=>setGroupEditing({...groupEditing,isActive:e.target.checked})}/> Đang hoạt động</label>}
     <div className="category-actions">{groupEditing&&<button type="button" className="category-secondary" disabled={busy} onClick={newGroup}>Hủy</button>}<button disabled={busy||!scopeReady}>{busy?'Đang lưu…':groupEditing?'Lưu nhóm':'Tạo nhóm'}</button></div>
    </form>
   </section>
   <section className="category-panel" aria-labelledby="category-items-title">
    <div className="category-panel-heading"><h2 id="category-items-title">Giá trị danh mục <span>{visibleItems.length}</span></h2><button type="button" disabled={!selected||!selectedGroup?.isActive||!canEditSelected||!scopeReady} onClick={newItem}>Thêm giá trị</button></div>
    <p className="category-selected">{groups.find(x=>x.id===selected)?.name ?? 'Chọn một nhóm danh mục bên trái'}</p>
    <div className="category-list-scroll" onContextMenu={itemGrid.onContextMenu}><table><thead><tr>{itemGrid.visibleColumns.map(column=><th key={column.key} data-grid-column={column.key}>{column.title}</th>)}</tr><GridFilterRow columns={itemGrid.visibleColumns} filters={itemFilters} onChange={(key,value)=>{setItemFilters(old=>({...old,[key]:value}));setItemPage(1)}}/></thead><tbody>{itemGrid.groupBy&&groupedItems?Object.entries(groupedItems).map(([label,rows])=><Fragment key={label}><tr className="data-grid-group-row"><td colSpan={itemGrid.visibleColumns.length}><button type="button" aria-expanded={!collapsedItems.includes(label)} onClick={()=>setCollapsedItems(old=>old.includes(label)?old.filter(x=>x!==label):[...old,label])}>{collapsedItems.includes(label)?'＋':'−'} {itemGrid.groupBy==='status'?'Trạng thái':itemGrid.groupBy==='code'?'Mã':'Tên giá trị'}: {label} ({rows.length})</button></td></tr>{!collapsedItems.includes(label)&&rows.map(renderItem)}</Fragment>):pagedItems.map(renderItem)}</tbody></table>{itemsLoading&&<p className="category-empty" role="status">Đang tải giá trị…</p>}{selected&&!itemsLoading&&filteredItems.length===0&&<p className="category-empty">{visibleItems.length?'Không có giá trị phù hợp.':'Nhóm này chưa có giá trị.'}</p>}</div>
    {itemGrid.contextMenu}
    <div className="data-grid-footer category-grid-footer"><GridFilterSummary filters={itemFilters} columns={itemColumns} onRemove={key=>setItemFilters(old=>({...old,[key]:''}))} onClear={()=>setItemFilters({})}/><GridPager page={itemPage} pageSize={itemPageSize} total={filteredItems.length} onPage={setItemPage} onPageSize={size=>{setItemPageSize(size);setItemPage(1)}}/></div>
    {selected&&canEditSelected&&<form className="category-form" onSubmit={saveItem}>
     <h3>{itemEditing?'Sửa giá trị':'Thêm giá trị'}</h3>
     <label>Tên giá trị<span className="required-mark" aria-hidden="true"> *</span><input value={itemName} maxLength={120} required disabled={busy} onChange={e=>setItemName(e.target.value)}/></label>
     {itemEditing&&<label>Mã hệ thống cấp<input readOnly value={itemEditing.code}/></label>}
     {itemEditing&&<label className="category-checkbox"><input type="checkbox" checked={itemEditing.isActive} disabled={busy} onChange={e=>setItemEditing({...itemEditing,isActive:e.target.checked})}/> Đang hoạt động</label>}
     <div className="category-actions">{itemEditing&&<button type="button" className="category-secondary" disabled={busy} onClick={newItem}>Hủy</button>}<button disabled={busy||!scopeReady}>{busy?'Đang lưu…':itemEditing?'Lưu giá trị':'Thêm giá trị'}</button></div>
    </form>}
    {selected&&!canEditSelected&&<p className="category-form-note">Đây là danh mục chuẩn toàn hệ thống. Chỉ Super Admin được chỉnh sửa.</p>}
   </section>
  </ResizableSplit>
  {message&&<p role="alert" className="form-error">{message}</p>}
 </section>
}
