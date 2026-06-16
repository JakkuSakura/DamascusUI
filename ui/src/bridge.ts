// Bridge from the SolidJS frontend to the Avalonia desktop viewer.
// Uses damascus:// URL scheme intercepted by the native webview host.

export function setWindowTitle(title: string) {
  window.location.href = `damascus://set-title/${encodeURIComponent(title)}`;
}

export function setWindowIcon(iconBase64: string) {
  window.location.href = `damascus://set-icon/${iconBase64}`;
}
