# Plano de implementação do MVP

Este plano divide o MVP da especificação oficial (`SPEC.md`) em etapas incrementais. A lógica pedagógica, FSRS, progressão, autenticação e persistência permanecem no backend. Os minigames permanecem isolados em `frontend/src/minigames/` e tratam apenas da interação com uma atividade e do envio da resposta.

## Decisões oficiais incorporadas

- Atividades de ordenação são opcionais; a publicação de um módulo não depende delas.
- A compatibilidade principal é: `EXPOSURE` → `exposure`; `RECOGNITION` → `true-false`; `GUIDED_RECALL` → `fill-blank`; `FREE_RECALL` → `guess-concept`. `ordering` é complementar e pode ser usado em `RECOGNITION` ou `GUIDED_RECALL` quando houver atividade compatível.
- Dez atividades são o alvo de uma sessão, não uma quantidade mínima. Se houver menos atividades pedagogicamente elegíveis, a sessão termina com menos, sem inventar conteúdo ou forçar repetições.
- Os dois acertos de `FREE_RECALL` necessários para `MASTERED` devem ocorrer em sessões diferentes e separados por um agendamento real de revisão. Dois acertos na mesma sessão nunca satisfazem esse requisito.
- A importação CSV deve fazer parse e validação, apresentar pré-visualização e erros por linha, e, após confirmação, persistir somente as linhas válidas.
- O banco armazena apenas o hash dos códigos temporários. O texto puro existe somente durante a geração, quando também é produzido o CSV de credenciais. Códigos antigos não podem ser recuperados; o professor pode gerar um novo código.
- Conceitos e atividades usados por alunos não devem ser apagados fisicamente quando isso comprometer histórico. Usar desativação/soft delete e preservar tentativas, progresso e dados FSRS.
- A autenticação web deve preferir cookie HTTP-only, oferecer proteção CSRF, configurar `Secure`/`SameSite` conforme o ambiente e usar expiração de sessão aproximada de oito horas no MVP.
- Códigos temporários expiram após sete dias e permitem no máximo cinco tentativas inválidas; depois disso, o professor precisa gerar outro código.
- A implementação FSRS fica encapsulada em `FsrsService`. A biblioteca específica pode ser escolhida antes da Etapa 9, considerando compatibilidade com .NET, testabilidade e ausência de acoplamento do restante da aplicação.

## Etapas

### 1. Fundação do repositório

- **Objetivo:** preparar a estrutura mínima e os ambientes de execução locais.
- **Funcionalidades:** React, TypeScript, Vite e CSS Modules; ASP.NET Core com C# e .NET 10; PostgreSQL local via Docker Compose; documentação inicial de execução.
- **Arquivos/pastas principais:** `frontend/`, `backend/`, `docker-compose.yml`, `README.md`.
- **Dependências:** nenhuma.
- **Validação:** iniciar PostgreSQL, API e frontend localmente; confirmar que a API responde e a página inicial carrega.

### 2. Domínio e persistência

- **Objetivo:** estabelecer o modelo de dados e o acesso ao banco.
- **Funcionalidades:** entidades, enums, relacionamentos, índices, constraints, configuração do EF Core e migrations para PostgreSQL. Incluir desde o desenho do modelo o histórico que deve sobreviver à desativação de conceitos e atividades; não implementar ainda os fluxos completos do produto.
- **Arquivos/pastas principais:** `backend/Models/`, `backend/Domain/`, `backend/Data/`, `backend/Migrations/`.
- **Dependências:** Etapa 1.
- **Validação:** aplicar migrations em banco vazio e conferir constraints, incluindo matrícula única por turma e associação única módulo-turma.

### 3. Contratos da API e tratamento de erros

- **Objetivo:** padronizar a comunicação entre frontend e backend.
- **Funcionalidades:** DTOs específicos, validações de entrada, respostas de erro consistentes e middleware global de exceções.
- **Arquivos/pastas principais:** `backend/DTOs/`, `backend/Validators/`, `backend/Exceptions/`, `backend/Controllers/`.
- **Dependências:** Etapa 2.
- **Validação:** enviar requisições inválidas e conferir respostas previsíveis, sem expor entidades ou detalhes internos.

### 4. Autenticação e autorização

