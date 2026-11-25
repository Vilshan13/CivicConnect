import { useState } from 'react'

async function readResponse(response, fallbackMessage) {
  const data = await response.json().catch(() => null)

  if (!response.ok) {
    const message = Array.isArray(data)
      ? data.map((error) => error.description).join(' ')
      : data?.message ||
        Object.values(data?.errors ?? {}).flat().join(' ')

    throw new Error(message || fallbackMessage)
  }

  return data ?? {}
}

export default function AuthPanel({ onAuthenticated }) {
  const [mode, setMode] = useState('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function handleSubmit(event) {
    event.preventDefault()
    setBusy(true)
    setError('')

    try {
      const cleanEmail = email.trim()

      if (mode === 'register') {
        const registerResponse = await fetch('/api/auth/citizen-register', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email: cleanEmail, password }),
        })

        await readResponse(registerResponse, 'Could not create your account.')
      }

      const loginResponse = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: cleanEmail, password }),
      })

      const loginData = await readResponse(loginResponse, 'Login failed.')

      onAuthenticated({
        email: cleanEmail,
        accessToken: loginData.accessToken,
      })
    } catch (submitError) {
      setError(submitError.message || 'Something went wrong. Please try again.')
    } finally {
      setBusy(false)
    }
  }

  const registering = mode === 'register'

  return (
    <section className="panel form-panel">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">Citizen account</p>
          <h2>{registering ? 'Create your account' : 'Welcome back'}</h2>
        </div>
      </div>

      {error && <div className="notice error" role="alert">{error}</div>}

      <form onSubmit={handleSubmit}>
        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="you@example.com"
            autoComplete="email"
            required
          />
        </label>

        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete={registering ? 'new-password' : 'current-password'}
            minLength="6"
            required
          />
        </label>

        <button className="submit-button" type="submit" disabled={busy}>
          {busy
            ? 'Please wait…'
            : registering
              ? 'Create citizen account'
              : 'Log in'}
        </button>
      </form>

      <p>
        {registering ? 'Already have an account? ' : 'New to CivicConnect? '}
        <button
          type="button"
          onClick={() => {
            setMode(registering ? 'login' : 'register')
            setError('')
          }}
        >
          {registering ? 'Log in' : 'Register'}
        </button>
      </p>
    </section>
  )
}