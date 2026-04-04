## Структура проекта

| Папка / область | Назначение |
|-----------------|------------|
| `Controllers/` | HTTP API (`Auth`, `Users`, `Files`). |
| `Services/` | Бизнес-логика: аутентификация, JWT, поиск пользователей, MinIO. |
| `Database/` | Фабрика подключений MySQL и расширение `WithConnectionAsync` (открытие соединения, `Dispose`). |
| `Models/Dtos/` | Контракты запросов/ответов API. |
| `Options/` | Привязка секций `appsettings` к классам (`Jwt`, `MinIO`). |
| `Database/Migrations/` | SQL для инициализации схемы (при старте контейнера MySQL через `docker-compose`). |

## Запуск

1. Поднять MySQL и MinIO (из каталога `MINMs.Server`):

   ```bash
   docker compose up -d
   ```

   Скрипты из `Database/Migrations` монтируются в `docker-entrypoint-initdb.d` и выполняются при первом создании тома БД.

2. Запустить API (корень решения или папка сервера):

   ```bash
   dotnet run --project MINMs.Server
   ```

## Как устроен код

- **Аутентификация:** `AuthService` пишет/читает таблицу `users`, пароли — BCrypt. `JwtTokenService` формирует access token с клеймами пользователя.
- **Доступ к БД:** все запросы идут через `IDbConnectionFactory` + `WithConnectionAsync`, чтобы соединение всегда закрывалось после операции.
- **Файлы:** `MinioStorageService` оборачивает MinIO SDK (загрузка, presigned URL для скачивания).