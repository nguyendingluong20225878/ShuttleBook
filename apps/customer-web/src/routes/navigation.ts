export function navigate(path: string, replace = false, state: unknown = null) {
  if (replace) history.replaceState(state, '', path); else history.pushState(state, '', path);
  window.dispatchEvent(new Event('popstate'));
}
export function safeReturnTo() {
  const value = new URLSearchParams(location.search).get('returnTo');
  if (!value || !/^\/(venues(?:\/|\?|$)|teams(?:\/|\?|$)|booking-review\?|series-review(?:\?|$)|bookings\/|me\/(?:bookings|play-schedule|notifications|teams|team-requests)(?:\?|$))/.test(value) || value.startsWith('//')) return '/venues';
  return value;
}
