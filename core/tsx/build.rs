use std::{env, fs, path::PathBuf};

fn main() {
    let out_dir = PathBuf::from(env::var_os("OUT_DIR").expect("OUT_DIR is not set"));
    fs::write(
        out_dir.join("damascus-tsx-index.html"),
        "<html><body><div id=\"root\"></div></body></html>",
    )
    .expect("failed to write TSX test document");
}
