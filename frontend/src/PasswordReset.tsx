import { useState, type FormEvent } from 'react'
import { adminApi as api } from './adminApi'

export default function PasswordReset() {
 const params=new URLSearchParams(window.location.search || window.location.hash.split('?')[1] || '')
 const userId=params.get('userId')??''
 const token=params.get('token')??''
 const [busy,setBusy]=useState(false)
 const [error,setError]=useState('')
 const [complete,setComplete]=useState(false)

 async function submit(event:FormEvent<HTMLFormElement>) {
  event.preventDefault();const form=event.currentTarget;const data=new FormData(form)
  if(data.get('password')!==data.get('confirmPassword')) { setError('Mật khẩu xác nhận không khớp.');return }
  setBusy(true);setError('')
  try { await api('auth/reset-password','POST',{userId,token,password:data.get('password')});setComplete(true);form.reset() }
  catch(e) { setError(e instanceof Error?e.message:'Liên kết không hợp lệ hoặc đã hết hạn. Hãy yêu cầu gửi link mới.') }
  finally { setBusy(false) }
 }

 return <main className="password-reset-page"><section className="panel access-panel"><h1>Đặt lại mật khẩu</h1>
    {complete?<><p>Mật khẩu đã được cập nhật.</p><a className="password-reset-link" href="/home">Trở lại đăng nhập</a></>:!userId||!token?<><p>Liên kết đặt lại không hợp lệ hoặc bị thiếu.</p><a className="password-reset-link" href="/home">Trở lại đăng nhập</a></>:<form className="access-form" onSubmit={submit}>
   <label>Mật khẩu mới<span className="required-mark" aria-hidden="true"> *</span><input name="password" type="password" minLength={8} maxLength={128} autoComplete="new-password" required disabled={busy}/></label>
   <label>Xác nhận mật khẩu<span className="required-mark" aria-hidden="true"> *</span><input name="confirmPassword" type="password" minLength={8} maxLength={128} autoComplete="new-password" required disabled={busy}/></label>
   {error&&<p role="alert" className="form-error">{error}</p>}
   <button disabled={busy}>{busy?'Đang cập nhật…':'Lưu mật khẩu mới'}</button>
  </form>}
 </section></main>
}