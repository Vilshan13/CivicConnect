import { useCallback, useEffect, useState } from 'react';
import { CircleMarker, MapContainer, TileLayer, useMapEvents } from 'react-leaflet';

const API = import.meta.env.VITE_API_URL || 'http://localhost:5080';
const COLOMBO = [6.9271, 79.8612];
const STATUSES = ['Under review', 'In progress', 'Resolved', 'Rejected'];

async function apiRequest(path, options = {}, token = sessionStorage.getItem('civicconnect-token')) {
  const headers = new Headers(options.headers || {});
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json');
  const response = await fetch(`${API}${path}`, { ...options, headers });
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) throw new Error(data?.message || (response.status === 401 ? 'Please sign in again.' : 'The request could not be completed.'));
  return data;
}

function LocationPicker({ value, onChange }) {
  useMapEvents({ click(event) { onChange([event.latlng.lat, event.latlng.lng]); } });
  return value ? <CircleMarker center={value} radius={9} pathOptions={{ color: '#16745a', fillColor: '#38a883', fillOpacity: 0.9 }} /> : null;
}

function formatDate(value) {
  return new Intl.DateTimeFormat('en-LK', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

export default function App() {
  const [session, setSession] = useState(() => {
    const token = sessionStorage.getItem('civicconnect-token');
    const user = sessionStorage.getItem('civicconnect-user');
    return token && user ? { token, user: JSON.parse(user) } : null;
  });
  const [categories, setCategories] = useState([]);
  const [areas, setAreas] = useState([]);
  const [reports, setReports] = useState([]);
  const [departments, setDepartments] = useState([]);
  const [officers, setOfficers] = useState([]);
  const [notifications, setNotifications] = useState([]);
  const [location, setLocation] = useState(null);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [authMode, setAuthMode] = useState('login');

  const roles = session?.user?.roles || [];
  const isOfficer = roles.includes('Officer');
  const isAdmin = roles.includes('Administrator');
  const isCitizen = !isOfficer && !isAdmin;

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [categoryData, areaData] = await Promise.all([
        apiRequest('/api/categories', {}, null),
        apiRequest('/api/areas', {}, null),
      ]);
      setCategories(categoryData);
      setAreas(areaData);
      if (session?.token) {
        const reportData = await apiRequest('/api/reports', {}, session.token);
        setReports(reportData);
        setError('');
      } else {
        setReports([]);
        setError('');
      }
    } catch (e) {
      setError(e.message);
      if (e.message === 'Please sign in again.') signOut();
    } finally {
      setLoading(false);
    }
  }, [session?.token]);

  const loadAdminData = useCallback(async () => {
    if (!session?.token || !isAdmin) return;
    try {
      const [departmentData, officerData] = await Promise.all([
        apiRequest('/api/admin/departments', {}, session.token),
        apiRequest('/api/admin/officers', {}, session.token),
      ]);
      setDepartments(departmentData);
      setOfficers(officerData);
    } catch (e) { setError(e.message); }
  }, [session?.token, isAdmin]);

  const loadNotifications = useCallback(async () => {
    if (!session?.token) return;
    try { setNotifications(await apiRequest('/api/notifications', {}, session.token)); }
    catch (e) { setError(e.message); }
  }, [session?.token]);

  useEffect(() => { loadData(); }, [loadData]);
  useEffect(() => { loadAdminData(); loadNotifications(); }, [loadAdminData, loadNotifications]);

  function signOut() {
    sessionStorage.removeItem('civicconnect-token');
    sessionStorage.removeItem('civicconnect-user');
    setSession(null);
    setReports([]);
    setNotifications([]);
    setNotice('You have signed out.');
  }

  async function authenticate(event) {
    event.preventDefault();
    setError(''); setNotice(''); setBusy(true);
    const form = new FormData(event.currentTarget);
    const payload = Object.fromEntries(form.entries());
    try {
      const result = await apiRequest(`/api/auth/${authMode === 'register' ? 'register' : 'login'}`, {
        method: 'POST', body: JSON.stringify(payload),
      }, null);
      sessionStorage.setItem('civicconnect-token', result.accessToken);
      sessionStorage.setItem('civicconnect-user', JSON.stringify(result.user));
      setSession({ token: result.accessToken, user: result.user });
      setNotice(authMode === 'register' ? 'Your account is ready. You can now report a community issue.' : `Welcome back, ${result.user.displayName}.`);
    } catch (e) { setError(e.message); }
    finally { setBusy(false); }
  }

  async function useMyLocation() {
    setError('');
    if (!navigator.geolocation) {
      setError('Location is not available in this browser. Click the map to place a pin.');
      return;
    }
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => setLocation([coords.latitude, coords.longitude]),
      () => setError('We could not access your location. Click the map to place a pin instead.'),
      { enableHighAccuracy: true, timeout: 10000 },
    );
  }

  async function submitReport(event) {
    event.preventDefault(); setNotice(''); setError('');
    if (!location) { setError('Choose the issue location on the map or use your current location.'); return; }
    const form = new FormData(event.currentTarget);
    form.set('latitude', String(location[0])); form.set('longitude', String(location[1]));
    setBusy(true);
    try {
      const data = await apiRequest('/api/reports', { method: 'POST', body: form }, session.token);
      setNotice(`Report submitted. Reference: ${data.referenceNumber}`);
      event.currentTarget.reset(); setLocation(null);
      await loadData();
    } catch (e) { setError(e.message); }
    finally { setBusy(false); }
  }

  async function updateStatus(reportId, form) {
    setError(''); setNotice(''); setBusy(true);
    try {
      const values = new FormData(form);
      await apiRequest(`/api/reports/${reportId}/status`, { method: 'PUT', body: JSON.stringify({ status: values.get('status'), note: values.get('note') }) }, session.token);
      setNotice('Report status updated and the citizen was notified.');
      await Promise.all([loadData(), loadNotifications()]);
    } catch (e) { setError(e.message); }
    finally { setBusy(false); }
  }

  async function assignReport(reportId, form) {
    setError(''); setNotice(''); setBusy(true);
    try {
      const values = form instanceof HTMLFormElement ? Object.fromEntries(new FormData(form).entries()) : form;
      await apiRequest(`/api/reports/${reportId}/assignment`, { method: 'PUT', body: JSON.stringify({ officerUserId: values.officerUserId, departmentId: Number(values.departmentId), note: values.note || '' }) }, session.token);
      setNotice('Report assigned. The citizen was notified.');
      await Promise.all([loadData(), loadNotifications()]);
    } catch (e) { setError(e.message); }
    finally { setBusy(false); }
  }

  async function adminAction(path, method, payload, successMessage) {
    setError(''); setNotice(''); setBusy(true);
    try {
      const options = { method };
      if (payload !== undefined) options.body = JSON.stringify(payload);
      await apiRequest(path, options, session.token);
      setNotice(successMessage);
      await Promise.all([loadData(), loadAdminData()]);
    } catch (e) { setError(e.message); }
    finally { setBusy(false); }
  }

  async function markNotificationRead(notificationId) {
    try {
      await apiRequest(`/api/notifications/${notificationId}/read`, { method: 'PUT' }, session.token);
      setNotifications(items => items.map(item => item.id === notificationId ? { ...item, readAt: new Date().toISOString() } : item));
    } catch (e) { setError(e.message); }
  }

  const unreadCount = notifications.filter(item => !item.readAt).length;

  return (
    <div className="page-shell">
      <header className="topbar">
        <a className="brand" href="#top" aria-label="CivicConnect home"><span className="brand-mark">C</span><span>Civic<span className="brand-light">Connect</span></span></a>
        <div className="topbar-actions">
          <span className="pilot-tag"><span className="live-dot" /> COLOMBO PILOT</span>
          {session && <>
            <details className="notification-menu">
              <summary aria-label={`Notifications, ${unreadCount} unread`}>♧ {unreadCount > 0 && <span className="notification-count">{unreadCount}</span>}</summary>
              <div className="notification-popover"><strong>Notifications</strong>
                {notifications.length === 0 ? <p>You’re all caught up.</p> : notifications.map(item => <button key={item.id} type="button" className={item.readAt ? 'notification-read' : ''} onClick={() => markNotificationRead(item.id)}><span>{item.message}</span><small>{formatDate(item.createdAt)}</small></button>)}
              </div>
            </details>
            <span className="signed-in-as">{session.user.displayName}</span>
            <button className="sign-out" onClick={signOut}>Sign out</button>
          </>}
        </div>
      </header>

      <main id="top">
        <section className="intro">
          <p className="eyebrow">A better neighbourhood starts here</p>
          <h1>{isOfficer ? <>Community issues,<br /><em>ready for action.</em></> : isAdmin ? <>Tools for a<br /><em>connected community.</em></> : <>See something?<br /><em>Let’s sort it together.</em></>}</h1>
          <p className="intro-copy">{isOfficer ? 'Review community reports, coordinate the right team, and keep residents updated.' : isAdmin ? 'Set up departments, officer accounts, and issue categories for your pilot area.' : 'Report a community issue and help the right people find it. Add a location, tell us what happened, and follow reports from your area.'}</p>
          <div className="intro-stats"><span><strong>{reports.length}</strong> {isOfficer || isAdmin ? 'reports to review' : 'your reports'}</span><span className="stat-divider" /><span><strong>{categories.length}</strong> issue categories</span></div>
        </section>

        {notice && <div className="notice success" role="status"><span>✓</span>{notice}</div>}
        {error && <div className="notice error" role="alert"><span>!</span>{error}<button type="button" onClick={loadData}>Retry</button></div>}

        {!session ? <section className="auth-card">
          <div className="section-heading"><span className="step-number">01</span><div><p className="eyebrow">Your community account</p><h2>{authMode === 'login' ? 'Welcome back' : 'Join CivicConnect'}</h2></div></div>
          <form onSubmit={authenticate}>
            {authMode === 'register' && <><label htmlFor="displayName">Your name <span className="required">*</span></label><input id="displayName" name="displayName" autoComplete="name" maxLength="100" required /></>}
            <label htmlFor="email">Email address <span className="required">*</span></label><input id="email" name="email" type="email" autoComplete="email" maxLength="254" required />
            <label htmlFor="password">Password <span className="required">*</span></label><input id="password" name="password" type="password" autoComplete={authMode === 'login' ? 'current-password' : 'new-password'} minLength="10" required />
            {authMode === 'register' && <p className="field-hint">Use at least 10 characters, including an uppercase letter and a number.</p>}
            <button className="submit-button" type="submit" disabled={busy}>{busy ? 'Please wait…' : authMode === 'login' ? 'Sign in' : 'Create account'}</button>
          </form>
          <p className="auth-switch">{authMode === 'login' ? 'New to CivicConnect?' : 'Already have an account?'} <button type="button" onClick={() => { setAuthMode(authMode === 'login' ? 'register' : 'login'); setError(''); }}>{authMode === 'login' ? 'Create an account' : 'Sign in'}</button></p>
        </section> : <>
          {isCitizen && <section className="content-grid" aria-label="Report an issue and view your reports">
            <form className="report-card" onSubmit={submitReport}>
              <div className="section-heading"><span className="step-number">01</span><div><p className="eyebrow">Tell us what’s happening</p><h2>Make a report</h2></div></div>
              <label htmlFor="pilotAreaId">Pilot area <span className="required">*</span></label>
              <select id="pilotAreaId" name="pilotAreaId" required defaultValue=""><option value="" disabled>{areas.length ? 'Choose an area' : 'Pilot area not configured yet'}</option>{areas.map(area => <option key={area.id} value={area.id}>{area.name} · {area.district}</option>)}</select>
              <label htmlFor="categoryId">Issue type <span className="required">*</span></label>
              <select id="categoryId" name="categoryId" required defaultValue=""><option value="" disabled>Choose a category</option>{categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}</select>
              <label htmlFor="description">What did you notice? <span className="required">*</span></label>
              <textarea id="description" name="description" rows="4" maxLength="2000" placeholder="Share a few details that could help someone find and understand the issue…" required />
              <div className="field-hint">Please don’t include personal or sensitive information.</div>
              <div className="label-row"><label htmlFor="photo">Photo <span className="optional">OPTIONAL</span></label><span className="field-hint">JPG, PNG or WebP · up to 5 MB</span></div>
              <input id="photo" name="photo" type="file" accept="image/jpeg,image/png,image/webp" />
              <div className="label-row location-label"><label>Where is it? <span className="required">*</span></label><button type="button" className="text-button" onClick={useMyLocation}>◎ Use my location</button></div>
              <p className="field-hint map-hint">Click on the map to place a pin. Your exact pin is attached to the report.</p>
              <div className="map-frame"><MapContainer center={COLOMBO} zoom={13} scrollWheelZoom={false}><TileLayer attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" /><LocationPicker value={location} onChange={setLocation} /></MapContainer>{!location && <span className="map-prompt">Click the map to choose a location</span>}{location && <span className="location-chip">✓ Location selected</span>}</div>
              {location && <p className="coordinates">{location[0].toFixed(5)}, {location[1].toFixed(5)}</p>}
              <button className="submit-button" type="submit" disabled={busy || categories.length === 0 || areas.length === 0}>{busy ? 'Submitting…' : <>Submit report <span aria-hidden="true">↗</span></>}</button>
              <p className="privacy-note">Only your account and authorized CivicConnect staff can view this report.</p>
            </form>
            <ReportsPanel reports={reports} loading={loading} isOfficer={false} accessToken={session.token} />
          </section>}

          {(isOfficer || isAdmin) && <section className="content-grid staff-grid">
            <ReportsPanel reports={reports} loading={loading} isOfficer={isOfficer} isAdmin={isAdmin} currentUser={session.user} accessToken={session.token} onStatusUpdate={updateStatus} onAssign={assignReport} departments={departments} officers={officers} busy={busy} />
            {isAdmin && <AdminPanel departments={departments} categories={categories} areas={areas} busy={busy} onAction={adminAction} />}
          </section>}
        </>}
      </main>
      <footer><span>© 2026 CivicConnect</span><span>Made for stronger communities <span className="heart">♥</span></span></footer>
    </div>
  );
}

