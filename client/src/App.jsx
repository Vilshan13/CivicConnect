import { useCallback, useEffect, useRef, useState } from 'react'
import './App.css'
import AuthPanel from './AuthPanel'
import LocationPicker from './LocationPicker'


const emptyForm = {
  title: '',
  description: '',
  category: 'Road damage',
  areaName: '',
  latitude: '',
  longitude: '',
}

function App() {
  const [reports, setReports] = useState([])
  const [form, setForm] = useState(emptyForm)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [session, setSession] = useState(null)
  const [photoFile, setPhotoFile] = useState(null)
  const [photoPreview, setPhotoPreview] = useState('')
  const photoInputRef = useRef(null)

  const loadReports = useCallback(async () => {
  if (!session?.accessToken) return

  const response = await fetch('/api/reports', {
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
    },
  })

  if (!response.ok) throw new Error('Could not load reports.')
  setReports(await response.json())
}, [session?.accessToken])

 useEffect(() => {
  if (!session?.accessToken) return

  setLoading(true)
  loadReports()
    .catch(() => setError('Could not connect to CivicConnect. Check that the API is running.'))
    .finally(() => setLoading(false))
}, [loadReports, session?.accessToken])

  useEffect(() => {
    if (!photoFile) {
      setPhotoPreview('')
      return undefined
    }

    const previewUrl = URL.createObjectURL(photoFile)
    setPhotoPreview(previewUrl)
    return () => URL.revokeObjectURL(previewUrl)
  }, [photoFile])

  function updateField(event) {
    const { name, value } = event.target
    setForm((current) => ({ ...current, [name]: value }))
  }

  function handlePhotoChange(event) {
    const selectedFile = event.target.files?.[0] ?? null

    if (!selectedFile) {
      setPhotoFile(null)
      return
    }

    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp']
    if (!allowedTypes.includes(selectedFile.type)) {
      setPhotoFile(null)
      event.target.value = ''
      setError('Choose a JPG, PNG, or WebP image.')
      return
    }

    if (selectedFile.size > 5 * 1024 * 1024) {
      setPhotoFile(null)
      event.target.value = ''
      setError('The image must be 5 MB or smaller.')
      return
    }

    setError('')
    setPhotoFile(selectedFile)
  }

  async function submitReport(event) {
    event.preventDefault()
    setSaving(true)
    setMessage('')
    setError('')

    const report = {
      ...form,
      latitude: Number(form.latitude),
      longitude: Number(form.longitude),
    }

    try {
      const headers = {
        Authorization: `Bearer ${session.accessToken}`,
      }
      let requestBody = JSON.stringify(report)
      let requestUrl = '/api/reports'

      if (photoFile) {
        const formData = new FormData()
        Object.entries(report).forEach(([key, value]) => {
          formData.append(key, String(value))
        })
        formData.append('photo', photoFile)
        requestBody = formData
        requestUrl = '/api/reports/with-photo'
      } else {
        headers['Content-Type'] = 'application/json'
      }

      const response = await fetch(requestUrl, {
        method: 'POST',
        headers,
        body: requestBody,
      })

      if (!response.ok) {
        const responseData = await response.json().catch(() => null)
        throw new Error(responseData?.message || 'Could not submit this report. Please check the details.')
      }

      const createdReport = await response.json()
      setForm(emptyForm)
      setPhotoFile(null)
      if (photoInputRef.current) photoInputRef.current.value = ''
      setMessage(`Report #${createdReport.id} submitted successfully.`)
      await loadReports()
    } catch (submitError) {
      setError(submitError.message || 'Something went wrong. Please try again.')
    } finally {
      setSaving(false)
    }
  }

  if (!session) {
  return (
    <div className="app-shell">
      <header className="site-header">
        <a className="brand" href="#top" aria-label="CivicConnect home">
          <span className="brand-mark">C</span>
          <span>Civic<span className="brand-light">Connect</span></span>
        </a>
        <span className="header-note">
          <span className="live-dot" /> Colombo District pilot
        </span>
      </header>

      <main id="top" style={{ maxWidth: 480, paddingTop: 48 }}>
        <section className="intro" style={{ padding: '16px 0 24px' }}>
          <p className="eyebrow">Your community, connected</p>
          <h1>Welcome to<br /><span>CivicConnect.</span></h1>
          <p className="intro-copy">
            Sign in or create a citizen account to continue.
          </p>
        </section>

        <AuthPanel onAuthenticated={setSession} />
      </main>

      <footer className="site-footer">
        <span>CivicConnect</span>
        <span>Local issues, visible progress.</span>
      </footer>
    </div>
  )
}

  return (
    <div className="app-shell">
      <header className="site-header">
        <a className="brand" href="#top" aria-label="CivicConnect home">
          <span className="brand-mark">C</span>
          <span>Civic<span className="brand-light">Connect</span></span>
        </a>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
  <span className="header-note">
    <span className="live-dot" /> Signed in: {session.email}
  </span>
  <button type="button" onClick={() => setSession(null)}>
    Sign out
  </button>
</div>
      </header>

      <main id="top">
        <section className="intro">
          <p className="eyebrow">A better neighbourhood starts with a report</p>
          <h1>See an issue?<br /><span>Let’s get it moving.</span></h1>
          <p className="intro-copy">Report a local problem and follow its progress in one place. Share what happened and where it needs attention.</p>
        </section>

        <div className="workspace-grid">
          <section className="panel form-panel" aria-labelledby="form-title">
            <div className="panel-heading">
              <div>
                <p className="eyebrow">Community report</p>
                <h2 id="form-title">Tell us what’s happening</h2>
              </div>
              <span className="step-badge">01 <span>/ 01</span></span>
            </div>

            {message && <div className="notice success" role="status">{message}</div>}
            {error && <div className="notice error" role="alert">{error}</div>}

            <form onSubmit={submitReport}>
              <label>
                Issue title
                <input name="title" value={form.title} onChange={updateField} placeholder="e.g. Damaged road near the junction" maxLength="120" required />
              </label>

              <div className="field-row">
                <label>
                  Category
                  <select name="category" value={form.category} onChange={updateField}>
                    <option>Road damage</option>
                    <option>Waste</option>
                    <option>Blocked drain</option>
                    <option>Streetlight</option>
                    <option>Other</option>
                  </select>
                </label>
                <label>
                  Area
                  <input name="areaName" value={form.areaName} onChange={updateField} placeholder="e.g. Colombo 05" maxLength="100" required />
                </label>
              </div>

              <label>
                Description
                <textarea name="description" value={form.description} onChange={updateField} placeholder="Add a few details to help the team understand the issue…" maxLength="2000" rows="4" required />
                <span className="field-hint">Include useful details such as nearby landmarks.</span>
              </label>

              <label className="photo-field">
                Add a photo (optional)
                <input
                  ref={photoInputRef}
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={handlePhotoChange}
                />
                <span className="field-hint">JPG, PNG, or WebP · up to 5 MB.</span>
                {photoPreview && (
                  <img className="photo-preview" src={photoPreview} alt="Selected report" />
                )}
              </label>

              <div className="location-heading">
                <span className="location-icon" aria-hidden="true">⌖</span>
                <div>
                  <strong>Pinpoint the location</strong>
                  <span>Click the map, or enter coordinates below.</span>
                </div>
              </div>

              <LocationPicker
                latitude={form.latitude}
                longitude={form.longitude}
                onLocationSelect={(latitude, longitude) =>
                  setForm((current) => ({
                    ...current,
                    latitude: latitude.toFixed(6),
                    longitude: longitude.toFixed(6),
                  }))
                }
              />

              <div className="field-row coordinates">
                <label>
                  Latitude
                  <input name="latitude" type="number" step="any" min="-90" max="90" value={form.latitude} onChange={updateField} placeholder="6.9271" required />
                </label>
                <label>
                  Longitude
                  <input name="longitude" type="number" step="any" min="-180" max="180" value={form.longitude} onChange={updateField} placeholder="79.8612" required />
                </label>
              </div>

              <button className="submit-button" type="submit" disabled={saving}>
                {saving ? 'Submitting…' : 'Submit report'}
                <span aria-hidden="true">↗</span>
              </button>
              <p className="privacy-note">Your report will be saved with a reference number and status.</p>
            </form>
          </section>

          <section className="panel reports-panel" aria-labelledby="reports-title">
            <div className="panel-heading reports-heading">
              <div>
                <p className="eyebrow">Keeping things moving</p>
                <h2 id="reports-title">Recent reports</h2>
              </div>
              <span className="report-count">{reports.length} total</span>
            </div>

            {loading ? (
              <p className="empty-state">Loading reports…</p>
            ) : reports.length === 0 ? (
              <div className="empty-state">
                <span className="empty-icon" aria-hidden="true">＋</span>
                <strong>No reports yet</strong>
                <span>Submitted reports will show up here.</span>
              </div>
            ) : (
              <div className="report-list">
                {reports.map((report) => (
                  <article className="report-card" key={report.id}>
                    <div className="report-card-top">
                      <span className="category-tag">{report.category}</span>
                      <span className={`status-tag status-${report.status.toLowerCase().replaceAll(' ', '-')}`}>{report.status}</span>
                    </div>
                    {report.photoPath && (
                      <img className="report-photo" src={report.photoPath} alt={`Photo for ${report.title}`} loading="lazy" />
                    )}
                    <h3>{report.title}</h3>
                    <p className="report-description">{report.description}</p>
                    <div className="report-meta">
                      <span><span aria-hidden="true">⌖</span> {report.areaName}</span>
                      <span>#{report.id} · {new Date(report.createdAtUtc).toLocaleDateString('en-LK', { day: 'numeric', month: 'short' })}</span>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </section>
        </div>
      </main>

      <footer className="site-footer"><span>CivicConnect</span><span>Local issues, visible progress.</span></footer>
    </div>
  )
}

export default App
