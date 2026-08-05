use std::{
    env, fs,
    path::{Path, PathBuf},
    process::Command,
};

#[derive(Debug, thiserror::Error)]
pub enum BuildError {
    #[error("failed to read TSX source: {0}")]
    Read(#[from] std::io::Error),

    #[error("no TSX component source was found under {0}")]
    MissingComponent(PathBuf),

    #[error("frontend command `{command}` failed with status {status}")]
    Command { command: String, status: String },
}

/// Build SolidJS source declared in Rust `tsx!` blocks.
pub fn compile_from(source_path: impl AsRef<Path>) -> Result<(), BuildError> {
    let source_path = source_path.as_ref();
    let sources = collect_sources(source_path)?;
    if sources.components.is_empty() && sources.routes.is_empty() {
        return Err(BuildError::MissingComponent(source_path.to_owned()));
    }

    let out_dir =
        PathBuf::from(env::var_os("OUT_DIR").ok_or_else(|| {
            std::io::Error::new(std::io::ErrorKind::NotFound, "OUT_DIR is not set")
        })?);
    let project_dir = out_dir.join("damascus-tsx-frontend");
    let src_dir = project_dir.join("src");
    fs::create_dir_all(&src_dir)?;

    fs::write(project_dir.join("package.json"), package_json())?;
    fs::write(project_dir.join("index.html"), index_html())?;
    fs::write(project_dir.join("vite.config.ts"), vite_config())?;
    fs::write(project_dir.join("src/index.tsx"), index_tsx(&sources))?;
    fs::write(project_dir.join("src/style.css"), style_css())?;

    if !sources.routes.is_empty() {
        for route in &sources.routes {
            let destination = src_dir.join("routes").join(&route.relative_path);
            if let Some(parent) = destination.parent() {
                fs::create_dir_all(parent)?;
            }
            fs::write(destination, &route.source)?;
        }
    } else {
        for (index, component) in sources.components.iter().enumerate() {
            let destination = src_dir
                .join("inline")
                .join(format!("component-{index}.tsx"));
            fs::create_dir_all(destination.parent().unwrap())?;
            fs::write(destination, component)?;
        }
    }

    run(&project_dir, "pnpm", &["install", "--ignore-scripts"])?;
    run(&project_dir, "pnpm", &["run", "build"])?;

    let dist_dir = project_dir.join("dist");
    let mut html = fs::read_to_string(dist_dir.join("index.html"))?;
    let assets_dir = dist_dir.join("assets");
    for entry in fs::read_dir(&assets_dir)? {
        let path = entry?.path();
        let Some(name) = path.file_name().and_then(|name| name.to_str()) else {
            continue;
        };
        let url = format!("/assets/{name}");
        let content = fs::read_to_string(&path)?;
        if name.ends_with(".js") {
            let tag = format!("<script type=\"module\" crossorigin src=\"{url}\"></script>");
            html = html.replace(&tag, &format!("<script type=\"module\">{content}</script>"));
        } else if name.ends_with(".css") {
            let tag = format!("<link rel=\"stylesheet\" crossorigin href=\"{url}\">");
            html = html.replace(&tag, &format!("<style>{content}</style>"));
        }
    }

    fs::write(out_dir.join("damascus-tsx-index.html"), html)?;
    println!("cargo:rerun-if-changed={}", source_path.display());
    Ok(())
}

struct Sources {
    components: Vec<String>,
    routes: Vec<RouteSource>,
}

struct RouteSource {
    relative_path: PathBuf,
    source: String,
}

fn collect_sources(root: &Path) -> Result<Sources, BuildError> {
    let mut sources = Sources {
        components: Vec::new(),
        routes: Vec::new(),
    };
    collect_sources_recursive(root, root, &mut sources)?;
    Ok(sources)
}

fn collect_sources_recursive(
    root: &Path,
    path: &Path,
    sources: &mut Sources,
) -> Result<(), BuildError> {
    if path.is_dir() {
        for entry in fs::read_dir(path)? {
            let entry = entry?;
            let name = entry.file_name();
            if name == "target" || name.to_string_lossy().starts_with('.') {
                continue;
            }
            collect_sources_recursive(root, &entry.path(), sources)?;
        }
        return Ok(());
    }

    match path.extension().and_then(|extension| extension.to_str()) {
        Some("rs") => {
            let source = fs::read_to_string(path)?;
            sources.components.extend(extract_components(&source));
        }
        Some("tsx") => {
            let routes_root = root.join("src/routes");
            if let Ok(relative_path) = path.strip_prefix(&routes_root) {
                sources.routes.push(RouteSource {
                    relative_path: relative_path.to_owned(),
                    source: fs::read_to_string(path)?,
                });
            }
        }
        _ => {}
    }
    Ok(())
}

fn run(dir: &Path, command: &str, args: &[&str]) -> Result<(), BuildError> {
    let status = Command::new(command).args(args).current_dir(dir).status()?;
    if status.success() {
        return Ok(());
    }
    Err(BuildError::Command {
        command: format!("{command} {}", args.join(" ")),
        status: status.to_string(),
    })
}

fn extract_components(source: &str) -> Vec<String> {
    let marker = "tsx!";
    let mut components = Vec::new();
    let mut search_from = 0;
    while let Some(relative) = source[search_from..].find(marker) {
        let start = search_from + relative + marker.len();
        let Some(open_relative) = source[start..].find('{') else {
            break;
        };
        let open = start + open_relative;
        let Some(end) = matching_brace(source, open) else {
            break;
        };
        let block = source[open + 1..end].trim().to_owned();
        if block.contains("export default") {
            components.push(block);
        }
        search_from = end + 1;
    }
    components
}

fn matching_brace(source: &str, open: usize) -> Option<usize> {
    let mut depth = 0;
    for (index, character) in source[open..].char_indices() {
        match character {
            '{' => depth += 1,
            '}' => {
                depth -= 1;
                if depth == 0 {
                    return Some(open + index);
                }
            }
            _ => {}
        }
    }
    None
}

fn index_tsx(sources: &Sources) -> String {
    let mut imports = Vec::new();
    let mut routes = Vec::new();
    if !sources.routes.is_empty() {
        for (index, route) in sources.routes.iter().enumerate() {
            let module = route.relative_path.with_extension("");
            let module = module.to_string_lossy().replace('\\', "/");
            let module = format!("./routes/{}", module.trim_end_matches('/'));
            let route_path = route_path(&route.relative_path);
            imports.push(format!("import Route{index} from \"{module}\";"));
            routes.push(format!("  \"{route_path}\": Route{index}"));
        }
    } else {
        for index in 0..sources.components.len() {
            imports.push(format!(
                "import Route{index} from \"./inline/component-{index}\";"
            ));
            let route_path = if index == 0 {
                "/".to_owned()
            } else {
                format!("/component-{index}")
            };
            routes.push(format!("  \"{route_path}\": Route{index}"));
        }
    }

    format!(
        r#"import {{ render }} from "solid-js/web";
{}
import "./style.css";

const bootstrap = JSON.parse(document.getElementById("damascus-data")?.textContent ?? "{{}}");
const routes = {{
{}
}};
const Component = routes[window.location.pathname] ?? routes["/"];
render(() => (
  <Component
    values={{bootstrap.values ?? {{}}}}
    on_load_content={{() => fetch(`/__damascus/callback/${{bootstrap.callbacks.on_load_content}}`).then((response) => response.json())}}
  />
), document.getElementById("root")!);
"#,
        imports.join("\n"),
        routes.join(",\n")
    )
}

fn route_path(path: &Path) -> String {
    let without_extension = path.with_extension("");
    let text = without_extension.to_string_lossy().replace('\\', "/");
    if text == "index" {
        "/".to_owned()
    } else if text.ends_with("/index") {
        format!(
            "/{}",
            text.trim_end_matches("/index").trim_start_matches('/')
        )
    } else {
        format!("/{}", text.trim_start_matches('/'))
    }
}

fn package_json() -> &'static str {
    r#"{
  "private": true,
  "type": "module",
  "scripts": { "build": "vite build" },
  "dependencies": { "solid-js": "^1.9.13" },
  "devDependencies": {
    "typescript": "^5.8.0",
    "vite": "^6.2.0",
    "vite-plugin-solid": "^2.11.12"
  }
}"#
}