- **Objetivo:** permitir acesso seguro a professores e alunos.
- **Funcionalidades:** cadastro e login de professor; ativação e login de aluno; logout e consulta da identidade atual; autorização por papel e propriedade dos recursos. Preferir cookie HTTP-only, proteger operações contra CSRF, configurar `Secure`/`SameSite` conforme o ambiente e usar expiração aproximada de oito horas. Códigos temporários serão armazenados apenas como hash, expirarão em sete dias e serão bloqueados após cinco tentativas inválidas, exigindo regeneração pelo professor.
- **Arquivos/pastas principais:** `backend/Services/Auth/`, `backend/Controllers/`, `backend/DTOs/`; `frontend/src/pages/`, `frontend/src/services/`, `frontend/src/routes/`.
- **Dependências:** Etapas 2 e 3.
- **Validação:** verificar cadastro/login dos dois tipos de usuário, ativação de código válido uma única vez, expiração e limite de tentativas, proteção CSRF e bloqueio de acesso cruzado. Cobrir as regras críticas com xUnit.

### 5. Módulos, conceitos e pré-requisitos

- **Objetivo:** permitir ao professor criar e manter conteúdo pedagógico.
- **Funcionalidades:** CRUD e duplicação de módulos e conceitos; estados draft/published/archived; desativação de conceitos e atividades utilizados; validação de publicação; validação de referências e detecção de ciclos. Ordenação pode estar ausente sem impedir publicação.
- **Arquivos/pastas principais:** `backend/Services/Modules/`, `backend/Domain/`, `backend/Controllers/`; `frontend/src/pages/modules/`, `frontend/src/components/`.
- **Dependências:** Etapas 2–4.
- **Validação:** criar, editar e duplicar conteúdo; publicar conteúdo válido; rejeitar conteúdo inválido, referências inexistentes ou ciclos; confirmar que ordenação é opcional e que desativação preserva histórico. Cobrir regras com xUnit.

### 6. Importação e exportação JSON

- **Objetivo:** permitir transportar módulos sem comprometer a integridade dos dados.
- **Funcionalidades:** schema oficial, validação completa e importação atômica; exportação reutilizável com IDs externos e pré-requisitos.
- **Arquivos/pastas principais:** `backend/Services/Modules/`, `backend/Validators/`, `backend/Controllers/`; `frontend/src/services/` e páginas de módulos.
- **Dependências:** Etapa 5.
- **Validação:** importar JSON válido e reexportar; rejeitar schema inválido, referência inexistente ou ciclo sem persistência parcial. Cobrir com xUnit.

### 7. Turmas, módulos associados e alunos

- **Objetivo:** organizar módulos por turma e criar acessos individuais.
- **Funcionalidades:** criar/arquivar turmas; associar/desassociar módulos; cadastrar alunos manualmente; redefinir acesso. Na geração de códigos temporários, mostrar/baixar o CSV de credenciais naquele momento; armazenar somente hashes e nunca recuperar códigos anteriores.
- **Arquivos/pastas principais:** `backend/Services/Classrooms/`, `backend/Services/Students/`, `backend/Controllers/`; `frontend/src/pages/classrooms/`.
- **Dependências:** Etapas 4 e 5.
- **Validação:** conferir código de turma único sem distinção de caixa, matrícula única por turma, autorização por propriedade e visibilidade correta dos módulos. Confirmar que o texto puro do código só é disponibilizado na geração e que regenerar invalida o anterior.

### 8. Importação de alunos por CSV

- **Objetivo:** cadastrar alunos em lote com tratamento claro de erros.
- **Funcionalidades:** parse, validação, pré-visualização, identificação de erros por linha e confirmação; persistir somente as linhas válidas, nunca persistir linhas inválidas; gerar códigos e CSV de credenciais no momento da confirmação.
- **Arquivos/pastas principais:** `backend/Services/Students/`, `backend/Validators/`, `backend/Controllers/`; `frontend/src/pages/classrooms/` e componentes de importação.
- **Dependências:** Etapa 7.
- **Validação:** importar CSV válido com nomes opcionais, duplicatas e erros por linha; confirmar que apenas linhas válidas são criadas após confirmação e que cada código pode ser obtido somente no momento de geração.

### 9. Motor pedagógico e FSRS

