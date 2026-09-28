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


## Autenticação local

A API usa `http://localhost:5080` e permite em Development apenas a origem `http://localhost:5173`, com credenciais. Para outra porta, ajuste `Frontend:Origin` em `appsettings.Development.json`. CORS não é aberto em produção.

Antes de qualquer operação POST/PUT/PATCH/DELETE em `/api`, o cliente obtém `GET /api/auth/csrf`. A resposta contém `token` e define o cookie HTTP-only `CAE-XSRF`; envie o token retornado no cabeçalho `X-CSRF-TOKEN`. O cliente React mantém o token somente em memória e envia cookies com `credentials: include`. Após login, cadastro, ativação ou logout, ele solicita outro token, pois o antiforgery associa o token à identidade atual.

Em Development, as chaves de cookies e CSRF ficam apenas na memória; reiniciar a API invalida as sessões locais. Fora de Development, configure armazenamento persistente e compartilhado de chaves para a implantação.

A sessão usa o cookie HTTP-only `CAE.Auth`, `SameSite=Strict` e expira em oito horas. Em HTTP de Development, `Secure` segue o esquema da requisição; fora de Development é sempre habilitado. Endpoints disponíveis: `POST /api/auth/teachers/register`, `POST /api/auth/teachers/login`, `POST /api/auth/students/activate`, `POST /api/auth/students/login`, `POST /api/auth/logout` e `GET /api/auth/me`.

Os testes HTTP xUnit usam um banco PostgreSQL exclusivo chamado `trabalho_cae_auth_test`, separado do banco local do produto. Crie-o uma vez com PostgreSQL iniciado:

~~~bash
docker compose exec postgres psql -U trabalho_cae -d postgres -c "CREATE DATABASE trabalho_cae_auth_test;"
~~~

O teste aplica as migrations e limpa as tabelas desse banco de teste. Nunca configure `STUDYPLATFORM_TEST_CONNECTION` para o banco normal de desenvolvimento.
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

## Gestão de conteúdo pedagógico

A Etapa 5 usa rotas aninhadas sob `/api/modules`. Conceitos e todas as suas keywords, pistas, pré-requisitos e atividades são criados/editados como um único recurso agregado em `POST/PUT /api/modules/{moduleId}/concepts/{conceptId}`. Isso mantém a gravação coerente e dá ao frontend um contrato previsível. Atividades omitidas em uma edição são desativadas para preservar tentativas; keywords e pistas são atualizadas pela lista e posição, respectivamente.

Rotas adicionais: `GET/POST /api/modules`, `GET/PUT /api/modules/{id}`, `POST /api/modules/{id}/duplicate|publish|archive`, `GET /api/modules/{id}/publication-validation`, `GET /api/modules/{moduleId}/concepts`, `GET /api/modules/{moduleId}/concepts/{conceptId}`, `POST /api/modules/{moduleId}/concepts/{conceptId}/duplicate|deactivate`.

## Intercâmbio de módulos JSON

O formato de intercâmbio atual usa `schemaVersion: 1`; um exemplo estrutural está em `docs/module-exchange-v1.example.json`. Ele contém um objeto `module` e conceitos identificados por `externalId`; pré-requisitos referenciam esses IDs externos. IDs internos, proprietário, turmas e dados de aprendizagem não fazem parte do arquivo.

O backend inclui conteúdo e atividades ativos ou inativos na exportação para preservar o material. A importação aceita JSON (`POST /api/modules/import`, com `Content-Type: application/json`) e a exportação baixa JSON em `GET /api/modules/{id}/export`. O arquivo deve ter até 1 MiB e propriedades fora do schema são rejeitadas. Versões de schema desconhecidas são rejeitadas. Toda importação pertence ao professor autenticado e começa em `DRAFT`; o conteúdo pode precisar de ajustes antes da publicação, que usa a validação da Etapa 5.

## Turmas e alunos

Na Etapa 7, a gestão usa as rotas `/api/classrooms` e recursos aninhados `/api/classrooms/{id}/modules` e `/api/classrooms/{id}/students`. Apenas professores podem usá-las. O código da turma é normalizado para maiúsculas e aceita de 3 a 32 caracteres alfanuméricos, com hífens somente entre grupos (por exemplo, `TURMA-A`).

O cadastro e a redefinição retornam o código temporário uma única vez. Ele possui seis caracteres do alfabeto `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`, gerados por `RandomNumberGenerator`, e expira em sete dias. O banco conserva somente o hash produzido pelo `PasswordHasher<Student>`. Após reset, a senha anterior deixa de funcionar; a ativação da Etapa 4 consome o código novo.

## Importação de alunos por CSV (Etapa 8)

Na tela da turma, selecione um arquivo de até 1 MiB e analise-o antes de confirmar. O formato UTF-8 aceita BOM, CRLF/LF e campos CSV entre aspas; o cabeçalho obrigatório é matricula, e nome é opcional. Colunas desconhecidas ou repetidas são rejeitadas. Matrículas preservam zeros à esquerda e seguem a mesma comparação exata do cadastro manual.

