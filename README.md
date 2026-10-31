# Plataforma Educacional de Repetição Espaçada

MVP web para professores criarem módulos de estudo e acompanharem a aprendizagem individual dos alunos por recuperação ativa e repetição espaçada. A especificação do produto está em [SPEC.md](SPEC.md); o formato JSON de intercâmbio tem um exemplo em [docs/module-exchange-v1.example.json](docs/module-exchange-v1.example.json).

## Escopo e arquitetura

O MVP inclui cadastro/login de professor e aluno, módulos e conceitos, pré-requisitos, importação/exportação JSON, turmas e matrículas manuais ou CSV, códigos temporários, sessões de estudo, histórico de tentativas, cinco minigames e acompanhamento básico do professor. Não inclui recursos pós-MVP como ranking, moedas, notificações, offline/PWA, biblioteca pública, IA ou analytics avançado.

\`\`\`text
React + TypeScript + Vite
        ↓ HTTP/JSON
ASP.NET Core 10 → Services → EF Core → PostgreSQL
\`\`\`

O frontend cuida da interface e da interação. O backend é a autoridade para autorização, correção, seleção de atividades, progressão pedagógica, pré-requisitos e agendamento.

| Parte | Tecnologias principais |
|---|---|
| Frontend | React 19, TypeScript, Vite, CSS Modules, Vitest |
| Backend | C#, ASP.NET Core/.NET 10, EF Core 10, xUnit |
| Dados | PostgreSQL 17, Npgsql, migrations EF Core |
| Repetição espaçada | Fsrs.Sharp 2.0.0, FSRS-6, encapsulado em FsrsService |

Estrutura: frontend/src contém páginas, serviços, layouts e minigames; backend/StudyPlatform.Api contém controllers, DTOs, domínio, serviços, dados e migrations; backend/Tests/StudyPlatform.Tests contém testes; docs/ contém exemplos e checklist manual.

## Pré-requisitos e portas

- Git;
- .NET SDK 10;
- Node.js compatível com Vite 7 e npm;
- Docker Desktop com Docker Compose.

Portas locais: frontend 5173, API 5080, PostgreSQL 5432. O CORS em Development permite somente a origem configurada em Frontend:Origin, por padrão http://localhost:5173. Use --strictPort no Vite para ele não trocar silenciosamente para outra porta.

## Primeira execução no Windows/PowerShell

Execute na raiz do repositório. Em um terminal, inicie o PostgreSQL e aplique o schema:

\`\`\`powershell
docker compose up -d postgres
docker compose ps
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet ef database update --project backend/StudyPlatform.Api/StudyPlatform.Api.csproj --startup-project backend/StudyPlatform.Api/StudyPlatform.Api.csproj
\`\`\`

Se dotnet-ef 10.0.0 já estiver instalado, pule a instalação. O Compose cria o banco local trabalho_cae e persiste os dados no volume postgres_data. A API não aplica migrations automaticamente: rode database update antes da primeira inicialização e após receber migrations novas.

Inicie a API em outro terminal, também na raiz:

\`\`\`powershell
dotnet run --project backend/StudyPlatform.Api/StudyPlatform.Api.csproj
\`\`\`

Inicie o frontend em um terceiro terminal:

\`\`\`powershell
Set-Location frontend
npm install
npm run dev -- --port 5173 --strictPort
\`\`\`

Abra http://localhost:5173. Crie uma conta de professor na tela inicial; não há usuário demo nem seed automático. O fluxo rápido é: professor cria módulo e turma, cadastra aluno e associa o módulo; aluno ativa a conta com o código temporário, escolhe o módulo e estuda; professor acompanha o resumo na turma.

## Configuração local e segurança

Os valores de appsettings.Development.json e docker-compose.yml são credenciais locais de desenvolvimento, não segredos de produção. Não os reutilize fora do ambiente local. Sobrescreva a conexão da API com a variável ConnectionStrings__DefaultConnection; o frontend aceita VITE_API_BASE_URL e, por padrão, chama http://localhost:5080. Para trocar a origem do frontend em Development, ajuste Frontend:Origin em appsettings.Development.json. Não habilite CORS amplo para contornar erro de porta.

Não há arquivo .env necessário. Arquivos .env locais são ignorados pelo Git. Configure credenciais reais por mecanismo de secrets/environment próprio do ambiente de implantação; não as grave no repositório ou nos logs. Senhas e códigos temporários são armazenados no banco como hashes.

A autenticação usa cookies HTTP-only CAE.Auth e antiforgery CAE-XSRF, com token CSRF enviado em X-CSRF-TOKEN. Em Development, as chaves de Data Protection são efêmeras: reiniciar a API invalida cookies locais. Se a sessão antiga causar erro, limpe os cookies CAE.Auth e CAE-XSRF de localhost e entre novamente. Em produção, cookies usam Secure e as chaves de Data Protection são compartilhadas no PostgreSQL.

GET http://localhost:5080/health retorna estado do processo e se a connection string está configurada (databaseConfigured). Não executa consulta ao PostgreSQL; o health endpoint não é um teste de disponibilidade do banco. A aplicação deve ser iniciada com PostgreSQL acessível e migrations aplicadas.

## Banco, migrations e testes

As migrations estão em backend/StudyPlatform.Api/Migrations. Para aplicar alterações futuras, use dotnet ef database update com os argumentos --project e --startup-project acima. A CLI precisa corresponder ao EF Core 10.0.0. O repositório não inclui manifesto de ferramenta; a instalação global mostrada na primeira execução é a opção documentada.

Os testes de integração usam bancos separados e truncam suas tabelas. Crie uma vez os bancos ausentes antes de executar a suíte; este comando PowerShell só cria os que ainda não existirem:

\`\`\`powershell
$testDatabases = @('trabalho_cae_auth_test', 'trabalho_cae_classrooms_test', 'trabalho_cae_modules_test', 'trabalho_cae_student_csv_test', 'trabalho_cae_learning_test')
foreach ($database in $testDatabases) {
  $exists = docker compose exec -T postgres psql -U trabalho_cae -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$database'"
  if ($exists.Trim() -ne '1') { docker compose exec -T postgres createdb -U trabalho_cae $database }
}
\`\`\`

As factories conferem o nome permitido antes de limpar; não aponte variáveis STUDYPLATFORM_*_TEST_CONNECTION para trabalho_cae. A conexão padrão de cada factory já seleciona seu banco de teste.

Na raiz, os comandos de backend são:

\`\`\`powershell
dotnet restore backend/Tests/StudyPlatform.Tests/StudyPlatform.Tests.csproj
dotnet build backend/Tests/StudyPlatform.Tests/StudyPlatform.Tests.csproj
dotnet build -c Release backend/Tests/StudyPlatform.Tests/StudyPlatform.Tests.csproj
dotnet test backend/Tests/StudyPlatform.Tests/StudyPlatform.Tests.csproj
\`\`\`

No terminal do frontend (frontend/):

\`\`\`powershell
npm install
npm test -- --run
npm run build
\`\`\`

## Fluxos do MVP

### Professor, módulos e intercâmbio JSON

O cadastro de professor começa pela tela inicial. Na área do professor, use Dashboard, Módulos, Turmas e Conta. Um módulo pode ser editado, duplicado, publicado e arquivado; publicação valida conceitos, atividades e pré-requisitos. A edição de módulo publicado vale imediatamente para as turmas associadas.

Importe o JSON oficial pela tela de módulos e exporte um módulo já existente para backup. O schema atual usa schemaVersion: 1; consulte o exemplo ligado no início deste README. O arquivo é limitado a 1 MiB, campos fora do schema são rejeitados e a importação é atômica. Referências externas e ciclos são validados; a importação cria um módulo DRAFT pertencente ao professor atual.

### Turmas, alunos, CSV e códigos temporários

O código da turma é globalmente único sem distinção de maiúsculas/minúsculas. Matrículas são únicas dentro da turma e preservam zeros à esquerda. O professor pode cadastrar um aluno manualmente ou fazer preview e confirmar um CSV UTF-8 de até 1 MiB e 2.000 linhas. Nome é opcional. Linhas inválidas, duplicadas no arquivo ou já cadastradas são ignoradas na confirmação; as válidas são persistidas.

Ao criar ou redefinir acesso, o código temporário é exibido/baixado apenas naquele momento, possui seis caracteres e expira em sete dias. Após cinco tentativas inválidas, é necessário redefini-lo. O banco mantém apenas o hash; ativação consome o código e define uma senha. O CSV de credenciais baixado na confirmação é a única cópia em texto puro; códigos antigos não podem ser recuperados.

### Aprendizagem, sessões e FSRS

O backend evolui conceitos por NEW → EXPOSURE → RECOGNITION → GUIDED_RECALL → FREE_RECALL → MASTERED, com regressões em erros. Todos os pré-requisitos devem estar MASTERED antes da liberação. O estado pedagógico decide como estudar; FsrsService usa FSRS-6 para decidir quando revisar. Um conceito dominado continua em manutenção. Para dominar, dois acertos de evocação livre devem ocorrer em sessões diferentes e após o vencimento de uma revisão real.

Uma sessão tem alvo de dez atividades, mas termina antes se não houver mais conteúdo elegível sem repetição forçada. Cada resposta é corrigida e persistida pelo backend; sessões ativas podem ser retomadas e sessões abandonadas preservam tentativas. Os cinco minigames são Exposure, True/False, Fill Blank, Guess Concept e Ordering. Ordenação é complementar e não obrigatória para publicar. O backend só revela keywords/pistas quando solicitado e permanece autoridade sobre respostas corretas e progresso.

### Acompanhamento do professor

O dashboard conta módulos não arquivados do professor, turmas ativas e alunos ativos das turmas do professor. A tabela da turma apresenta matrícula, nome, progresso, revisões pendentes e último acesso; progresso usa conceitos ativos de módulos publicados associados. A migration AddStudentLastAccessAtUtc registra o último acesso autenticado do aluno. Professor só consulta seus próprios dados; endpoints de relatório exigem papel TEACHER.

## Limitações e validação manual

Recursos adicionais de integração: módulos podem habilitar opcionalmente cruzadinha (com palavras limitadas a conceitos dominados ou a todos os conceitos ativos) e permitir adiantar revisões; as páginas de acompanhamento exibem estatísticas agregadas nos períodos de 7, 30 ou 90 dias. Arrastar itens no preenchimento e na ordenação é suportado por mouse, toque e teclado.

Não há seed de demonstração, recuperação por e-mail, chaves de cookie persistidas em Development ou inspeção automática entre navegadores. Não são parte do MVP cruzadinha, matching, simulações/diagramas, ranking, gamificação, analytics avançado, notificações, modo offline/PWA, compartilhamento/biblioteca pública ou IA. Consulte docs/manual-validation.md para a sequência de verificação manual, incluindo desktop e celular.

## PERE e área do professor

A interface usa a identidade PERE (Plataforma Educacional de Repetição Espaçada). Professores acessam Página inicial, Dashboard, Módulos e Turmas pela navegação superior; o menu do perfil reúne Meu perfil e Sair. Em Meu perfil é possível atualizar nome, formação e instituição, alterar a senha e enviar/remover uma foto PNG, JPEG ou WebP de até 2 MiB. A foto é redimensionada no navegador antes do envio e fica associada à conta do professor. O dashboard reúne filtros e gráficos por turma; detalhes do progresso continuam protegidos por propriedade e papel.

## Deploy na Vercel (Container Runtime)

O deploy usa um único projeto Vercel na raiz, com frontend nginx e API ASP.NET Core em containers separados. Os rewrites mantêm ambos no mesmo domínio; `/api/*` segue para a API. O banco de produção é externo (Neon); o PostgreSQL do Docker Compose continua exclusivo para desenvolvimento.

### Pré-requisitos e banco

- Conta Vercel com suporte a Container Runtime no plano Hobby;
- projeto conectado ao repositório, com Docker disponível no deploy;
- banco PostgreSQL Neon provisionado pela aba **Storage/Marketplace** da Vercel.

No Neon, obtenha as duas connection strings com SSL. Cadastre no projeto Vercel, para os ambientes Production e Preview conforme desejado:

- `ConnectionStrings__DefaultConnection`: string pooled do Neon para a API em runtime, no formato `Host=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true`;
- `ASPNETCORE_ENVIRONMENT`: `Production`.

Não há outras variáveis de configuração obrigatórias no backend. O frontend de produção compila com `VITE_API_BASE_URL` vazio para usar URLs relativas `/api/...`; não cadastre uma URL localhost. Se o pooler reportar problemas com prepared statements, acrescente `Max Auto Prepare=0` à string pooled. Nunca grave credenciais reais no repositório.

Antes do primeiro deploy, aplique as migrations usando a string **direta** do Neon (hostname sem `-pooler`), em ambiente seguro com o SDK .NET 10 e `dotnet-ef` 10.0.0 instalados:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true'
dotnet ef database update --project backend/StudyPlatform.Api/StudyPlatform.Api.csproj --startup-project backend/StudyPlatform.Api/StudyPlatform.Api.csproj --connection $env:ConnectionStrings__DefaultConnection
```

A API não aplica migrations automaticamente. Execute o comando novamente quando publicar uma migration nova, sempre com a connection string direta.

### Publicar e validar

Vincule a raiz local ao projeto Vercel e publique:

```powershell
vercel link
vercel --prod
```

Também é possível publicar por push se o repositório estiver conectado ao projeto. A primeira conta de professor é criada pela tela pública de cadastro de professor na página inicial; depois, entre com o e-mail e senha cadastrados. Valide `https://<dominio>/api/health` (resposta `{"status":"ok"}`) e faça login pela interface para confirmar cookies e CSRF no mesmo domínio. Após cerca de cinco minutos sem tráfego, a primeira requisição pode demorar por cold start do container e reativação do banco.

### Validar imagens localmente

Na raiz do repositório, construa as imagens:

```powershell
docker build -f backend/Dockerfile -t pere-api backend
docker build -f frontend/Dockerfile --build-arg VITE_API_BASE_URL=http://localhost:5080 -t pere-frontend frontend
```

Rode a API na porta local 5080 e o frontend na 5173. Substitua a connection string por uma string de desenvolvimento segura já configurada no PostgreSQL local:

```powershell
docker run --rm --name pere-api -p 5080:80 -e PORT=80 -e ASPNETCORE_ENVIRONMENT=Development -e 'ConnectionStrings__DefaultConnection=Host=host.docker.internal;Port=5432;Database=trabalho_cae;Username=trabalho_cae;Password=<senha-local>' pere-api
docker run --rm --name pere-frontend -p 5173:80 -e PORT=80 pere-frontend
```

O argumento de build local aponta o frontend para a API publicada em `localhost:5080`; no deploy Vercel ele fica vazio e os rewrites mantêm o mesmo domínio. Depois de iniciar os dois containers, acesse o frontend em `http://localhost:5173`. Para o backend local, a origem permitida em Development é `http://localhost:5173`.
