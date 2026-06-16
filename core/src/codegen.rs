pub struct Output {
    pub rust: String,
    pub typescript: String,
    pub csharp: String,
}

pub fn generate(name: &str, src: &str) -> Output {
    let records = parse_records(src);
    let endpoints = parse_endpoints(src);

    Output {
        rust: gen_rust(name, &records, &endpoints),
        typescript: gen_ts(name, &records, &endpoints),
        csharp: gen_cs(name, &records, &endpoints),
    }
}

#[derive(Debug, Clone)]
struct Record {
    name: String,
    fields: Vec<Field>,
}

#[derive(Debug, Clone)]
struct Field {
    name: String,
    wit_type: String,
}

#[derive(Debug, Clone)]
struct Endpoint {
    method: String,
    name: String,
    args: Vec<Arg>,
    returns: String,
}

#[derive(Debug, Clone)]
struct Arg {
    kind: ArgKind,
    name: String,
    wit_type: String,
}

#[derive(Debug, Clone, PartialEq)]
enum ArgKind {
    Body,
    Path,
}

fn pascal(s: &str) -> String {
    s.split('-')
        .map(|w| {
            let mut c = w.chars();
            match c.next() {
                None => String::new(),
                Some(f) => f.to_uppercase().chain(c).collect(),
            }
        })
        .collect()
}

fn camel(s: &str) -> String {
    let p = pascal(s);
    let mut c = p.chars();
    match c.next() {
        None => String::new(),
        Some(f) => f.to_lowercase().chain(c).collect(),
    }
}

fn wit_to_rust(t: &str) -> String {
    t.replace("u32", "u32")
        .replace("string", "String")
        .replace("bool", "bool")
        .replace("f32", "f32")
        .replace("unit", "()")
}

fn wrap_rust_type(t: &str) -> String {
    let base = wit_to_rust(t);
    if base.contains('<') { return base; }
    if base == "u32" || base == "String" || base == "bool" || base == "f32" || base == "()" {
        return base;
    }
    pascal(t)
}

fn wrap_opt_rs(t: &str) -> String {
    if let Some(inner) = t.strip_prefix("option<").and_then(|s| s.strip_suffix('>')) {
        format!("Option<{}>", wrap_rust_type(inner))
    } else if let Some(inner) = t.strip_prefix("list<").and_then(|s| s.strip_suffix('>')) {
        format!("Vec<{}>", wrap_rust_type(inner))
    } else {
        wrap_rust_type(t)
    }
}

fn wit_to_ts(t: &str) -> String {
    if let Some(inner) = t.strip_prefix("option<").and_then(|s| s.strip_suffix('>')) {
        format!("{} | null", wit_to_ts(inner))
    } else if let Some(inner) = t.strip_prefix("list<").and_then(|s| s.strip_suffix('>')) {
        format!("{}[]", wit_to_ts(inner))
    } else {
        t.replace("u32", "number")
            .replace("string", "string")
            .replace("bool", "boolean")
            .replace("f32", "number")
            .replace("unit", "void")
    }
}

fn wrap_ts_type(t: &str) -> String {
    let base = wit_to_ts(t);
    if base == "number" || base == "string" || base == "boolean" || base == "void" || base.contains("null") || base.contains("[]") {
        return base;
    }
    pascal(t)
}

fn wit_to_cs(t: &str) -> String {
    if let Some(inner) = t.strip_prefix("option<").and_then(|s| s.strip_suffix('>')) {
        format!("{}?", wit_to_cs(inner))
    } else if let Some(inner) = t.strip_prefix("list<").and_then(|s| s.strip_suffix('>')) {
        format!("{}[]", wit_to_cs(inner))
    } else {
        t.replace("u32", "uint")
            .replace("string", "string")
            .replace("bool", "bool")
            .replace("f32", "float")
            .replace("unit", "void")
    }
}

fn wrap_cs_type(t: &str) -> String {
    let base = wit_to_cs(t);
    if base == "uint" || base == "string" || base == "bool" || base == "float" || base == "void" || base.contains('?') || base.contains("[]") {
        return base;
    }
    pascal(t)
}

// ── parser ────────────────────────────────────────────────

