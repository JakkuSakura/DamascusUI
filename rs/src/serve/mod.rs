use std::thread;

use axum::Router;

use crate::config::Config;
use crate::error::Result;

#[cfg(feature = "tokio")]
mod viewer;

#[cfg(feature = "tokio")]
pub fn serve(router: Router, config: &Config) -> Result<()> {
    let rt = tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()?;

    // Try to launch the desktop viewer
    let shutdown = viewer::try_launch().map(|(mut child, notify)| {
        let notify_clone = notify.clone();
        thread::spawn(move || {
            let _ = child.wait();
            notify_clone.notify_one();
        });
        notify
    });

    rt.block_on(async {
        let listener = tokio::net::TcpListener::bind(config.addr()).await?;
        tracing::info!("DamascusUI listening on http://{}", config.addr());

        let app = router;

        if let Some(shutdown) = shutdown {
            axum::serve(listener, app)
                .with_graceful_shutdown(async move { shutdown.notified().await })
                .await?;
        } else {
            axum::serve(listener, app).await?;
        }

        Ok(())
    })
}

#[cfg(feature = "compio")]
pub fn serve(router: Router, config: &Config) -> Result<()> {
    compio_runtime::Runtime::new()?.block_on(async {
        let listener = compio_net::TcpListener::bind(config.addr()).await?;
        tracing::info!("DamascusUI (compio) listening on http://{}", config.addr());
        cyper_axum::serve(listener, router).await.map_err(|e| {
            crate::error::Error::Internal(e.to_string())
        })
    })
}
