import { initializeApp } from 'firebase/app';
import { getAuth, connectAuthEmulator } from 'firebase/auth';
import { getDatabase, connectDatabaseEmulator } from 'firebase/database';

// Web config is public by design; access is enforced by RTDB rules + Auth (docs/SECURITY.md).
const app = initializeApp({
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
  databaseURL: import.meta.env.VITE_FIREBASE_DATABASE_URL,
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
  appId: import.meta.env.VITE_FIREBASE_APP_ID,
});

export const auth = getAuth(app);
export const db = getDatabase(app);

if (import.meta.env.VITE_USE_EMULATOR === 'true') {
  connectDatabaseEmulator(db, '127.0.0.1', 9000);
  connectAuthEmulator(auth, 'http://127.0.0.1:9099', { disableWarnings: true });
}
