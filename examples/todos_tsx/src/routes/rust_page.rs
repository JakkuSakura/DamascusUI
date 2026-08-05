use damascus::prelude::*;
use damascus_tsx::tsx;
use serde::Serialize;

#[derive(Debug, Serialize)]
struct RustPageData {
    title: String,
    message: String,
}

// A standalone Rust UI route. The TSX fragment is compiled inline; no
// rust_page.tsx file is required.
pub async fn handler() -> impl IntoResponse {
    let data = RustPageData {
        title: "Pure Rust page".into(),
        message: "This text came from Rust through ${...}.".into(),
    };

    tsx! {
        <main>
            <p class="eyebrow">DamascusUI / Rust route</p>
            <h1>${data.title}</h1>
            <p class="summary">
                ${data.message}
            </p>
        </main>
    }
}
