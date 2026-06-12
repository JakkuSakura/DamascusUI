//! Damascus CLI — codegen, serve, and single-binary frontend.

mod codegen;

use std::env;
use std::fs;
use std::path::Path;

fn main() {
    let args: Vec<String> = env::args().collect();
    if args.len() < 2 {
        eprintln!("Usage: damascus <codegen|serve> [args...]");
        return;
    }

    match args[1].as_str() {
        "codegen" => {
            if args.len() < 3 {
                eprintln!("Usage: damascus codegen <file.wit>");
                return;
            }
            run_codegen(&args[2]);
        }
        "serve" => serve(),
        _ => eprintln!("unknown command: {}", args[1]),
    }
}

#[cfg(feature = "tokio")]
fn serve() {
    use std::process::Command;
    use std::sync::Arc;
    use tokio::sync::Notify;

    tracing_subscriber::fmt::init();
    let config = damascus::Config::new().port(3000);

    let app = axum::Router::new()
        .route("/health", axum::routing::get(|| async { "ok" }))
        .fallback(damascus::static_files::serve_static);

    // Try to launch the Avalonia desktop viewer
    let viewer = find_viewer_binary()
        .and_then(|path| Command::new(&path).spawn().ok());

    if viewer.is_some() {
        tracing::info!("Launched desktop viewer");
    }

    let addr = config.addr();
    let rt = tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()
        .unwrap();

    let shutdown = Arc::new(Notify::new());
    let shutdown_clone = shutdown.clone();

    // Watch the viewer process — when it exits, shut down the server
    if let Some(mut child) = viewer {
        std::thread::spawn(move || {
            let _ = child.wait();
            shutdown_clone.notify_one();
        });
    }

    rt.block_on(async {
        let listener = tokio::net::TcpListener::bind(&addr).await.unwrap();
        tracing::info!("DamascusUI listening on http://{addr}");

        axum::serve(listener, app)
            .with_graceful_shutdown(async move { shutdown.notified().await })
            .await
            .unwrap();
    });
}

fn find_viewer_binary() -> Option<std::path::PathBuf> {
    let exe = std::env::current_exe().ok()?;
    let dir = exe.parent()?;

    let base = if cfg!(target_os = "windows") { "DamascusUI.exe" } else { "DamascusUI" };

    let candidates = [
        dir.join("../../../csharp/bin/Debug/net10.0").join(base),
        dir.join("../../../csharp/bin/Release/net10.0").join(base),
        dir.join("../csharp/bin/Debug/net10.0").join(base),
        dir.join("../csharp/bin/Release/net10.0").join(base),
    ];

    candidates.into_iter().find(|p| p.exists())
}

#[cfg(not(feature = "tokio"))]
fn serve() {
    eprintln!("serve requires the 'tokio' feature. Rebuild with: cargo build -F tokio");
}

fn run_codegen(path: &str) {
    let src = fs::read_to_string(path).expect("failed to read WIT file");
    let name = Path::new(path)
        .file_stem()
        .unwrap()
        .to_str()
        .unwrap()
        .to_string();

    let out = codegen::generate(&name, &src);
    let root = Path::new(path).parent().unwrap_or(Path::new("."));

    let rs_dir = root.join("../../rs/src/codegen");
    let ts_dir = root.join("../../ts/src/codegen");
    let cs_dir = root.join("../../csharp/codegen");

    fs::create_dir_all(&rs_dir).ok();
    fs::create_dir_all(&ts_dir).ok();
    fs::create_dir_all(&cs_dir).ok();

    fs::write(rs_dir.join(format!("{name}.rs")), &out.rust).unwrap();
    fs::write(ts_dir.join(format!("{name}.ts")), &out.typescript).unwrap();
    fs::write(cs_dir.join(format!("{}.cs", pascal(&name))), &out.csharp).unwrap();

    println!("  ✓ rs/src/codegen/{name}.rs");
    println!("  ✓ ts/src/codegen/{name}.ts");
    println!("  ✓ csharp/codegen/{}.cs", pascal(&name));
}

fn pascal(s: &str) -> String {
    s.split('-')
        .map(|w| {
            let mut c = w.chars();
            match c.next() {
                None => String::new(),
                Some(f) => f.to_uppercase().collect::<String>() + c.as_str(),
            }
        })
        .collect()
}
