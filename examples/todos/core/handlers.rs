use damascus::prelude::*;

use crate::models::{CreateTodo, Db, Todo, UpdateTodo};

pub async fn list(Extension(db): Extension<Db>) -> impl IntoResponse {
    Json(db.lock().unwrap().clone())
}

pub async fn create(
    Extension(db): Extension<Db>,
    Json(input): Json<CreateTodo>,
) -> impl IntoResponse {
    let mut todos = db.lock().unwrap();
    let id = todos.last().map(|t| t.id + 1).unwrap_or(1);
    let todo = Todo {
        id,
        title: input.title,
        done: false,
    };
    todos.push(todo.clone());
    (StatusCode::CREATED, Json(todo))
}

pub async fn update(
    Extension(db): Extension<Db>,
    Path(id): Path<usize>,
    Json(input): Json<UpdateTodo>,
) -> impl IntoResponse {
    let mut todos = db.lock().unwrap();
    let Some(todo) = todos.iter_mut().find(|t| t.id == id) else {
        return Err(StatusCode::NOT_FOUND);
    };
    if let Some(title) = input.title {
        todo.title = title;
    }
    if let Some(done) = input.done {
        todo.done = done;
    }
    Ok(Json(todo.clone()))
}

pub async fn delete(Extension(db): Extension<Db>, Path(id): Path<usize>) -> impl IntoResponse {
    let mut todos = db.lock().unwrap();
    if todos.iter().any(|t| t.id == id) {
        todos.retain(|t| t.id != id);
        StatusCode::NO_CONTENT
    } else {
        StatusCode::NOT_FOUND
    }
}
