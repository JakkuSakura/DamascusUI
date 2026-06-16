use std::fs;

fn main() {
    let ui_dist = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../ui/dist");
    if ui_dist.exists() {
        println!("cargo:warning=bundling frontend from ui/dist");
        println!("cargo:rerun-if-changed=../ui/dist");
    }
}
