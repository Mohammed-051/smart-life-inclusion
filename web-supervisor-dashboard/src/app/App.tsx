import { createBrowserRouter, Navigate, NavLink, Outlet, RouterProvider, useRouteError } from 'react-router-dom';
import './layout.css';

const NAV = [
  { to: '/routines', label: 'Routine Builder' },
  { to: '/telemetry', label: 'Live Telemetry' },
  { to: '/emergency', label: 'Emergency Intervention' },
];

function AppLayout() {
  return (
    <div className="shell">
      <nav className="shell-nav" aria-label="Main">
        <strong className="shell-brand">Smart-Life Supervisor</strong>
        {NAV.map(({ to, label }) => (
          <NavLink key={to} to={to} className="shell-link">
            {label}
          </NavLink>
        ))}
      </nav>
      <main className="shell-main">
        <Outlet />
      </main>
    </div>
  );
}

// Pages load Firebase lazily, so missing config breaks only the page that needs it.
function PageError() {
  console.error(useRouteError());
  return (
    <>
      <h1>This page could not load</h1>
      <p>Check that <code>.env</code> has your Firebase settings (see <code>.env.example</code>), then reload.</p>
    </>
  );
}

const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      {
        errorElement: <PageError />,
        children: [
          { index: true, element: <Navigate to="/telemetry" replace /> },
          { path: 'routines', lazy: () => import('./pages/RoutineBuilderPage') },
          { path: 'telemetry', lazy: () => import('./pages/LiveTelemetryPage') },
          { path: 'emergency', lazy: () => import('./pages/EmergencyInterventionPage') },
          { path: '*', element: <Navigate to="/telemetry" replace /> },
        ],
      },
    ],
  },
]);

export default function App() {
  return <RouterProvider router={router} />;
}