fn index_html() -> &'static str {
    r#"<!doctype html>
<html lang="en">
  <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"><title>Todos TSX</title></head>
  <body>
    <script id="damascus-data" type="application/json">__DAMASCUS_BOOTSTRAP__</script>
    <div id="root"></div>
    <script type="module" src="/src/index.tsx"></script>
  </body>
</html>"#
}

fn vite_config() -> &'static str {
    r#"import { defineConfig } from "vite";
import solid from "vite-plugin-solid";
export default defineConfig({ plugins: [solid()] });
"#
}

fn style_css() -> &'static str {
    r#":root { color: #e8eef5; background: #10161f; font-family: ui-sans-serif, system-ui, sans-serif; }
* { box-sizing: border-box; }
body { margin: 0; min-width: 320px; }
main { width: min(680px, calc(100% - 40px)); margin: 0 auto; padding: 72px 0; }
.eyebrow { color: #6ee7b7; font-size: .75rem; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
h1 { margin: 8px 0; font-size: 3rem; }
h2 { margin-top: 0; }
.summary { color: #a7b3c2; }
.panel { margin: 28px 0; padding: 24px; border: 1px solid #2a394b; border-radius: 8px; background: #17212d; }
ul { margin: 0; padding-left: 20px; }
li { margin: 8px 0; }
li.done { color: #76889b; text-decoration: line-through; }
button { border: 0; border-radius: 6px; padding: 12px 16px; color: #07130f; background: #6ee7b7; font: inherit; font-weight: 700; cursor: pointer; }
button:disabled { cursor: wait; opacity: .65; }
.result { border-color: #6ee7b7; }
"#
}
