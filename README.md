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

A autenticação usa cookies HTTP-only CAE.Auth e antiforgery CAE-XSRF, com token CSRF enviado em X-CSRF-TOKEN. Em Development, as chaves de Data Protection são efêmeras: reiniciar a API invalida cookies locais. Se a sessão antiga causar erro, limpe os cookies CAE.Auth e CAE-XSRF de localhost e entre novamente. Fora de Development, cookies usam Secure; configure armazenamento persistente e compartilhado de chaves de Data Protection antes de executar múltiplas instâncias.

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

Não há seed de demonstração, recuperação por e-mail, chaves de cookie persistidas em Development ou inspeção automática entre navegadores. Não são parte do MVP cruzadinha, matching, simulações/diagramas, ranking, gamificação, analytics avançado, notificações, modo offline/PWA, compartilhamento/biblioteca pública ou IA. Consulte docs/manual-validation.md para a sequência de verificação manual, incluindo desktop e celular.
