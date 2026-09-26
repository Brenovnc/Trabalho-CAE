# Plataforma Educacional de Repetição Espaçada

Aplicação web educacional definida em [`SPEC.md`](SPEC.md). O repositório está sendo construído em etapas; esta etapa prepara somente a infraestrutura inicial.

## Stack

- Frontend: React, TypeScript, Vite, CSS Modules e Vitest.
- Backend: C#, ASP.NET Core, .NET 10, Entity Framework Core e xUnit.
- Banco local: PostgreSQL via Docker Compose.

## Requisitos locais

- .NET SDK 10.
- Node.js e npm em versões compatíveis com o Vite configurado.
- Docker com Docker Compose.

## Iniciar o PostgreSQL

Na raiz do repositório:

```bash
docker compose up -d postgres
```

O banco fica disponível em `localhost:5432`. O usuário, banco e senha definidos no Compose são exclusivamente para desenvolvimento local; não os reutilize em produção. Os dados persistem no volume `postgres_data`.

## Iniciar o backend

Em um terminal:

```bash
cd backend/StudyPlatform.Api
dotnet run
```

A API usa `ConnectionStrings:DefaultConnection`. O perfil local carrega essa configuração de `appsettings.Development.json`; ela pode ser substituída pela variável de ambiente `ConnectionStrings__DefaultConnection`. O contexto do EF Core está configurado para PostgreSQL, mas ainda não há entidades nem migrations de domínio.

O endpoint `http://localhost:5080/health` confirma que a aplicação iniciou e que a configuração de conexão foi carregada. Nesta etapa ele não executa uma consulta ao banco.

## Iniciar o frontend

Em outro terminal:

```bash
cd frontend
npm install
npm run dev
```

Abra o endereço informado pelo Vite, normalmente `http://localhost:5173`.

## Executar os testes

Frontend:

```bash
cd frontend
npm test
```

Backend:

```bash
dotnet test backend/Tests/StudyPlatform.Tests/StudyPlatform.Tests.csproj
```

## Estrutura inicial

```text
.
├── backend/
│   ├── StudyPlatform.Api/
│   │   ├── Controllers/
│   │   ├── Data/
│   │   ├── DTOs/
│   │   ├── Domain/
│   │   ├── Exceptions/
│   │   ├── Migrations/
│   │   ├── Models/
│   │   ├── Repositories/
│   │   ├── Services/
│   │   └── Validators/
│   └── Tests/StudyPlatform.Tests/
├── frontend/src/
│   ├── assets/
│   ├── components/
│   ├── hooks/
│   ├── layouts/
│   ├── minigames/
│   ├── pages/
│   ├── routes/
│   ├── services/
│   ├── types/
│   └── utils/
├── docker-compose.yml
└── docs/
```
