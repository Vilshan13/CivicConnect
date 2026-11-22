import { useCallback, useEffect, useState } from 'react';
import { CircleMarker, MapContainer, TileLayer, useMapEvents } from 'react-leaflet';

const API = import.meta.env.VITE_API_URL || 'http://localhost:5080';
const COLOMBO = [6.9271, 79.8612];

function LocationPicker({ value, onChange }) {
  useMapEvents({ click(event) { onChange([event.latlng.lat, event.latlng.lng]); } });
  return value ? <CircleMarker center={value} radius={9} pathOptions={{ color: '#16745a', fillColor: '#38a883', fillOpacity: 0.9 }} /> : null;
}

function formatDate(value) {
  return new Intl.DateTimeFormat('en-LK', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

export default function App() {
  const [categories, setCategories] = useState([]);
  const [reports, setReports] = useState([]);
  const [location, setLocation] = useState(null);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [categoryResponse, reportResponse] = await Promise.all([
        fetch(`${API}/api/categories`),
        fetch(`${API}/api/reports`),
      ]);
      if (!categoryResponse.ok || !reportResponse.ok) throw new Error('Could not load the community reports. Is the API running?');
      setCategories(await categoryResponse.json());
      setReports(await reportResponse.json());
      setError('');
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

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
    event.preventDefault();
    setNotice('');
    setError('');
    if (!location) {
      setError('Choose the issue location on the map or use your current location.');
      return;
    }
    const form = new FormData(event.currentTarget);
    form.set('latitude', String(location[0]));
    form.set('longitude', String(location[1]));
    setBusy(true);
    try {
      const response = await fetch(`${API}/api/reports`, { method: 'POST', body: form });
      const data = await response.json();
      if (!response.ok) throw new Error(data.message || 'The report could not be submitted.');
      setNotice(`Report submitted. Reference: ${data.referenceNumber}`);
      event.currentTarget.reset();
      setLocation(null);
      await loadData();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="page-shell">
      <header className="topbar">
        <a className="brand" href="#top" aria-label="CivicConnect home"><span className="brand-mark">C</span><span>Civic<span className="brand-light">Connect</span></span></a>
        <span className="pilot-tag"><span className="live-dot" /> COLOMBO PILOT</span>
      </header>

      <main id="top">
        <section className="intro">
          <p className="eyebrow">A better neighbourhood starts here</p>
          <h1>See something?<br /><em>Let’s sort it together.</em></h1>
          <p className="intro-copy">Report a community issue and help the right people find it. Add a location, tell us what happened, and follow reports from your area.</p>
          <div className="intro-stats"><span><strong>{reports.length}</strong> community reports</span><span className="stat-divider" /><span><strong>5</strong> issue categories</span></div>
        </section>

        {notice && <div className="notice success" role="status"><span>✓</span>{notice}</div>}
        {error && <div className="notice error" role="alert"><span>!</span>{error}<button type="button" onClick={loadData} aria-label="Retry">Retry</button></div>}

        <section className="content-grid" aria-label="Report an issue and view reports">
          <form className="report-card" onSubmit={submitReport}>
            <div className="section-heading"><span className="step-number">01</span><div><p className="eyebrow">Tell us what’s happening</p><h2>Make a report</h2></div></div>

            <label htmlFor="categoryId">Issue type <span className="required">*</span></label>
            <select id="categoryId" name="categoryId" required defaultValue="">
              <option value="" disabled>Choose a category</option>
              {categories.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}
            </select>

            <label htmlFor="description">What did you notice? <span className="required">*</span></label>
            <textarea id="description" name="description" rows="4" maxLength="2000" placeholder="Share a few details that could help someone find and understand the issue…" required />
            <div className="field-hint">Please don’t include personal or sensitive information.</div>

            <div className="label-row"><label htmlFor="photo">Photo <span className="optional">OPTIONAL</span></label><span className="field-hint">JPG, PNG or WebP · up to 5 MB</span></div>
            <input id="photo" name="photo" type="file" accept="image/jpeg,image/png,image/webp" />

            <div className="label-row location-label"><label>Where is it? <span className="required">*</span></label><button type="button" className="text-button" onClick={useMyLocation}>◎ Use my location</button></div>
            <p className="field-hint map-hint">Click on the map to place a pin. Your exact pin is attached to the report.</p>
            <div className="map-frame">
              <MapContainer center={COLOMBO} zoom={13} scrollWheelZoom={false}>
                <TileLayer attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
                <LocationPicker value={location} onChange={setLocation} />
              </MapContainer>
              {!location && <span className="map-prompt">Click the map to choose a location</span>}
              {location && <span className="location-chip">✓ Location selected</span>}
            </div>
            {location && <p className="coordinates">{location[0].toFixed(5)}, {location[1].toFixed(5)}</p>}

            <button className="submit-button" type="submit" disabled={busy || categories.length === 0}>
              {busy ? 'Submitting…' : <>Submit report <span aria-hidden="true">↗</span></>}
            </button>
            <p className="privacy-note">Reports are shared with the pilot team to help review community issues.</p>
          </form>

          <section className="reports-panel" aria-labelledby="reports-title">
            <div className="section-heading reports-heading"><span className="step-number">02</span><div><p className="eyebrow">Your community, in view</p><h2 id="reports-title">Recent reports <span className="count-pill">{reports.length}</span></h2></div></div>
            {loading ? <div className="empty-state">Loading reports…</div> : reports.length === 0 ? (
              <div className="empty-state"><div className="empty-icon">↗</div><h3>It starts with one report</h3><p>Issues reported in the pilot area will appear here.</p></div>
            ) : (
              <div className="report-list">
                {reports.map(report => (
                  <article className="report-item" key={report.id}>
                    <div className="report-topline"><span className="category-dot" /> <span className="report-category">{report.category}</span><span className="status-pill">{report.status}</span></div>
                    <p className="report-description">{report.description}</p>
                    {report.photoUrl && <img className="report-photo" src={`${API}${report.photoUrl}`} alt="Photo attached to the community report" />}
                    <div className="report-meta"><span>{report.referenceNumber}</span><time dateTime={report.createdAt}>{formatDate(report.createdAt)}</time></div>
                    <a className="map-link" href={`https://www.openstreetmap.org/?mlat=${report.latitude}&mlon=${report.longitude}#map=17/${report.latitude}/${report.longitude}`} target="_blank" rel="noreferrer">View location ↗</a>
                  </article>
                ))}
              </div>
            )}
            <div className="pilot-note"><span>i</span><p><strong>Pilot area</strong><br />This prototype is being developed for a selected community in Colombo District.</p></div>
          </section>
        </section>
      </main>

      <footer><span>© 2026 CivicConnect</span><span>Made for stronger communities <span className="heart">♥</span></span></footer>
    </div>
  );
}