- **Objetivo:** implementar progressão individual, elegibilidade por pré-requisitos e agendamento de revisões.
- **Funcionalidades:** transições de estado, domínio e regressões; integração FSRS encapsulada em `FsrsService`; dois acertos de `FREE_RECALL` para domínio devem pertencer a sessões diferentes e estar separados por agendamento real de revisão. O FSRS não seleciona minigames. Compatibilidade principal: `EXPOSURE` → `exposure`, `RECOGNITION` → `true-false`, `GUIDED_RECALL` → `fill-blank`, `FREE_RECALL` → `guess-concept`; `ordering` é complementar em `RECOGNITION` ou `GUIDED_RECALL` se houver atividade compatível.
- **Arquivos/pastas principais:** `backend/Services/Learning/`, `backend/Domain/`, `backend/Data/`.
- **Dependências:** Etapas 2 e 5. Escolher biblioteca FSRS compatível com .NET, testável e isolada antes de implementar a integração.
- **Validação:** usar xUnit para verificar transições, regressões, domínio apenas após revisões válidas, pré-requisitos e agendamento FSRS, com relógio controlável nos testes.

### 10. Sessões e correção de atividades

- **Objetivo:** selecionar e corrigir atividades mantendo o backend como autoridade.
- **Funcionalidades:** sessões com alvo de dez atividades; encerrar com menos quando não houver dez atividades pedagogicamente elegíveis, sem inventar conteúdo ou forçar repetição. Aplicar compatibilidade de estado/atividade definida na Etapa 9; evitar repetição desnecessária; corrigir respostas no backend; persistir cada tentativa e atualizar progresso e FSRS.
- **Arquivos/pastas principais:** `backend/Services/Learning/`, `backend/Controllers/`, `backend/DTOs/`; entidades `StudySession` e `ActivityAttempt`.
- **Dependências:** Etapas 5 e 9.
- **Validação:** percorrer sessões completas e curtas; confirmar prioridade de revisões vencidas, exclusão de conceitos bloqueados, ausência de repetição forçada e persistência imediata inclusive em sessão interrompida. Cobrir seleção com xUnit.

### 11. Experiência de estudo do aluno

- **Objetivo:** permitir ao aluno estudar com feedback claro e interface acessível.
- **Funcionalidades:** lista de módulos, resumo, sessão, cronômetro visual sem timeout, feedback e tela final. Criar os cinco minigames como componentes de interação que recebem atividade e enviam resposta, sem autenticação, persistência ou regras pedagógicas próprias.
- **Arquivos/pastas principais:** `frontend/src/pages/`, `frontend/src/services/`, `frontend/src/types/`, `frontend/src/minigames/exposure/`, `true-false/`, `fill-blank/`, `guess-concept/`, `ordering/`.
- **Dependências:** Etapas 4 e 10.
- **Validação:** percorrer o fluxo aluno → módulo → sessão; verificar interação por mouse, teclado quando aplicável e toque; testar componentes e utilitários com Vitest.

### 12. Interface e acompanhamento do professor

- **Objetivo:** completar as telas de gestão e acompanhamento básico.
- **Funcionalidades:** dashboard simples, gestão de módulos e turmas, tabela de progresso e detalhes do aluno; tema escuro, responsividade, loading e estados vazios.
- **Arquivos/pastas principais:** `frontend/src/pages/`, `frontend/src/components/`, `frontend/src/layouts/` e CSS Modules; endpoints de progresso no backend.
- **Dependências:** Etapas 5–11.
- **Validação:** comparar os dados exibidos com o histórico persistido; revisar visualização em mobile e desktop e testar componentes críticos com Vitest.

### 13. Integração e documentação do MVP

- **Objetivo:** preparar o MVP para execução local reproduzível e conferir os fluxos de aceitação.
- **Funcionalidades:** instruções de configuração, Docker, PostgreSQL, migrations, execução do backend/frontend e testes; percorrer os fluxos completos de professor, módulo, turma, aluno e estudo. Seed de desenvolvimento, se incluído, deve ser opcional e nunca automático em produção.
- **Arquivos/pastas principais:** `README.md`, `docs/`, projetos de frontend e backend.
- **Dependências:** Etapas anteriores.
- **Validação:** seguir o README em ambiente limpo, executar testes xUnit e Vitest e percorrer o fluxo integral de aceitação definido no `SPEC.md`.

## Bloqueios específicos da Etapa 1

Não há decisão pendente que bloqueie a Etapa 1. Ela pode ser iniciada quando houver solicitação explícita para começar a implementação. A escolha da biblioteca FSRS pertence à preparação da Etapa 9 e não bloqueia a fundação.