fn parse_records(src: &str) -> Vec<Record> {
    let mut records = Vec::new();
    let mut current: Option<Record> = None;
    let mut depth = 0;

    for line in src.lines() {
        let line = line.split("//").next().unwrap_or("").trim();
        if line.is_empty() { continue; }

        if let Some(rm) = line.strip_prefix("record ").and_then(|s| s.strip_suffix('{')) {
            current = Some(Record { name: rm.trim().to_string(), fields: Vec::new() });
            depth = 1;
            continue;
        }

        if let Some(ref mut rec) = current {
            if line == "}" {
                depth -= 1;
                if depth == 0 {
                    records.push(current.take().unwrap());
                }
                continue;
            }
            if let Some((name, typ)) = line.split_once(':') {
                let typ = typ.trim().trim_end_matches(',');
                rec.fields.push(Field { name: name.trim().to_string(), wit_type: typ.to_string() });
            }
        }
    }
    records
}

fn parse_endpoints(src: &str) -> Vec<Endpoint> {
    let mut eps = Vec::new();

    for line in src.lines() {
        let line = line.split("//").next().unwrap_or("").trim();
        if line.is_empty() || line.starts_with("record ") || line.starts_with("interface ") { continue; }

        // get|post|put|delete name(args): returnType
        let parts: Vec<&str> = line.splitn(2, ' ').collect();
        if parts.len() < 2 { continue; }
        let method = parts[0];
        if !["get","post","put","delete"].contains(&method) { continue; }

        let rest = parts[1];
        let paren_pos = rest.find('(');
        let colon_pos = rest.find(':');

        let (name_str, remainder) = match (paren_pos, colon_pos) {
            (Some(pos), _) => (rest[..pos].trim(), &rest[pos..]),
            (_, Some(pos)) => (rest[..pos].trim(), &rest[pos..]),
            _ => continue,
        };

        let name = name_str.to_string();
        let (args_str, returns) = if remainder.starts_with('(') {
            let end = remainder.find(')').unwrap_or(remainder.len());
            let args = &remainder[1..end];
            let ret = remainder[end + 1..].trim().trim_start_matches(':').trim();
            (args, ret.to_string())
        } else {
            ("", remainder.trim_start_matches(':').trim().to_string())
        };

        let args = parse_arg_list(args_str);
        eps.push(Endpoint { method: method.to_string(), name: name.to_string(), args, returns });
    }
    eps
}

fn parse_arg_list(raw: &str) -> Vec<Arg> {
    if raw.is_empty() { return vec![]; }
    let mut args = Vec::new();
    let mut depth = 0;
    let mut buf = String::new();

    for ch in raw.chars().chain(std::iter::once(',')) {
        match ch {
            '<' => { depth += 1; buf.push(ch); }
            '>' => { depth -= 1; buf.push(ch); }
            ',' if depth == 0 => {
                let a = buf.trim().to_string();
                if !a.is_empty() {
                    if let Some((n, t)) = a.split_once(':') {
                        args.push(Arg { kind: ArgKind::Path, name: n.trim().to_string(), wit_type: t.trim().to_string() });
                    } else {
                        args.push(Arg { kind: ArgKind::Body, name: "body".into(), wit_type: a.trim().to_string() });
                    }
                }
                buf.clear();
            }
            _ => buf.push(ch),
        }
    }
    args
}

// ── generators ────────────────────────────────────────────