function ReportsPanel({ reports, loading, isOfficer, isAdmin = false, currentUser, accessToken, onStatusUpdate, onAssign, departments, officers, busy }) {
  return <section className="reports-panel" aria-labelledby="reports-title">
    <div className="section-heading reports-heading"><span className="step-number">02</span><div><p className="eyebrow">{isOfficer || isAdmin ? 'Review and keep residents informed' : 'Your community, in view'}</p><h2 id="reports-title">{isOfficer || isAdmin ? 'Issue queue' : 'Your reports'} <span className="count-pill">{reports.length}</span></h2></div></div>
    {loading ? <div className="empty-state">Loading reports…</div> : reports.length === 0 ? <div className="empty-state"><div className="empty-icon">↗</div><h3>{isOfficer || isAdmin ? 'No reports need attention' : 'It starts with one report'}</h3><p>{isOfficer || isAdmin ? 'New reports in your pilot area will appear here.' : 'Your reports and their progress will appear here.'}</p></div> : <div className="report-list">
      {reports.map(report => <article className="report-item" key={report.id}>
        <div className="report-topline"><span className="category-dot" /><span className="report-category">{report.category}</span><span className={`status-pill status-${report.status.toLowerCase().replaceAll(' ', '-')}`}>{report.status}</span></div>
        <p className="report-description">{report.description}</p>
        {report.hasPhoto && <ReportPhoto reportId={report.id} token={accessToken} />}
        <div className="report-meta"><span>{report.referenceNumber}</span><time dateTime={report.createdAt}>{formatDate(report.createdAt)}</time></div>
        <div className="report-details"><span>{report.area} · {report.district}</span><span>{report.department || 'Not assigned'}</span>{report.assignedOfficer && <span>Officer: {report.assignedOfficer}</span>}</div>
        <a className="map-link" href={`https://www.openstreetmap.org/?mlat=${report.latitude}&mlon=${report.longitude}#map=17/${report.latitude}/${report.longitude}`} target="_blank" rel="noreferrer">View location ↗</a>
        {(isOfficer || isAdmin) && <div className="staff-actions">
          {!report.assignedOfficerId && !isAdmin && <button className="text-button" type="button" onClick={() => onAssign(report.id, { officerUserId: currentUser.id, departmentId: currentUser.departmentId, note: '' })} disabled={busy || !currentUser?.departmentId}>Take this report</button>}
          {(isAdmin || report.assignedOfficerId === currentUser?.id) && <form className="status-form" onSubmit={event => { event.preventDefault(); onStatusUpdate(report.id, event.currentTarget); }}>
            <label htmlFor={`status-${report.id}`}>Update status</label><div className="status-controls"><select id={`status-${report.id}`} name="status" defaultValue={STATUSES.includes(report.status) ? report.status : STATUSES[0]}>{STATUSES.map(status => <option key={status}>{status}</option>)}</select><input name="note" maxLength="500" placeholder="Optional note for the resident" /><button type="submit" disabled={busy}>Save</button></div>
          </form>}
          {isAdmin && <form className="assign-form" onSubmit={event => { event.preventDefault(); onAssign(report.id, event.currentTarget); }}>
            <label>Assign report</label><div className="status-controls"><select name="departmentId" defaultValue={report.departmentId || ''} required><option value="" disabled>Department</option>{departments.map(department => <option key={department.id} value={department.id}>{department.name}</option>)}</select><select name="officerUserId" defaultValue={report.assignedOfficerId || ''} required><option value="" disabled>Officer</option>{officers.map(officer => <option key={officer.id} value={officer.id}>{officer.displayName}</option>)}</select><input name="note" maxLength="500" placeholder="Optional note" /><button type="submit" disabled={busy}>Assign</button></div>
          </form>}
        </div>}
        {report.statusHistory?.length > 0 && <details className="history-details"><summary>Status history</summary><ol>{[...report.statusHistory].reverse().map((entry, index) => <li key={`${entry.createdAt}-${index}`}><strong>{entry.toStatus}</strong><time>{formatDate(entry.createdAt)}</time>{entry.note && <p>{entry.note}</p>}</li>)}</ol></details>}
      </article>)}
    </div>}
    {!isOfficer && !isAdmin && <div className="pilot-note"><span>i</span><p><strong>Pilot area</strong><br />This prototype is being developed for a selected community in Colombo District.</p></div>}
  </section>;
}

