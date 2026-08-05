use serde::{Deserialize, Serialize};
use std::sync::{Arc, Mutex};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Todo {
    pub id: usize,
    pub title: String,
    pub done: bool,
}

#[derive(Debug, Deserialize)]
pub struct CreateTodo {
    pub title: String,
}

#[derive(Debug, Deserialize)]
pub struct UpdateTodo {
    pub title: Option<String>,
    pub done: Option<bool>,
}

pub type Db = Arc<Mutex<Vec<Todo>>>;

pub fn init_db() -> Db {
    Arc::new(Mutex::new(vec![
        Todo {
            id: 1,
            title: "Learn DamascusUI".into(),
            done: false,
        },
        Todo {
            id: 2,
            title: "Build something cool".into(),
            done: false,
        },
    ]))
}
