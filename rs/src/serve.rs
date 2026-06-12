use crate::config::Config;
use crate::error::Result;

mod inner {
    use axum::Router;

    use super::{Config, Result};

    #[cfg(feature = "tokio")]
    pub fn serve(router: Router, config: &Config) -> Result<()> {
        let rt = tokio::runtime::Builder::new_multi_thread()
            .enable_all()
            .build()?;
        rt.block_on(async {
            let listener = tokio::net::TcpListener::bind(config.addr()).await?;
            tracing::info!("DamascusUI (tokio) listening on http://{}", config.addr());
            axum::serve(listener, router).await?;
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
}

pub(crate) use inner::serve;
