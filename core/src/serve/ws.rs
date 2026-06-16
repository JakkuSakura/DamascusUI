use axum::Router;
use axum::extract::ws::{Message, WebSocket};
use tokio::sync::broadcast;

use crate::protocol::ViewerCommand;

#[derive(Clone)]
pub struct ViewerHandle {
    tx: broadcast::Sender<String>,
}

impl ViewerHandle {
    pub fn send(&self, cmd: &ViewerCommand) {
        if let Ok(json) = serde_json::to_string(cmd) {
            let _ = self.tx.send(json);
        }
    }
}

pub fn viewer_route() -> (Router, ViewerHandle) {
    let (tx, _) = broadcast::channel::<String>(32);
    let handle = ViewerHandle { tx: tx.clone() };

    let router = Router::new().route("/ws", {
        axum::routing::get(move |ws: axum::extract::WebSocketUpgrade| {
            let tx = tx.clone();
            async move { ws.on_upgrade(move |socket| handle_viewer(socket, tx)) }
        })
    });

    (router, handle)
}

async fn handle_viewer(mut socket: WebSocket, tx: broadcast::Sender<String>) {
    let mut rx = tx.subscribe();

    loop {
        tokio::select! {
            msg = rx.recv() => {
                match msg {
                    Ok(text) => {
                        if socket.send(Message::Text(text.into())).await.is_err() {
                            break;
                        }
                    }
                    Err(_) => break,
                }
            }
            msg = socket.recv() => {
                match msg {
                    Some(Ok(Message::Text(text))) => {
                        tracing::debug!("viewer event: {text}");
                    }
                    _ => break,
                }
            }
        }
    }
}
