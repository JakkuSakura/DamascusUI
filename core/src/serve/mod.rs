use axum::Router;

#[cfg(feature = "tokio")]
use tokio::net::TcpListener;

#[cfg(feature = "tokio")]
use std::thread;

use crate::config::Config;
use crate::error::Result;

#[cfg(feature = "tokio")]
mod viewer;
#[cfg(feature = "tokio")]
pub mod ws;

#[cfg(feature = "tokio")]
pub fn serve(router: Router, config: &Config) -> Result<()> {
    let rt = tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()?;

    rt.block_on(async {
        let (listener, addr) = bind_with_fallback(config).await?;
        tracing::info!("DamascusUI listening on http://{addr}");

        // Launch viewer with the actual bound address
        let shutdown = viewer::try_launch(&addr, &config.app_name).map(|(mut child, notify)| {
            let notify_clone = notify.clone();
            thread::spawn(move || {
                let _ = child.wait();
                notify_clone.notify_one();
            });
            notify
        });

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

#[cfg(feature = "tokio")]
async fn bind_with_fallback(config: &Config) -> Result<(TcpListener, String)> {
    let port = config.port;
    for offset in 0..100 {
        let candidate = format!("{}:{}", config.host, port + offset);
        match TcpListener::bind(&candidate).await {
            Ok(listener) => return Ok((listener, candidate)),
            Err(e) if e.kind() == std::io::ErrorKind::AddrInUse => continue,
            Err(e) => return Err(e.into()),
        }
    }
    Err(crate::error::Error::Bind {
        addr: format!("{}:{}-{}", config.host, port, port + 99),
        source: std::io::Error::new(std::io::ErrorKind::AddrInUse, "all ports in range busy"),
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
