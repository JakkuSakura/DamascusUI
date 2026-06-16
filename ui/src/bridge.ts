// Bridge from the SolidJS frontend to the Avalonia desktop viewer.
// Uses location.href to damascus:// URLs — the webview intercepts
// NavigationStarting for these custom schemes without navigating away.

export function setWindowTitle(title: string) {
  window.location.href = `damascus://set-title/${encodeURIComponent(title)}`;
}

export function setWindowIcon(iconBase64: string) {
  window.location.href = `damascus://set-icon/${iconBase64}`;
}
