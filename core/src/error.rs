#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("I/O error: {0}")]
    Io(#[from] std::io::Error),

    #[error("server bind failed on {addr}: {source}")]
    Bind {
        addr: String,
        source: std::io::Error,
    },

    #[error("protocol error: {0}")]
    Protocol(String),

    #[error("not found: {0}")]
    NotFound(String),

    #[error("{0}")]
    Internal(String),
}

pub type Result<T> = std::result::Result<T, Error>;
