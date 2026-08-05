use axum::Router;

use crate::config::Config;
use crate::error::Result;

#[cfg(feature = "tokio")]
pub fn serve(router: Router, config: &Config) -> Result<()> {
    let rt = tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()?;

    rt.block_on(async {
        let (listener, addr) = bind_with_fallback(config).await?;
        println!("DAMASCUS_ADDR=http://{addr}");
        tracing::info!("DamascusUI listening on http://{addr}");
        axum::serve(listener, router).await?;
        Ok(())
    })
}

#[cfg(feature = "tokio")]
async fn bind_with_fallback(config: &Config) -> Result<(tokio::net::TcpListener, String)> {
    let port = config.port;
    for offset in 0..100 {
        let candidate = format!("{}:{}", config.host, port + offset);
        match tokio::net::TcpListener::bind(&candidate).await {
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

// Prefer Tokio when both runtime features are unified by a workspace build.
#[cfg(all(feature = "compio", not(feature = "tokio")))]
pub fn serve(router: Router, config: &Config) -> Result<()> {
    compio_runtime::Runtime::new()?.block_on(async {
        let listener = compio_net::TcpListener::bind(config.addr()).await?;
        tracing::info!("DamascusUI (compio) listening on http://{}", config.addr());
        cyper_axum::serve(listener, router)
            .await
            .map_err(|e| crate::error::Error::Internal(e.to_string()))
    })
}
