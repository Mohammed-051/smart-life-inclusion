import { useEffect, useState, type FormEvent } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { signIn, signOut, watchAccess, watchAuth, type SupervisorAccess } from '../lib/services/auth';
import { watchEmergencies } from '../lib/services/emergency';
import { StudentContext } from './studentContext';

/** Every data page sits behind this, because database rules refuse anonymous reads. */
export function Component() {
  const [uid, setUid] = useState<string | null | undefined>(undefined);
  useEffect(() => watchAuth(setUid), []);

  if (uid === undefined) return <p>Checking sign-in...</p>;
  if (uid === null) return <SignInForm />;
  return <SignedIn uid={uid} />;
}

function SignInForm() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await signIn(email.trim(), password);
    } catch {
      // Generic on purpose: the message must not reveal whether an email has an account.
      setError('Those sign-in details did not work.');
      setPassword('');
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="panel form" onSubmit={submit}>
      <h1>Supervisor sign-in</h1>
      <label>
        Email
        <input type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
      </label>
      <label>
        Password
        <input type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
      </label>
      {error && <p role="alert" className="chip chip-help">{error}</p>}
      <button className="btn btn-go" type="submit" disabled={busy}>{busy ? 'Signing in...' : 'Sign in'}</button>
    </form>
  );
}

/** Steady, on every page, so a supervisor on another page still sees an open alert. */
function OpenAlertBanner({ studentId }: { studentId: string }) {
  const [openCount, setOpenCount] = useState(0);
  const { pathname } = useLocation();

  useEffect(() => watchEmergencies(studentId, 20, (list) => setOpenCount(list.filter((e) => e.status === 'open').length)), [studentId]);

  if (openCount === 0 || pathname === '/emergency') return null;
  return (
    <p role="alert" className="chip chip-help">
      {studentId} has an open alert. <Link to="/emergency">Go to Emergency Intervention</Link>
    </p>
  );
}

function SignedIn({ uid }: { uid: string }) {
  const [access, setAccess] = useState<SupervisorAccess | null>(null);
  const [failed, setFailed] = useState(false);
  const [picked, setPicked] = useState<string | null>(null);

  useEffect(() => watchAccess(uid, setAccess, () => setFailed(true)), [uid]);

  const studentId = picked && access?.studentIds.includes(picked) ? picked : access?.studentIds[0] ?? null;

  return (
    <>
      <div className="toolbar">
        {access && access.studentIds.length > 0 && (
          <label>
            Learner{' '}
            <select value={studentId ?? ''} onChange={(e) => setPicked(e.target.value)}>
              {access.studentIds.map((id) => <option key={id} value={id}>{id}</option>)}
            </select>
          </label>
        )}
        <button className="btn" type="button" onClick={() => void signOut()}>Sign out</button>
      </div>
      {failed && <p role="alert" className="chip chip-help">This account could not load its access. Check that it is listed under /supervisors.</p>}
      {access && access.role === null && !failed && (
        <p role="alert" className="chip chip-help">This account has no supervisor record, so the database will refuse its reads. Ask an admin to add it.</p>
      )}
      <StudentContext.Provider value={studentId}>
        {studentId && <OpenAlertBanner key={studentId} studentId={studentId} />}
        <Outlet />
      </StudentContext.Provider>
    </>
  );
}