function ReportPhoto({ reportId, token }) {
  const [source, setSource] = useState('');
  useEffect(() => {
    let objectUrl = '';
    let active = true;
    fetch(`${API}/api/reports/${reportId}/photo`, { headers: { Authorization: `Bearer ${token}` } })
      .then(response => response.ok ? response.blob() : Promise.reject(new Error('Photo unavailable')))
      .then(blob => { objectUrl = URL.createObjectURL(blob); if (active) setSource(objectUrl); })
      .catch(() => { if (active) setSource('error'); });
    return () => { active = false; if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [reportId, token]);
  if (!source) return <div className="photo-loading">Loading photo…</div>;
  if (source === 'error') return null;
  return <img className="report-photo" src={source} alt="Photo attached to the community report" />;
}

function AdminPanel({ departments, categories, areas, busy, onAction }) {
  return <section className="admin-panel">
    <div className="section-heading"><span className="step-number">03</span><div><p className="eyebrow">Configure the pilot</p><h2>Administration</h2></div></div>
    <form className="admin-form" onSubmit={event => { event.preventDefault(); const values = new FormData(event.currentTarget); onAction('/api/admin/departments', 'POST', Object.fromEntries(values.entries()), 'Department added.'); event.currentTarget.reset(); }}>
      <h3>Add a department</h3><label>Department name</label><input name="name" maxLength="120" required /><label>Description <span className="optional">OPTIONAL</span></label><input name="description" maxLength="300" /><button type="submit" disabled={busy}>Add department</button>
    </form>
    <form className="admin-form" onSubmit={event => { event.preventDefault(); const values = new FormData(event.currentTarget); onAction('/api/admin/areas', 'POST', Object.fromEntries(values.entries()), 'Pilot area added.'); event.currentTarget.reset(); }}>
      <h3>Add a pilot area</h3><label>Area name</label><input name="name" maxLength="120" required placeholder="For example, a selected community" /><label>District</label><input name="district" maxLength="120" required defaultValue="Colombo District" /><button type="submit" disabled={busy}>Add pilot area</button>
    </form>
    <form className="admin-form" onSubmit={event => { event.preventDefault(); const values = new FormData(event.currentTarget); onAction('/api/admin/officers', 'POST', Object.fromEntries(values.entries()), 'Officer account created.'); event.currentTarget.reset(); }}>
      <h3>Create an officer account</h3><label>Officer name</label><input name="displayName" maxLength="100" required /><label>Email</label><input name="email" type="email" required /><label>Temporary password</label><input name="password" type="password" minLength="10" required /><label>Department</label><select name="departmentId" required defaultValue=""><option value="" disabled>Select department</option>{departments.map(department => <option key={department.id} value={department.id}>{department.name}</option>)}</select><button type="submit" disabled={busy || departments.length === 0}>Create officer</button>
    </form>
    <form className="admin-form" onSubmit={event => { event.preventDefault(); const values = new FormData(event.currentTarget); onAction('/api/admin/categories', 'POST', Object.fromEntries(values.entries()), 'Issue category added.'); event.currentTarget.reset(); }}>
      <h3>Add an issue category</h3><label>Category name</label><input name="name" maxLength="80" required /><label>Description</label><input name="description" maxLength="300" /><button type="submit" disabled={busy}>Add category</button>
    </form>
    <div className="admin-inventory"><h3>Current setup</h3><p><strong>{areas.length}</strong> pilot areas <span>·</span> <strong>{departments.length}</strong> departments <span>·</span> <strong>{categories.length}</strong> issue categories</p>
      <ul>{areas.map(area => <li key={area.id}>{area.name} · {area.district}<button type="button" onClick={() => window.confirm(`Remove ${area.name}?`) && onAction(`/api/admin/areas/${area.id}`, 'DELETE', undefined, 'Pilot area removed.')}>Remove</button></li>)}</ul>
      <ul>{departments.map(department => <li key={department.id}>{department.name}<button type="button" onClick={() => window.confirm(`Remove ${department.name}?`) && onAction(`/api/admin/departments/${department.id}`, 'DELETE', undefined, 'Department removed.')}>Remove</button></li>)}</ul>
      <ul>{categories.map(category => <li key={category.id}>{category.name}<button type="button" onClick={() => window.confirm(`Remove ${category.name}?`) && onAction(`/api/admin/categories/${category.id}`, 'DELETE', undefined, 'Issue category removed.')}>Remove</button></li>)}</ul>
    </div>
  </section>;
}