fn gen_rust(name: &str, records: &[Record], endpoints: &[Endpoint]) -> String {
    let mut out = format!("// Generated by `damascus codegen` from {name}.wit — do not edit.\n\nuse serde::{{Deserialize, Serialize}};\n\n");

    // structs
    for r in records {
        out += &format!("#[derive(Debug, Clone, Serialize, Deserialize)]\npub struct {} {{\n", pascal(&r.name));
        for f in &r.fields {
            let rt = wrap_opt_rs(&f.wit_type);
            let sn = if f.name.contains('-') { format!("#[serde(rename = \"{}\")]\n    ", f.name) } else { String::new() };
            out += &format!("    {}pub {}: {},\n", sn, camel(&f.name), rt);
        }
        out += "}\n\n";
    }

    // trait
    out += &format!("#[axum::async_trait]\npub trait {}Api: Clone + Send + Sync + 'static {{\n", pascal(name));
    for ep in endpoints {
        let params = ep.args.iter().map(|a| {
            if a.kind == ArgKind::Path {
                format!("{}: {}", camel(&a.name), wrap_opt_rs(&a.wit_type))
            } else {
                format!("body: {}", wrap_opt_rs(&a.wit_type))
            }
        }).collect::<Vec<_>>().join(", ");
        let ret = if ep.returns == "unit" { String::new() } else { format!(" -> {}", wrap_opt_rs(&ep.returns)) };
        out += &format!("    async fn {}(&self{}){};\n", camel(&ep.name), if params.is_empty() { String::new() } else { format!(", {params}") }, ret);
    }
    out += "}\n\n";

    // router
    out += &format!("pub fn router<A: {}Api>(api: A) -> axum::Router {{\n", pascal(name));
    out += "    use std::sync::Arc;\n";
    out += "    use axum::{Router, Extension, Json, extract::Path, routing::{get, post, put, delete}};\n";
    out += "    use axum::response::IntoResponse;\n";
    out += "    use axum::http::StatusCode;\n\n";
    out += "    let api = Arc::new(api);\n    Router::new()\n";

    for ep in endpoints {
        let path = if ep.args.iter().any(|a| a.kind == ArgKind::Path) {
            let segments: Vec<String> = ep.args.iter().filter(|a| a.kind == ArgKind::Path).map(|a| format!("{{{}}}", camel(&a.name))).collect();
            format!("\"/api/{}/{}\"", name, segments.join("/"))
        } else {
            format!("\"/api/{}\"", name)
        };
        out += &format!("        .route({path}, {}(__h_{}))\n", ep.method, ep.name.replace('-', "_"));
    }
    out += "        .layer(Extension(api))\n}\n\n";

    // handlers
    for ep in endpoints {
        let hname = format!("__h_{}", ep.name.replace('-', "_"));
        let mut params = vec!["Extension(api): Extension<Arc<A>>".to_string()];
        for a in &ep.args {
            if a.kind == ArgKind::Path {
                params.push(format!("Path({}): Path<{}>", camel(&a.name), wrap_opt_rs(&a.wit_type)));
            } else {
                params.push(format!("Json(body): Json<{}>", wrap_opt_rs(&a.wit_type)));
            }
        }
        let ret = if ep.returns == "unit" { "" } else { " -> impl IntoResponse" };
        out += &format!("async fn {hname}<A: {}Api>({}){} {{\n", pascal(name), params.join(", "), ret);

        let call_args: Vec<String> = std::iter::once("api".to_string()).chain(ep.args.iter().map(|a| {
            if a.kind == ArgKind::Path { camel(&a.name) } else { "body".into() }
        })).collect();
        if ep.returns == "unit" {
            out += &format!("    {}.{}().await;\n", call_args.join("."), camel(&ep.name));
            out += "    StatusCode::NO_CONTENT\n";
        } else {
            out += &format!("    Json({}.{}().await)\n", call_args.join("."), camel(&ep.name));
        }
        out += "}\n\n";
    }

    out
}

fn gen_ts(name: &str, records: &[Record], endpoints: &[Endpoint]) -> String {
    let pname = pascal(name);
    let mut out = format!("// Generated by `damascus codegen` from {name}.wit — do not edit.\n\n");

    for r in records {
        out += &format!("export interface {} {{\n", pascal(&r.name));
        for f in &r.fields {
            out += &format!("  {}: {};\n", camel(&f.name), wrap_ts_type(&f.wit_type));
        }
        out += "}\n\n";
    }

    out += "const BASE = \"http://127.0.0.1:3001\";\n\n";
    out += &format!("export function create{pname}Client() {{\n  return {{\n");

    for ep in endpoints {
        let body = ep.args.iter().find(|a| a.kind == ArgKind::Body);
        let paths: Vec<_> = ep.args.iter().filter(|a| a.kind == ArgKind::Path).collect();

        let fn_params: Vec<String> = paths.iter().map(|a| format!("{}: {}", camel(&a.name), wrap_ts_type(&a.wit_type)))
            .chain(body.iter().map(|a| format!("{}: {}", camel(&a.wit_type), wrap_ts_type(&a.wit_type))))
            .collect();

        let url_parts: Vec<String> = std::iter::once(format!("${{BASE}}/api/{name}")).chain(paths.iter().map(|a| format!("${{{}}}", camel(&a.name)))).collect();
        let url = url_parts.join(" + \"/\" + ");

        let body_code = if let Some(b) = body {
            format!(",\n        body: JSON.stringify({}),\n        headers: {{ \"Content-Type\": \"application/json\" }}", camel(&b.wit_type))
        } else { String::new() };

        let ret = if ep.returns == "unit" { "Promise<void>".into() } else { format!("Promise<{}>", wrap_ts_type(&ep.returns)) };

        out += &format!("    async {}({}): {} {{\n", camel(&ep.name), fn_params.join(", "), ret);
        out += &format!("      const res = await fetch({}, {{ method: \"{}\"{} }});\n", url, ep.method.to_uppercase(), body_code);
        if ep.returns == "unit" {
            out += "      if (!res.ok) throw new Error(res.statusText);\n";
        } else {
            out += "      if (!res.ok) throw new Error(res.statusText);\n";
            out += "      return res.json();\n";
        }
        out += "    },\n\n";
    }

    out += "  };\n}\n";
    out
}

