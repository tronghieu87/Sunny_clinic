import './App.css'
import AccessPanel from './AccessPanel'
import sunheartMark from './assets/sunheart-mark.svg'
export default function App() {
 return <div className="app-shell">
    <header><div className="brand"><img className="brand-mark" src={sunheartMark} alt=""/><div><strong>SUNHEART MEDICAL</strong><small>Family Health &amp; Wellness</small></div></div></header>
  <main><AccessPanel /></main>
    <footer>SUNHEART MEDICAL · Family Health &amp; Wellness</footer>
 </div>
}
