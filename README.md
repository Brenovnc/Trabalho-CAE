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

A API usa ConnectionStrings:DefaultConnection. O perfil local carrega essa configuração de appsettings.Development.json; ela pode ser substituída pela variável de ambiente ConnectionStrings__DefaultConnection. O contexto do EF Core e a migration inicial InitialDomain estão configurados para PostgreSQL.

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

## Migrations e horários

Instale a ferramenta EF Core CLI compatível com a versão do projeto:

~~~bash
dotnet tool install --global dotnet-ef --version 10.0.0
~~~

Para criar uma migration após alterações futuras no modelo e aplicá-la:

~~~bash
dotnet ef migrations add NomeDaMigracao --project backend/StudyPlatform.Api/StudyPlatform.Api.csproj --output-dir Migrations
dotnet ef database update --project backend/StudyPlatform.Api/StudyPlatform.Api.csproj
~~~

Para reverter todas as migrations no banco local de desenvolvimento e remover a migration mais recente do projeto:

~~~bash
dotnet ef database update 0 --project backend/StudyPlatform.Api/StudyPlatform.Api.csproj
dotnet ef migrations remove --project backend/StudyPlatform.Api/StudyPlatform.Api.csproj
~~~

database update 0 remove as tabelas criadas pelas migrations e seus dados; use-o somente em um banco local descartável ou depois de fazer backup.

Os campos de data e hora do domínio são DateTime em UTC, identificados pelo sufixo Utc, e são mapeados para timestamp with time zone no PostgreSQL. Crie e atualize esses valores com DateTime.UtcNow; o Npgsql espera valores UTC para esse tipo.

A migration usa uma coluna gerada lower(Code) e um índice único para o código da turma ser case-insensitive. EmailNormalized segue o mesmo mecanismo para tratar e-mails sem distinção de caixa. O limite de 32 caracteres para o código da turma é a decisão de tamanho máximo razoável adotada nesta implementação.

Cada tentativa exige um snapshot JSONB do conteúdo apresentado, para preservar o que o aluno viu mesmo se o conteúdo do conceito ou atividade for editado ou desativado.