fn gen_cs(name: &str, records: &[Record], endpoints: &[Endpoint]) -> String {
    let pname = pascal(name);
    let mut out = format!("// Generated by `damascus codegen` from {name}.wit — do not edit.\n\nusing System.Net.Http.Json;\nusing System.Text.Json.Serialization;\n\nnamespace DamascusUI.Gen;\n\n");

    for r in records {
        out += &format!("public record {}(\n", pascal(&r.name));
        let fields: Vec<String> = r.fields.iter().map(|f| {
            format!("    {} {}", wrap_cs_type(&f.wit_type), pascal(&f.name))
        }).collect();
        out += &fields.join(",\n");
        out += "\n);\n\n";
    }

    // Collect all serialized types (deduplicated)
    let mut serialized_types: Vec<String> = Vec::new();
    for r in records {
        let t = pascal(&r.name);
        if !serialized_types.contains(&t) {
            serialized_types.push(t);
        }
    }
    for ep in endpoints {
        if ep.returns != "unit" {
            let rt = wrap_cs_type(&ep.returns);
            if !serialized_types.contains(&rt) {
                serialized_types.push(rt.clone());
            }
            let arr = format!("{}[]", rt);
            if !serialized_types.contains(&arr) {
                serialized_types.push(arr);
            }
        }
    }

    // Source-gen JSON context
    let ctx_name = format!("{pname}JsonContext");
    out += &format!("[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]\n");
    for t in &serialized_types {
        out += &format!("[JsonSerializable(typeof({t}))]\n");
    }
    out += &format!("public partial class {ctx_name} : JsonSerializerContext {{ }}\n\n");

    out += &format!("public class {pname}Client(HttpClient http)\n{{\n");
    out += &format!("    private static readonly {ctx_name} _json = {ctx_name}.Default;\n\n");

    for ep in endpoints {
        let body = ep.args.iter().find(|a| a.kind == ArgKind::Body);
        let paths: Vec<_> = ep.args.iter().filter(|a| a.kind == ArgKind::Path).collect();

        let fn_params: Vec<String> = paths.iter().map(|a| format!("{} {}", wrap_cs_type(&a.wit_type), pascal(&a.name)))
            .chain(body.iter().map(|a| format!("{} {}", wrap_cs_type(&a.wit_type), camel(&a.wit_type))))
            .collect();

        let url_parts: Vec<String> = std::iter::once(format!("\"/api/{name}\"")).chain(paths.iter().map(|a| format!("${{{}}}", pascal(&a.name)))).collect();
        let url = url_parts.join(" + \"/\" + ");

        let http_method = match ep.method.as_str() {
            "delete" => "DeleteAsync",
            "put" => "PutAsync",
            "post" => "PostAsync",
            _ => "GetAsync",
        };

        let ret = if ep.returns == "unit" { "Task".into() } else { format!("Task<{}>", wrap_cs_type(&ep.returns)) };
        let ret_type = wrap_cs_type(&ep.returns);
        let json_ret_type = ret_type.replace("[]", "Array");

        out += &format!("    public async {ret} {}({})\n    {{\n", pascal(&ep.name), fn_params.join(", "));

        if ep.method == "get" || ep.method == "delete" {
            if ep.returns == "unit" {
                out += &format!("        await http.{http_method}({url});\n");
            } else {
                out += &format!("        var res = await http.{http_method}({url});\n");
                out += &format!("        return (await res.Content.ReadFromJsonAsync(_json.{json_ret_type}))!;\n");
            }
        } else {
            let body_var_name = body.map_or("body".to_string(), |b| camel(&b.wit_type));
            let body_json_type = body.map_or(String::new(), |b| pascal(&b.wit_type));
            if ep.returns == "unit" {
                out += &format!("        await http.{http_method}({url}, JsonContent.Create({body_var_name}, _json.{body_json_type}));\n");
            } else {
                out += &format!("        var res = await http.{http_method}({url}, JsonContent.Create({body_var_name}, _json.{body_json_type}));\n");
                out += &format!("        return (await res.Content.ReadFromJsonAsync(_json.{json_ret_type}))!;\n");
            }
        }
        out += "    }\n\n";
    }

    out += "}\n";
    out
}
