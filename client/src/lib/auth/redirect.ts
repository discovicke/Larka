/** Skickar webbläsaren till inloggningen med sidan man var på, så att den öppnas igen efter inloggningen. */
export function redirectToLogin(returnTo: string): void {
  window.location.assign(`/login?redirect=${encodeURIComponent(returnTo)}`)
}
