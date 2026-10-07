import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './styles.css';
import { CustomerSessionProvider } from './features/auth/CustomerSession';
import { CustomerNotificationsProvider } from './features/notifications/CustomerNotifications';

createRoot(document.getElementById('root')!).render(<StrictMode><CustomerSessionProvider><CustomerNotificationsProvider><App /></CustomerNotificationsProvider></CustomerSessionProvider></StrictMode>);
