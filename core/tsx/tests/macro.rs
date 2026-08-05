use std::convert::Infallible;

use damascus_tsx::{TsxPage, tsx};
use serde::Serialize;

#[derive(Debug, Serialize)]
struct Todo {
    title: String,
}

#[test]
fn expands_tsx_values_and_callbacks() -> Result<(), Box<dyn std::error::Error>> {
    let title = "Todos";
    let todos = vec![Todo {
        title: "Learn TSX macros".into(),
    }];

    let page: TsxPage = tsx! {
        <App
            title=${title}
            todos=${todos}
            on_click=${|| async { Ok::<_, Infallible>(true) }}
        />
    }
    .unwrap();

    assert!(page.source().contains("{{damascus:title}}"));
    assert!(page.source().contains("{{damascus:todos}}"));
    assert!(page.source().contains("{{damascus:on_click}}"));
    assert_eq!(page.values()["title"], "Todos");
    assert_eq!(page.values()["todos"][0]["title"], "Learn TSX macros");
    assert_eq!(page.callback_names().collect::<Vec<_>>(), ["on_click"]);
    Ok(())
}

#[test]
fn names_text_values_after_attributes_without_collisions() -> Result<(), Box<dyn std::error::Error>>
{
    let page: TsxPage = tsx! {
        <main class="page">
            <h1>${"Title"}</h1>
            <p>${"Message"}</p>
        </main>
    }
    .unwrap();

    assert_eq!(page.values()["value_0"], "Title");
    assert_eq!(page.values()["value_1"], "Message");
    Ok(())
}
