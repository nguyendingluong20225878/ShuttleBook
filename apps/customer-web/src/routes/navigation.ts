export function navigate(path: string, replace = false, state: unknown = null) {
  if (replace) history.replaceState(state, '', path); else history.pushState(state, '', path);
  window.dispatchEvent(new Event('popstate'));
}
export function safeReturnTo() {
  const value = new URLSearchParams(location.search).get('returnTo');
  if (!value || !/^\/(venues(?:\/|\?|$)|booking-review\?|series-review(?:\?|$)|bookings\/|me\/(?:bookings|notifications)(?:\?|$))/.test(value) || value.startsWith('//')) return '/venues';
  return value;
}
