use std::process::Command;

fn main() {
    let ts_dir = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../ts");

    if ts_dir.join("package.json").exists() {
        let status = Command::new("pnpm")
            .args(["install", "--frozen-lockfile"])
            .current_dir(&ts_dir)
            .status();

        match status {
            Ok(s) if s.success() => {}
            _ => {
                // fallback: try without frozen lockfile
                Command::new("pnpm")
                    .args(["install"])
                    .current_dir(&ts_dir)
                    .status()
                    .ok();
            }
        }

        let status = Command::new("pnpm")
            .args(["run", "build"])
            .current_dir(&ts_dir)
            .status();

        if let Ok(s) = &status {
            if s.success() {
                println!("cargo:warning=frontend built successfully");
            }
        }
    }

    println!("cargo:rerun-if-changed=../ts/src");
    println!("cargo:rerun-if-changed=../ts/index.html");
    println!("cargo:rerun-if-changed=../ts/vite.config.ts");
    println!("cargo:rerun-if-changed=../ts/package.json");
}