Exemplo:

    matricula,nome
    12345,João
    12346,Maria
    12347,

O preview não grava dados nem gera códigos. A confirmação reenvia e revalida o mesmo arquivo; linhas inválidas, duplicadas no arquivo ou já cadastradas são ignoradas, enquanto as demais podem ser importadas. O limite é de 2.000 linhas de dados. Após a confirmação, o navegador baixa imediatamente matricula,nome,codigo_temporario apenas para alunos criados. Códigos não são recuperáveis depois; em caso de perda, redefina o acesso do aluno.

Endpoints do fluxo: POST /api/classrooms/{classroomId}/students/import/preview e POST /api/classrooms/{classroomId}/students/import/confirm. Ambos exigem sessão de professor e token CSRF.
## Motor pedagógico e FSRS (Etapa 9)

O backend cria `StudentConceptState` sob demanda; conceitos sem registro são tratados como `NEW`. A progressão define como estudar (`NEW → EXPOSURE → RECOGNITION → GUIDED_RECALL → FREE_RECALL → MASTERED`), enquanto FSRS define quando revisar. Conceitos ativos só podem ser introduzidos quando o módulo está publicado, associado à turma ativa do aluno e todos os pré-requisitos estão `MASTERED`.

A integração usa `Fsrs.Sharp` 2.0.0 (FSRS-6, licença MIT) exclusivamente dentro de `FsrsService`. O estado de memória usa dificuldade, estabilidade, vencimento, última revisão, elapsed/scheduled days, repetições, lapses e estado/step FSRS serializado na coluna existente `FsrsState`. A sequência padrão da biblioteca mantém intervalos curtos de aprendizagem; fuzzing está desativado para scheduling reproduzível. `MASTERED` continua recebendo revisões; acerto preserva o estado e erro regressa para `FREE_RECALL`.

Para chegar a `MASTERED`, o primeiro acerto de `FREE_RECALL` inicia a sequência válida e agenda revisão; o segundo só conta em outra sessão quando o vencimento anterior já chegou. Erro em `FREE_RECALL` regressa para `GUIDED_RECALL` e zera contador e sessão da sequência válida, mantendo os horários históricos de sucesso. Rating interno: erro → `AGAIN`; acerto com tentativa extra ou ao menos duas pistas → `HARD`; acerto limpo em até 5 segundos → `EASY`; os demais acertos → `GOOD`. Tempo sozinho não transforma acerto em erro nem em `HARD`.

Execute os testes do motor com `dotnet test backend/Tests/StudyPlatform.Tests/StudyPlatform.Tests.csproj`. Os testes de persistência usam exclusivamente o banco `trabalho_cae_learning_test` e conferem o nome antes da limpeza.

## Sessões e correção de atividades (Etapa 10)

O backend disponibiliza sessões apenas para alunos ativos e autenticados, com turma ativa e módulo publicado associado à turma. `POST /api/student/modules/{moduleId}/sessions` inicia ou retoma a única sessão ativa do aluno para aquele módulo. `GET /api/student/sessions/{sessionId}` e `/next` recuperam a apresentação atual; `POST /api/student/sessions/{sessionId}/activities/{presentationId}/reveal` revela a próxima palavra-chave/pista; `POST /api/student/sessions/{sessionId}/answer` corrige e registra uma resposta; `POST /api/student/sessions/{sessionId}/abandon` abandona sem apagar tentativas.

A seleção é incremental: cada resposta persiste tentativa e progressão antes de selecionar a seguinte. `TotalActivities` conta apresentações entregues até aquele momento (incluindo a atual); a sessão termina ao atingir 10 ou quando não há outra atividade compatível e ainda não usada. O serviço prioriza revisões vencidas, erros recentes (janela de 14 dias), conceitos em andamento e conceitos novos elegíveis; desempates usam estado/tipo principal e IDs estáveis. Não há repetição de um mesmo cartão na sessão.

Uma apresentação persistida guarda o snapshot privado usado para correção e a API projeta somente os campos públicos. A tentativa aponta para essa apresentação por uma referência única; locks transacionais do PostgreSQL serializam respostas concorrentes. Respostas repetidas recebem conflito HTTP 409 e não avançam o estado novamente. Todas as mutações exigem o token CSRF existente (`X-CSRF-TOKEN`). Os testes usam exclusivamente `trabalho_cae_learning_test`.

## Área de estudo do aluno

Após entrar como aluno, a área `/student` lista somente módulos publicados associados à turma do aluno. Cada módulo mostra título, disciplina e descrição; o resumo permite iniciar ou retomar uma sessão ativa. As rotas usadas são `/student`, `/student/modules/:moduleId` e `/student/sessions/:sessionId`.

A sessão usa a atividade e o timestamp retornados pela API. Os cinco minigames ficam isolados em `frontend/src/minigames/`; a API corrige respostas e persiste revelações. Para testar uma sessão, ative um aluno, associe à turma um módulo publicado com conceitos e atividades válidas, entre como aluno e escolha o módulo. Os jogos exibidos seguem a elegibilidade pedagógica, portanto nem todos precisam aparecer na mesma sessão.
