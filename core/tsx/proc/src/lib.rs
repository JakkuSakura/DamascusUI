use proc_macro::TokenStream;
use proc_macro2::{Delimiter, TokenStream as TokenStream2, TokenTree};
use quote::quote;

#[proc_macro]
pub fn tsx(input: TokenStream) -> TokenStream {
    if input.to_string().contains("export default") {
        return quote! { const _: () = (); }.into();
    }

    let mut source = String::new();
    let mut bindings = Vec::new();
    render_tokens(input.into(), &mut source, &mut bindings);

    let source = normalize_source(&source);
    let binding_code = bindings.iter().map(|binding| {
        let name = &binding.name;
        let expression = &binding.expression;
        if binding.is_callback {
            quote! {
                __page.bind_callback(#name, #expression)?;
            }
        } else {
            quote! {
                __page.bind_value(#name, &(#expression))?;
            }
        }
    });

    quote! {{
        let mut __page = ::damascus_tsx::TsxPage::with_document(
            include_str!(concat!(env!("OUT_DIR"), "/damascus-tsx-index.html")),
            #source,
        );
        #(#binding_code)*
        ::std::result::Result::<_, ::damascus_tsx::TsxError>::Ok(__page)
    }}
    .into()
}

struct Binding {
    name: String,
    expression: TokenStream2,
    is_callback: bool,
}

fn render_tokens(tokens: TokenStream2, source: &mut String, bindings: &mut Vec<Binding>) {
    let tokens: Vec<TokenTree> = tokens.into_iter().collect();
    let mut index = 0;
    while index < tokens.len() {
        if is_dollar(&tokens[index])
            && let Some(TokenTree::Group(group)) = tokens.get(index + 1)
            && group.delimiter() == Delimiter::Brace
        {
            let name = binding_name(source, bindings.len());
            let expression = group.stream();
            let is_callback = name.starts_with("on_");
            source.push_str(&format!("{{{{damascus:{name}}}}}"));
            bindings.push(Binding {
                name,
                expression,
                is_callback,
            });
            index += 2;
            continue;
        }

        render_token(&tokens[index], source, bindings);
        index += 1;
    }
}

fn render_token(token: &TokenTree, source: &mut String, bindings: &mut Vec<Binding>) {
    match token {
        TokenTree::Group(group) => {
            let (open, close) = match group.delimiter() {
                Delimiter::Parenthesis => ('(', ')'),
                Delimiter::Brace => ('{', '}'),
                Delimiter::Bracket => ('[', ']'),
                Delimiter::None => (' ', ' '),
            };
            if open != ' ' {
                source.push(open);
            }
            render_tokens(group.stream(), source, bindings);
            if close != ' ' {
                source.push(close);
            }
        }
        TokenTree::Ident(ident) => append_word(source, ident.to_string()),
        TokenTree::Literal(literal) => append_word(source, literal.to_string()),
        TokenTree::Punct(punct) => source.push(punct.as_char()),
    }
}

fn append_word(source: &mut String, word: String) {
    if let Some(last) = source.chars().last()
        && (last.is_ascii_alphanumeric() || last == '_' || last == '"')
    {
        source.push(' ');
    }
    source.push_str(&word);
}

fn is_dollar(token: &TokenTree) -> bool {
    matches!(token, TokenTree::Punct(punct) if punct.as_char() == '$')
}

fn binding_name(source: &str, index: usize) -> String {
    let Some(open) = source.rfind('<') else {
        return format!("value_{index}");
    };
    if source[open..].contains('>') {
        return format!("value_{index}");
    }
    let Some(equal_relative) = source[open..].rfind('=') else {
        return format!("value_{index}");
    };
    let equal = open + equal_relative;
    let before = source[..equal].trim_end();
    let name: String = before
        .chars()
        .rev()
        .take_while(|character| character.is_ascii_alphanumeric() || *character == '_')
        .collect::<String>()
        .chars()
        .rev()
        .collect();
    if name.is_empty() {
        format!("value_{index}")
    } else {
        name
    }
}

// Keep this named helper so source normalization can evolve without changing
// the generated macro shape.
fn normalize_source(source: &str) -> String {
    source.trim().to_owned()
}
