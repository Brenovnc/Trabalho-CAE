# Plataforma Educacional de Repetição Espaçada  
## Especificação funcional e técnica do MVP

---

# 1. Visão geral

O sistema será uma **aplicação web educacional** voltada inicialmente ao ensino de **Redes de Computadores**.

A plataforma utilizará:

- recuperação ativa;
- repetição espaçada;
- progressão adaptativa;
- pré-requisitos entre conceitos;
- diferentes tipos de minigames;
- histórico individual de aprendizagem.

A experiência do aluno deve ser simples.

O aluno não deverá administrar manualmente níveis, intervalos de revisão ou quais conceitos precisa estudar.

O sistema deverá decidir automaticamente:

- quais conceitos apresentar;
- quando revisar cada conceito;
- qual tipo de atividade utilizar;
- quando liberar novos conceitos;
- quando considerar determinado conceito dominado.

O primeiro módulo desenvolvido será de **Redes de Computadores**, mas nenhuma regra estrutural do sistema deve depender especificamente dessa disciplina.

A arquitetura deve permitir posteriormente criar módulos de:

- programação;
- segurança da informação;
- banco de dados;
- sistemas operacionais;
- matemática;
- idiomas;
- outras disciplinas.

---

# 2. Objetivo do MVP

O MVP deve ser um sistema completamente funcional contendo:

1. cadastro e autenticação de professores;
2. criação e gerenciamento de módulos;
3. criação manual de conceitos;
4. importação de módulos através de JSON;
5. exportação de módulos através de JSON;
6. criação de turmas;
7. cadastro manual de alunos;
8. importação de alunos por CSV;
9. geração de códigos temporários de acesso;
10. autenticação dos alunos;
11. associação de módulos a turmas;
12. mecanismo de pré-requisitos;
13. progressão pedagógica individual por conceito;
14. repetição espaçada utilizando FSRS;
15. sessões automáticas de estudo;
16. cinco tipos de atividades/minigames;
17. armazenamento do histórico de respostas;
18. acompanhamento básico de progresso pelo professor;
19. interface responsiva para desktop e dispositivos móveis.

---

# 3. Fora do escopo do MVP

Não devem ser implementados inicialmente:

- aplicativo Android ou iOS nativo;
- cruzadinhas;-
- biblioteca pública de módulos;
- compartilhamento direto de módulos entre professores;
- rankings;
- gamificação com moedas;
- conquistas;
- avatar;
- chat;
- comunicação professor-aluno;
- notificações push;
- integração com e-mail;
- recuperação de senha por e-mail;
- inteligência artificial para geração automática de questões;
- multiplayer;
- sessões misturando diferentes módulos;
- configuração avançada do algoritmo FSRS;
- painel analítico complexo;
- gráficos avançados;
- permissões administrativas globais;
- preview especial do módulo como aluno.

Caso o professor queira testar um módulo, poderá criar uma matrícula de teste em uma turma.

---

# 4. Stack tecnológica

## 4.1 Frontend

Utilizar:

- React;
- TypeScript;
- Vite;
- CSS Modules.

Responsabilidades do frontend:

- renderização da interface;
- gerenciamento de estado visual;
- interação dos minigames;
- comunicação com a API;
- validações simples de formulário;
- feedback ao usuário.

O frontend **não deve ser responsável pela lógica definitiva de progresso do aluno**.

---

## 4.2 Backend

Utilizar:

- C#;
- ASP.NET Core;
- .NET 10;
- Entity Framework Core.

O backend será a autoridade do sistema para:

- autenticação;
- autorização;
- progresso;
- correção das atividades;
- estado pedagógico;
- pré-requisitos;
- sessões;
- FSRS;
- persistência;
- importações;
- validações críticas.

---

# 5. Banco de dados

Utilizar:

**PostgreSQL**

O acesso deverá ser feito principalmente através do:

**Entity Framework Core**

O sistema deve utilizar migrations para controle da estrutura do banco.

---

# 6. Infraestrutura local

Utilizar Docker Compose pelo menos para:

- PostgreSQL.

Estrutura desejada:

```text
docker-compose.yml
frontend/
backend/
```

O projeto deve possuir documentação suficiente no README para permitir:

```text
git clone
docker compose up
backend run
frontend run
```

sem configurações incomuns.

---

# 7. Testes

## Backend

Utilizar:

```text
xUnit
```

Priorizar testes de:

- progressão;
- pré-requisitos;
- importação JSON;
- detecção de dependências circulares;
- autenticação;
- seleção de atividades;
- atualização FSRS.

## Frontend

Utilizar:

```text
Vitest
```

Testes frontend podem focar inicialmente em:

- componentes críticos;
- utilidades;
- comportamento básico dos minigames.

---

# 8. Arquitetura geral

Não é necessário implementar MVC clássico no frontend.

O projeto deve utilizar uma arquitetura em camadas.

Fluxo principal:

```text
React
  ↓
HTTP / JSON
  ↓
ASP.NET Core Controllers
  ↓
Services
  ↓
Repositories / Entity Framework Core
  ↓
PostgreSQL
```

A lógica pedagógica deve permanecer concentrada no backend.

---

# 9. Estrutura de diretórios

## 9.1 Raiz

```text
/
├── frontend/
├── backend/
├── docker-compose.yml
├── README.md
└── docs/
```

---

# 10. Estrutura do frontend

```text
frontend/
└── src/
    ├── assets/
    ├── components/
    ├── hooks/
    ├── layouts/
    ├── pages/
    ├── routes/
    ├── services/
    ├── types/
    ├── utils/
    │
    └── minigames/
        ├── exposure/
        ├── true-false/
        ├── fill-blank/
        ├── guess-concept/
        └── ordering/
```

---

# 11. Regra estrutural dos minigames

Todos os minigames devem existir dentro de:

```text
src/minigames/
```

Cada minigame deve possuir sua própria subpasta.

Exemplo:

```text
src/minigames/fill-blank/
├── FillBlankGame.tsx
├── FillBlankGame.module.css
├── types.ts
├── utils.ts
└── index.ts
```

Nenhum minigame poderá implementar diretamente:

- autenticação;
- atualização de progresso;
- regras FSRS;
- persistência;
- pré-requisitos;
- regras de sessão.

Um minigame deve apenas:

1. receber uma atividade;
2. permitir interação;
3. produzir um resultado;
4. enviar esse resultado ao backend.

---

# 12. Estrutura do backend

```text
backend/
├── Controllers/
├── DTOs/
├── Models/
├── Domain/
├── Services/
│   ├── Auth/
│   ├── Modules/
│   ├── Classrooms/
│   ├── Students/
│   │
│   └── Learning/
│       ├── ProgressionService.cs
│       ├── ActivitySelectionService.cs
│       ├── PrerequisiteService.cs
│       ├── SessionService.cs
│       └── FsrsService.cs
│
├── Repositories/
├── Data/
├── Migrations/
├── Validators/
├── Exceptions/
└── Tests/
```

---

# 13. Motor pedagógico

Criar uma camada própria responsável pela aprendizagem.

Estrutura conceitual:

```text
Learning Engine
│
├── ProgressionService
├── ActivitySelectionService
├── PrerequisiteService
├── SessionService
└── FsrsService
```

Cada componente deve possuir uma responsabilidade clara.

---

# 14. ProgressionService

Responsável por determinar:

- estado atual do conceito;
- avanço de estado;
- regressão;
- domínio;
- resposta a erros.

O ProgressionService não deve decidir quando uma revisão ocorrerá.

Essa responsabilidade pertence ao FSRS.

---

# 15. FsrsService

Responsável exclusivamente por repetição espaçada.

O FSRS deverá determinar principalmente:

- quando revisar;
- dificuldade;
- estabilidade;
- próxima revisão;
- comportamento baseado no histórico.

O FSRS **não escolhe qual minigame utilizar**.

A implementação concreta do FSRS deve ficar encapsulada dentro do `FsrsService`, evitando espalhar dependências do algoritmo pelo restante da aplicação.

---

# 16. ActivitySelectionService

Responsável por escolher:

- conceito;
- atividade;
- minigame.

Deve considerar:

1. revisões vencidas;
2. erros recentes;
3. conceitos em progressão;
4. conceitos novos liberados;
5. estado pedagógico;
6. disponibilidade de atividades;
7. atividades já utilizadas naquela sessão.

A prioridade pedagógica deve ser superior à variedade visual.

---

# 17. PrerequisiteService

Responsável por:

- verificar pré-requisitos;
- liberar conceitos;
- detectar dependências inválidas;
- detectar ciclos.

Exemplo inválido:

```text
A depende de B
B depende de C
C depende de A
```

O sistema deve detectar:

```text
A → B → C → A
```

e impedir publicação ou importação.

---

# 18. Usuários

Existem dois tipos de usuário:

```text
TEACHER
STUDENT
```

---

# 19. Professor

Qualquer pessoa poderá criar uma conta de professor.

Cadastro:

```text
Nome
E-mail
Senha
Confirmação de senha
```

O e-mail deverá ser único.

Senha deve ser armazenada somente utilizando hash seguro.

Nunca armazenar senha em texto puro.

---

# 20. Funcionalidades do professor

O professor poderá:

- criar conta;
- fazer login;
- criar módulo;
- editar módulo;
- duplicar módulo;
- arquivar módulo;
- importar módulo JSON;
- exportar módulo JSON;
- criar conceitos;
- duplicar conceitos;
- editar conceitos;
- desativar conceitos;
- criar turmas;
- arquivar turmas;
- associar módulos às turmas;
- cadastrar matrículas;
- importar matrículas por CSV;
- gerar códigos temporários;
- regenerar acesso de alunos;
- acompanhar progresso básico.

---

# 21. Turmas

Um professor poderá possuir várias turmas.

Exemplo:

```text
Professor
├── Turma A
├── Turma B
└── Turma C
```

Uma turma pode possuir vários módulos.

Um módulo pode estar associado a várias turmas.

Isso representa uma relação muitos-para-muitos.

Criar entidade associativa equivalente a:

```text
ClassroomModule
```

---

# 22. Código da turma

O professor escolhe o código.

Exemplos:

```text
REDES2026
CC1SEM
TURMA-A
```

O código deve:

- ser único globalmente;
- ignorar diferença entre maiúsculas e minúsculas;
- aceitar apenas caracteres previamente definidos;
- possuir tamanho máximo razoável;
- ser validado no backend.

---

# 23. Identificação do aluno

O aluno é identificado por:

```text
Código da turma
+
Número de matrícula
```

Uma matrícula não precisa ser globalmente única.

Exemplo:

```text
Turma A + 12345
```

é diferente de:

```text
Turma B + 12345
```

---

# 24. Cadastro de alunos

O professor poderá cadastrar alunos:

## Manualmente

```text
Matrícula
Nome opcional
```

## Em massa

Por CSV.

Formato:

```csv
matricula,nome
12345,João
12346,Maria
12347,
```

`matricula` é obrigatória.

`nome` é opcional.

---

# 25. Código temporário

Ao criar um aluno, o sistema deverá gerar um código temporário individual.

Exemplo:

```text
K82P91
```

Primeiro login:

```text
Código da turma
Matrícula
Código temporário
```

Caso os dados estejam corretos:

```text
Criar senha
Confirmar senha
```

Após criação da senha:

- código temporário é invalidado;
- aluno passa a utilizar sua senha.

---

# 26. Login posterior do aluno

Campos:

```text
Código da turma
Matrícula
Senha
```

---

# 27. Redefinição de senha

Não haverá recuperação por e-mail no MVP.

O professor poderá selecionar:

```text
Turma
→ Aluno
→ Redefinir acesso
```

O sistema:

1. invalida a senha atual;
2. gera novo código temporário;
3. aluno utiliza o código;
4. cadastra uma nova senha.

---

# 28. Exportação das credenciais

Após importar ou cadastrar alunos, o professor poderá baixar CSV contendo:

```csv
matricula,nome,codigo_temporario
12345,João,X7K29P
12346,Maria,M4Q81A
```

---

# 29. Módulos

Um módulo pertence a um professor.

Exemplo:

```text
Fundamentos de Redes
```

Campos mínimos:

```text
id
title
description
subject
version
status
createdAt
updatedAt
teacherId
```

---

# 30. Estados de módulo

Um módulo pode possuir:

```text
DRAFT
PUBLISHED
ARCHIVED
```

## DRAFT

Pode ser editado livremente.

Não aparece aos alunos.

## PUBLISHED

Pode ser associado a turmas.

Pode continuar sendo editado.

Alterações valem imediatamente para todas as turmas que utilizam o módulo.

## ARCHIVED

Mantém histórico.

Não deve ser utilizado em novas sessões.

---

# 31. Exclusão

Evitar exclusões físicas para informações relevantes.

Preferir:

```text
módulo → arquivado
turma → arquivada
conceito → desativado
aluno → desativado/removido da turma
```

Histórico deve permanecer armazenado.

---

# 32. Duplicação de módulos

Professor poderá duplicar um módulo.

A cópia deve possuir:

- novo ID;
- status `DRAFT`;
- cópia dos conceitos;
- cópia das atividades;
- cópia dos pré-requisitos internos;
- nenhuma associação automática com turmas.

---

# 33. Conceitos

Cada módulo possui vários conceitos.

Exemplo:

```text
IP
DNS
TCP
UDP
HTTP
Firewall
NAT
DHCP
```

---

# 34. Estrutura de conceito

Campos principais:

```text
id
moduleId
name
definition
keywords
isActive
createdAt
updatedAt
```

Além disso, possuirá:

- pré-requisitos;
- pistas;
- atividades verdadeiro/falso;
- atividades de lacuna;
- atividades de ordenação opcionais.

---

# 35. Pré-requisitos

Um conceito pode possuir:

```text
0..N pré-requisitos
```

Exemplo:

```text
DNS
└── depende de IP
```

Outro exemplo:

```text
Roteamento avançado
├── depende de IP
├── depende de Subnet
└── depende de Router
```

Pré-requisitos devem pertencer ao mesmo módulo no MVP.

---

# 36. Liberação de conceito

Conceitos sem pré-requisitos podem ser introduzidos imediatamente.

Conceitos com pré-requisitos somente podem ser utilizados depois que todos os seus pré-requisitos estiverem suficientemente dominados.

Para o MVP, considerar pré-requisito cumprido quando o conceito estiver:

```text
MASTERED
```

---

# 37. Estados pedagógicos

Os conceitos não possuem um minigame fixo por nível.

O estado pedagógico é independente do minigame.

Estados:

```text
NEW
EXPOSURE
RECOGNITION
GUIDED_RECALL
FREE_RECALL
MASTERED
```

---

# 38. Fluxo de progressão

```text
NEW
↓
EXPOSURE
↓
RECOGNITION
↓
GUIDED_RECALL
↓
FREE_RECALL
↓
MASTERED
```

---

# 39. Exposição

Ao ser apresentado pela primeira vez:

```text
NEW → EXPOSURE
```

Depois de concluir completamente a exposição:

```text
EXPOSURE → RECOGNITION
```

---

# 40. Reconhecimento

Um acerto:

```text
RECOGNITION → GUIDED_RECALL
```

Um erro:

```text
RECOGNITION → RECOGNITION
```

O aluno permanece no mesmo estágio.

---

# 41. Recuperação guiada

Um acerto:

```text
GUIDED_RECALL → FREE_RECALL
```

Um erro:

```text
GUIDED_RECALL → RECOGNITION
```

---

# 42. Evocação livre

Um conceito não deve tornar-se dominado com apenas um acerto.

É necessário:

```text
2 acertos
em revisões diferentes
```

Fluxo:

```text
FREE_RECALL
↓
Acerto 1
↓
FSRS agenda nova revisão
↓
Acerto 2
↓
MASTERED
```

Um erro:

```text
FREE_RECALL → GUIDED_RECALL
```

---

# 43. Conceito dominado

`MASTERED` não significa que o conceito deixou de ser estudado.

Significa que entrou efetivamente no ciclo de manutenção.

O FSRS continuará agendando revisões.

Erro em conceito dominado:

```text
MASTERED → FREE_RECALL
```

O conceito não retorna diretamente à exposição.

---

# 44. Separação entre progressão e tempo

Existem dois eixos diferentes.

## Estado pedagógico

Determina:

```text
como estudar
```

## FSRS

Determina:

```text
quando estudar
```

Exemplo:

```text
DNS

state = FREE_RECALL
dueAt = hoje

↓
ActivitySelectionService

seleciona atividade compatível com FREE_RECALL
```

---

# 45. Classificação interna do desempenho

Internamente, resultados poderão ser convertidos para:

```text
AGAIN
HARD
GOOD
EASY
```

Esses valores poderão ser utilizados pelo FSRS.

O aluno **não verá esses botões**.

A classificação deverá ser inferida automaticamente.

---

# 46. Fatores utilizados na classificação

Considerar:

- resposta correta;
- resposta errada;
- quantidade de tentativas;
- quantidade de pistas;
- tempo de resposta.

O tempo nunca deverá sozinho transformar uma resposta correta em erro.

---

# 47. Cronômetro

As atividades possuirão cronômetro visual.

Exemplo:

```text
00:17
```

Registrar:

```text
startedAt
answeredAt
responseTimeMs
```

O cronômetro não deverá encerrar automaticamente atividades no MVP.

Não existe:

```text
tempo acabou → erro
```

---

# 48. Sessão de estudo

Cada sessão terá:

```text
10 atividades
```

fixas no MVP.

Professor e aluno não configuram essa quantidade.

---

# 49. Seleção das atividades

Prioridade:

```text
1. revisões vencidas
2. conceitos com erros recentes
3. conceitos em progressão
4. conceitos novos liberados
```

Caso não existam revisões suficientes, novos conceitos poderão ser introduzidos.

---

# 50. Módulos não são misturados

O aluno escolhe primeiro qual módulo estudar.

Exemplo:

```text
Meus módulos

[ Redes de Computadores ]
[ Segurança da Informação ]
[ Sistemas Operacionais ]
```

Depois:

```text
Redes de Computadores

[ Iniciar sessão ]
```

Uma sessão contém apenas conceitos daquele módulo.

---

# 51. Repetição de atividades

Uma atividade específica poderá reaparecer em sessões futuras.

Evitar repetir a mesma atividade dentro da mesma sessão.

Somente repetir na mesma sessão caso não exista alternativa válida.

---

# 52. Sessão interrompida

Cada resposta deve ser persistida imediatamente.

Se o aluno fechar o navegador após completar:

```text
4 / 10
```

as quatro respostas continuam registradas.

A sessão poderá ser marcada como:

```text
ABANDONED
```

Ao retornar, o aluno inicia uma nova sessão.

Não é necessário restaurar exatamente a atividade 5 da sessão anterior.

---

# 53. Progresso do módulo

Progresso principal:

```text
conceitos MASTERED
÷
conceitos ativos
× 100
```

Exemplo:

```text
32 conceitos estudados
18 conceitos dominados
50 conceitos ativos
```

Progresso:

```text
36%
```

---

# 54. Minigames do MVP

Implementar:

1. exposição interativa;
2. verdadeiro ou falso;
3. completar lacunas;
4. jogo das pistas;
5. ordenação.

Cruzadinha será pós-MVP.

---

# 55. Minigame 1 — Exposição interativa

Objetivo:

Apresentar um conceito novo.

Exemplo:

```text
DNS

O DNS permite realizar a
[ resolução de nomes ],
relacionando
[ nomes de domínio ]
a
[ endereços IP ].
```

As palavras-chave começam ocultas.

---

# 56. Palavras ocultas

O professor cadastra:

```text
keywords
```

O sistema deverá procurar automaticamente essas keywords dentro da definição.

As correspondências deverão aparecer ocultas.

O aluno deverá interagir com elas para revelar o conteúdo.

---

# 57. Regra da exposição

Não existe resposta errada.

Atividade concluída quando:

```text
todas as keywords forem reveladas
```

Então:

```text
EXPOSURE → RECOGNITION
```

---

# 58. Minigame 2 — Verdadeiro ou falso

Objetivo:

Testar reconhecimento.

Exemplo:

```text
"O DNS é responsável por realizar
o roteamento de pacotes entre redes."

[ Verdadeiro ]
[ Falso ]
```

Resposta:

```text
Falso
```

Feedback:

```text
O DNS está relacionado à resolução de nomes.
O roteamento de pacotes possui outra função.
```

---

# 59. Estrutura de verdadeiro/falso

Cada atividade possui:

```text
statement
isCorrect
explanation
```

Exemplo:

```json
{
  "statement": "DNS pode relacionar nomes de domínio a endereços IP.",
  "isCorrect": true,
  "explanation": "Essa é uma das principais funções associadas ao DNS."
}
```

---

# 60. Quantidade mínima

Cada conceito precisa possuir no mínimo:

```text
3 atividades de verdadeiro/falso
```

antes que o módulo possa ser publicado.

---

# 61. Minigame 3 — Completar lacunas

Objetivo:

Recuperação guiada.

Exemplo:

```text
O ___ pode transformar ___ em endereços IP.

[ DNS ]
[ TCP ]
[ nomes de domínio ]
[ roteadores ]
```

---

# 62. Formas de interação

Suportar:

## Drag-and-drop

O aluno arrasta a palavra para a lacuna.

## Clique

O aluno pode:

```text
clicar palavra
↓
clicar lacuna
```

Isso deve funcionar principalmente para dispositivos móveis.

---

# 63. Resposta de lacunas

Apenas a combinação completamente correta conta como acerto.

Não utilizar pontuação parcial no MVP.

---

# 64. Estrutura de atividade de lacunas

Exemplo:

```json
{
  "text": "O {{1}} pode relacionar {{2}} a endereços IP.",
  "answers": {
    "1": "DNS",
    "2": "nomes de domínio"
  },
  "distractors": [
    "TCP",
    "roteadores"
  ]
}
```

---

# 65. Quantidade mínima

Cada conceito precisa possuir:

```text
3 atividades de lacunas
```

antes da publicação.

---

# 66. Minigame 4 — Jogo das pistas

Objetivo:

Evocação livre.

O aluno precisa descobrir o nome do conceito.

Exemplo:

```text
Descubra o conceito.

Dica 1:
Opera na camada de aplicação.

[ Resposta ]
```

---

# 67. Pistas

Cada conceito precisa possuir:

```text
3 pistas
```

obrigatórias.

Idealmente elas devem aumentar gradualmente em especificidade.

Exemplo:

```text
Pista 1:
Opera na camada de aplicação.

Pista 2:
Está relacionado a nomes utilizados por humanos.

Pista 3:
Pode relacionar um domínio a um endereço IP.
```

---

# 68. Tentativas

O aluno possui:

```text
3 tentativas
```

no total.

Fluxo:

```text
Pista 1
↓
erro
↓
Pista 2
↓
erro
↓
Pista 3
↓
erro
↓
primeira letra
```

Depois da sequência de erros, a primeira letra poderá ser exibida como ajuda final.

---

# 69. Classificação do jogo das pistas

Sugestão interna:

```text
acerto muito cedo
→ EASY

acerto com ajuda intermediária
→ GOOD

acerto após bastante ajuda
→ HARD

não conseguiu responder
→ AGAIN
```

Essa classificação não é apresentada ao aluno.

---

# 70. Minigame 5 — Ordenação

Objetivo:

Trabalhar:

- sequências;
- processos;
- protocolos;
- camadas;
- etapas.

Exemplo:

```text
Ordene as camadas:

[ Rede ]
[ Aplicação ]
[ Enlace ]
[ Transporte ]
```

---

# 71. Ordenação de processos

Exemplo:

```text
Ordene uma consulta DNS:

[ Servidor responde ]
[ Cliente realiza consulta ]
[ DNS procura o registro ]
[ Aplicação utiliza o endereço ]
```

---

# 72. Correção da ordenação

Somente uma sequência:

```text
100% correta
```

é considerada acerto.

Não usar pontuação parcial no MVP.

---

# 73. Ordenação é opcional

Nem todo conceito precisa possuir atividade de ordenação.

Exemplo:

```json
"orderingActivities": []
```

é válido.

---

# 74. Validação mínima de conceito

Para publicar um módulo, cada conceito ativo deverá possuir:

```text
nome
definição
≥ 1 keyword
3 pistas
≥ 3 atividades verdadeiro/falso
≥ 3 atividades de lacunas
```

Opcional:

```text
pré-requisitos
atividades de ordenação
```

---

# 75. Interface de validação

No editor:

```text
DNS

✓ definição
✓ palavras-chave
✓ pistas (3/3)
✓ verdadeiro/falso (3/3)
⚠ lacunas (2/3)
○ ordenação opcional
```

Caso algum conceito esteja inválido:

```text
PUBLICAR MÓDULO
```

permanece desabilitado.

---

# 76. Criação manual de módulo

Interface semelhante a um formulário dinâmico.

Exemplo:

```text
Novo módulo

Nome
[ Fundamentos de Redes ]

Descrição
[ ................ ]

Disciplina
[ Redes de Computadores ]

────────────────────────

CONCEITO 1

Nome
[ DNS ]

Definição
[ ................ ]

Palavras-chave
[ + adicionar ]

Pré-requisitos
[ + adicionar ]

Pistas
[ + adicionar ]

Verdadeiro/Falso
[ + adicionar ]

Lacunas
[ + adicionar ]

Ordenação
[ + adicionar ]

[ DUPLICAR CONCEITO ]

────────────────────────

[ + NOVO CONCEITO ]
```

---

# 77. Importação JSON

Professor poderá importar um módulo completo.

O JSON deve obedecer ao schema oficial da aplicação.

Exemplo simplificado:

```json
{
  "title": "Fundamentos de Redes",
  "description": "Introdução aos principais conceitos de redes.",
  "subject": "Redes de Computadores",
  "version": 1,
  "concepts": [
    {
      "id": "ip",
      "name": "IP",
      "definition": "Protocolo responsável pelo endereçamento lógico e encaminhamento de pacotes.",
      "keywords": [
        "endereçamento lógico",
        "pacotes"
      ],
      "prerequisites": [],
      "clues": [
        "Está relacionado à camada de rede.",
        "Trabalha com endereçamento lógico.",
        "É utilizado para identificar dispositivos em redes."
      ],
      "recognitionActivities": [],
      "fillBlankActivities": [],
      "orderingActivities": []
    }
  ]
}
```

---

# 78. Exemplo completo de conceito JSON

```json
{
  "id": "dns",
  "name": "DNS",
  "definition": "O DNS é utilizado para realizar resolução de nomes, permitindo relacionar nomes de domínio a endereços IP.",

  "keywords": [
    "resolução de nomes",
    "nomes de domínio",
    "endereços IP"
  ],

  "prerequisites": [
    "ip"
  ],

  "clues": [
    "Opera na camada de aplicação.",
    "Está relacionado a nomes utilizados por humanos.",
    "Pode relacionar um domínio a um endereço IP."
  ],

  "recognitionActivities": [
    {
      "statement": "DNS pode relacionar nomes de domínio a endereços IP.",
      "isCorrect": true,
      "explanation": "Essa é uma das funções associadas ao DNS."
    },
    {
      "statement": "DNS é responsável pelo roteamento físico dos pacotes.",
      "isCorrect": false,
      "explanation": "DNS trabalha com resolução de nomes, não com roteamento físico."
    },
    {
      "statement": "DNS é utilizado por aplicações que precisam descobrir o endereço associado a um domínio.",
      "isCorrect": true,
      "explanation": "A resolução de nomes permite descobrir endereços associados aos domínios."
    }
  ],

  "fillBlankActivities": [
    {
      "text": "O {{1}} pode relacionar {{2}} a endereços IP.",
      "answers": {
        "1": "DNS",
        "2": "nomes de domínio"
      },
      "distractors": [
        "TCP",
        "roteadores"
      ]
    },
    {
      "text": "O DNS realiza {{1}}.",
      "answers": {
        "1": "resolução de nomes"
      },
      "distractors": [
        "criptografia de pacotes",
        "roteamento físico"
      ]
    },
    {
      "text": "Um {{1}} pode ser associado a um {{2}} utilizando DNS.",
      "answers": {
        "1": "nome de domínio",
        "2": "endereço IP"
      },
      "distractors": [
        "switch",
        "cabo Ethernet"
      ]
    }
  ],

  "orderingActivities": []
}
```

---

# 79. Validação da importação JSON

A importação deve ser:

```text
ATÔMICA
```

O sistema deve:

1. receber o arquivo;
2. desserializar;
3. validar schema;
4. validar campos;
5. validar IDs;
6. validar referências;
7. validar pré-requisitos;
8. detectar ciclos;
9. validar atividades mínimas;
10. somente depois salvar.

Se qualquer etapa falhar:

```text
nenhuma parte do módulo deve ser persistida
```

Utilizar transação.

---

# 80. Mensagens de erro da importação

Mensagens devem identificar o problema.

Exemplo:

```text
Falha ao importar módulo.

concepts[4].prerequisites[0]:

O conceito "ipv7" não existe neste módulo.
```

Outro:

```text
Dependência circular detectada:

DNS → IP → DNS
```

---

# 81. Exportação JSON

Todo módulo deverá poder ser exportado.

A exportação deve produzir um JSON reutilizável para:

- backup;
- edição externa;
- compartilhamento manual;
- importação em outra conta.

---

# 82. Versionamento de módulo

Campo:

```text
version
```

deve existir.

No MVP não é necessário criar controle de versões completo.

A versão funciona inicialmente como metadado.

---

# 83. Edição de módulo publicado

Permitida.

Mudanças entram em vigor imediatamente nas turmas associadas.

---

# 84. Adição de novo conceito

Caso um novo conceito seja adicionado a módulo publicado:

```text
novo conceito
↓
verifica pré-requisitos
↓
quando satisfeitos
↓
conceito torna-se elegível
```

Não é necessário atualizar manualmente cada turma.

---

# 85. Remoção de conceito

Caso o conceito possua histórico:

não deletar fisicamente.

Utilizar:

```text
isActive = false
```

O conceito deixa de aparecer em novas sessões.

Histórico permanece acessível.

---

# 86. Duplicação de conceito

Professor poderá duplicar conceito.

Devem ser copiados:

- definição;
- keywords;
- pistas;
- atividades.

O novo conceito recebe:

- novo ID;
- nome ajustável.

Os pré-requisitos podem ser copiados.

---

# 87. Tela inicial do aluno

Não mostrar níveis técnicos.

Exemplo:

```text
Olá!

Escolha um módulo:

┌─────────────────────────┐
│ Redes de Computadores   │
│ Progresso: 36%          │
│ 8 revisões pendentes    │
└─────────────────────────┘
```

---

# 88. Tela do módulo

Exemplo:

```text
Redes de Computadores

18 conceitos dominados
32 conceitos estudados
8 revisões pendentes

[ INICIAR SESSÃO ]
```

Não exibir:

```text
DNS está em GUIDED_RECALL
```

para o aluno.

---

# 89. Durante a sessão

Mostrar:

```text
Atividade 3 de 10

cronômetro

conteúdo do minigame
```

Depois da resposta:

- mostrar feedback;
- registrar resultado;
- atualizar backend;
- seguir para próxima atividade.

---

# 90. Final da sessão

Exemplo:

```text
Sessão concluída

10 atividades realizadas
8 corretas
2 conceitos reforçados
1 novo conceito descoberto

[ VOLTAR AO MÓDULO ]
```

Não mostrar detalhes excessivamente técnicos sobre FSRS.

---

# 91. Interface do professor

Menu principal:

```text
Dashboard
Módulos
Turmas
Conta
```

---

# 92. Dashboard do professor

MVP simples.

Exemplo:

```text
Módulos: 4
Turmas ativas: 3
Alunos: 86
```

Não é necessário criar gráficos.

---

# 93. Tela de turmas

```text
Minhas turmas

[ Redes - Turma A ]
32 alunos
2 módulos

[ Redes - Turma B ]
28 alunos
1 módulo

[ + NOVA TURMA ]
```

---

# 94. Progresso da turma

Tabela básica:

| Matrícula | Nome | Progresso | Revisões pendentes | Último acesso |
|---|---|---:|---:|---|
| 10341 | João | 68% | 8 | Hoje |
| 10342 | Maria | 45% | 14 | Ontem |
| 10343 | — | 82% | 3 | 22/09 |

---

# 95. Detalhes do aluno

Ao selecionar um aluno:

```text
Matrícula: 10341

Redes de Computadores

Dominados: 28
Em aprendizagem: 14
Não iniciados: 19
Revisões pendentes: 6
```

Não implementar dashboard complexo inicialmente.

---

# 96. Entidades principais do banco

O modelo deverá possuir entidades equivalentes a:

```text
Teacher
Student
Classroom
Module
ClassroomModule
Concept
ConceptPrerequisite
ConceptKeyword
ConceptClue
RecognitionActivity
FillBlankActivity
OrderingActivity
StudentConceptState
StudySession
ActivityAttempt
```

Além das estruturas necessárias ao FSRS.

---

# 97. Teacher

Campos sugeridos:

```text
Id
Name
Email
PasswordHash
CreatedAt
UpdatedAt
```

---

# 98. Classroom

Campos sugeridos:

```text
Id
TeacherId
Name
Code
Status
CreatedAt
UpdatedAt
```

---

# 99. Student

Campos sugeridos:

```text
Id
ClassroomId
EnrollmentNumber
Name
PasswordHash
TemporaryAccessCodeHash
IsActivated
IsActive
CreatedAt
UpdatedAt
```

Criar unique constraint para:

```text
ClassroomId + EnrollmentNumber
```

---

# 100. Module

Campos sugeridos:

```text
Id
TeacherId
Title
Description
Subject
Version
Status
CreatedAt
UpdatedAt
```

---

# 101. ClassroomModule

Campos:

```text
ClassroomId
ModuleId
AssignedAt
```

Unique constraint:

```text
ClassroomId + ModuleId
```

---

# 102. Concept

Campos sugeridos:

```text
Id
ModuleId
ExternalId
Name
Definition
IsActive
CreatedAt
UpdatedAt
```

`ExternalId` pode armazenar identificadores como:

```text
dns
ip
tcp
```

utilizados no JSON.

---

# 103. ConceptPrerequisite

```text
ConceptId
PrerequisiteConceptId
```

Ambos devem pertencer ao mesmo módulo no MVP.

---

# 104. StudentConceptState

Representa o progresso individual.

Campos conceituais:

```text
StudentId
ConceptId
LearningState
FreeRecallSuccessCount
LastAttemptAt
LastSuccessAt
LastFailureAt
```

Além dos dados necessários ao FSRS:

```text
Difficulty
Stability
DueAt
LastReviewAt
...
```

A estrutura exata deverá seguir a implementação utilizada do FSRS.

---

# 105. StudySession

Campos sugeridos:

```text
Id
StudentId
ModuleId
Status
StartedAt
CompletedAt
TotalActivities
CompletedActivities
```

Status:

```text
ACTIVE
COMPLETED
ABANDONED
```

---

# 106. ActivityAttempt

Registrar cada tentativa.

Campos conceituais:

```text
Id
StudentId
ConceptId
SessionId
ActivityType
ActivityId
WasCorrect
StartedAt
AnsweredAt
ResponseTimeMs
AttemptsUsed
HintsUsed
FsrsRating
CreatedAt
```

Pode conter JSON adicional com detalhes da resposta quando necessário.

---

# 107. Histórico

Não substituir tentativas antigas.

Cada atividade realizada gera registro próprio.

Isso permitirá futuramente:

- análises;
- gráficos;
- melhorias no algoritmo;
- histórico detalhado.

---

# 108. Segurança

Obrigatório:

- senha com hash seguro;
- autenticação protegida;
- autorização por tipo de usuário;
- professor só acessa seus próprios módulos;
- professor só acessa suas próprias turmas;
- aluno só acessa sua própria turma;
- aluno só acessa módulos associados à sua turma;
- validação sempre no backend;
- DTOs específicos;
- não expor entidades diretamente;
- validar uploads;
- limitar tamanho dos arquivos;
- sanitizar entradas quando necessário.

---

# 109. Autenticação

Utilizar mecanismo seguro baseado em ASP.NET Core.

Pode utilizar:

- cookies HTTP-only;

ou arquitetura equivalente segura.

Para aplicação web tradicional, preferir autenticação via cookie HTTP-only se não houver necessidade concreta de expor tokens ao frontend.

Não armazenar token sensível em `localStorage` se puder ser evitado.

---

# 110. Autorização

Criar políticas/roles:

```text
Teacher
Student
```

Endpoints devem validar permissões.

Exemplo:

```text
Teacher:
POST /modules

Student:
não permitido
```

---

# 111. APIs

Os nomes finais podem variar, mas a API deve possuir estrutura próxima de:

## Auth

```text
POST /api/auth/teacher/register
POST /api/auth/teacher/login

POST /api/auth/student/activate
POST /api/auth/student/login

POST /api/auth/logout
GET  /api/auth/me
```

---

# 112. Modules

```text
GET    /api/modules
GET    /api/modules/{id}
POST   /api/modules
PUT    /api/modules/{id}
POST   /api/modules/{id}/duplicate
POST   /api/modules/{id}/publish
POST   /api/modules/{id}/archive
```

---

# 113. Importação e exportação

```text
POST /api/modules/import
GET  /api/modules/{id}/export
```

---

# 114. Concepts

```text
GET    /api/modules/{moduleId}/concepts
POST   /api/modules/{moduleId}/concepts
PUT    /api/concepts/{id}
POST   /api/concepts/{id}/duplicate
POST   /api/concepts/{id}/deactivate
```

---

# 115. Classrooms

```text
GET  /api/classrooms
POST /api/classrooms
GET  /api/classrooms/{id}
PUT  /api/classrooms/{id}
POST /api/classrooms/{id}/archive
```

---

# 116. Associação módulo-turma

```text
POST   /api/classrooms/{classroomId}/modules/{moduleId}
DELETE /api/classrooms/{classroomId}/modules/{moduleId}
```

A remoção da associação não deve apagar progresso histórico.

---

# 117. Students

```text
GET  /api/classrooms/{classroomId}/students
POST /api/classrooms/{classroomId}/students
```

Importação:

```text
POST /api/classrooms/{classroomId}/students/import
```

Credenciais:

```text
GET /api/classrooms/{classroomId}/students/credentials
```

---

# 118. Reset de acesso

```text
POST /api/students/{studentId}/reset-access
```

Retorna novo código temporário somente ao professor autorizado.

---

# 119. Estudo

```text
GET  /api/student/modules
GET  /api/student/modules/{moduleId}
POST /api/study/modules/{moduleId}/sessions
```

---

# 120. Próxima atividade

Pode ser implementado como:

```text
GET /api/study/sessions/{sessionId}/next
```

ou retornada automaticamente depois da resposta anterior.

---

# 121. Resposta de atividade

```text
POST /api/study/sessions/{sessionId}/answer
```

Payload conceitual:

```json
{
  "activityId": "...",
  "activityType": "FILL_BLANK",
  "answer": {},
  "responseTimeMs": 8200
}
```

Backend:

1. valida atividade;
2. corrige;
3. registra tentativa;
4. atualiza progresso;
5. atualiza FSRS;
6. retorna feedback;
7. retorna estado da sessão.

---

# 122. Regra de autoridade

O frontend nunca deve enviar algo como:

```text
"advanceConcept": true
```

ou:

```text
"newState": "MASTERED"
```

O backend calcula isso.

Frontend envia apenas fatos da interação.

---

# 123. Resposta conceitual da API

Exemplo:

```json
{
  "correct": true,
  "feedback": "DNS realiza resolução de nomes.",
  "sessionProgress": {
    "completed": 4,
    "total": 10
  }
}
```

Não é necessário expor todos os estados internos ao aluno.

---

# 124. Interface visual

Tema padrão:

```text
dark
```

Características:

- moderna;
- simples;
- lúdica;
- não infantil;
- pouco poluída;
- forte contraste;
- feedback visual claro;
- animações curtas;
- transições sutis.

---

# 125. Responsividade

Suportar:

```text
mobile ≥ 360px
tablet
desktop ≥ 1024px
```

Drag-and-drop deve funcionar com:

- mouse;
- touchscreen.

Sempre fornecer alternativa por clique quando o drag-and-drop for essencial.

---

# 126. Acessibilidade básica

Sempre que possível:

- labels em inputs;
- foco visível;
- navegação por teclado;
- contraste adequado;
- botões semanticamente corretos;
- mensagens de erro associadas aos campos.

Não depender exclusivamente de cor para indicar erro/acerto.

---

# 127. Feedback

Exemplo de acerto:

```text
✓ Correto!

DNS pode relacionar nomes de domínio a endereços IP.
```

Erro:

```text
✕ Não foi dessa vez.

Resposta correta:
DNS

Explicação:
...
```

Evitar feedback punitivo.

---

# 128. Estado vazio

Interfaces devem prever situações como:

```text
Nenhum módulo criado.
```

```text
Nenhuma turma cadastrada.
```

```text
Nenhuma revisão pendente.
```

e fornecer ação apropriada.

---

# 129. Regras para o seletor de sessão

O sistema deve buscar atividades até completar:

```text
10
```

Priorizar conceitos vencidos.

Evitar:

- mesma atividade duas vezes;
- concentração desnecessária no mesmo conceito;
- introduzir conceito cujo pré-requisito não foi dominado.

---

# 130. Novos conteúdos

Se não existirem revisões suficientes:

1. verificar conceitos `NEW`;
2. verificar pré-requisitos;
3. selecionar conceitos elegíveis;
4. introduzir através de exposição.

---

# 131. Erros recentes

Conceitos que sofreram regressão devem possuir prioridade adicional.

Exemplo:

```text
FREE_RECALL
↓ erro
GUIDED_RECALL
```

Esse conceito deve reaparecer de acordo com as regras pedagógicas e FSRS.

---

# 132. Publicação de módulo

Fluxo:

```text
Professor clica "Publicar"
↓
backend valida módulo
↓
valida conceitos
↓
valida atividades
↓
valida pré-requisitos
↓
detecta ciclos
↓
se tudo estiver correto
↓
status = PUBLISHED
```

---

# 133. Erro na publicação

Exemplo:

```text
Não foi possível publicar o módulo.

3 conceitos precisam ser corrigidos:

DNS
- possui apenas 2 atividades de lacunas

TCP
- possui apenas 2 pistas

HTTP
- pré-requisito "WWW" não existe
```

---

# 134. Importação CSV

Fluxo:

```text
upload
↓
parse
↓
validação
↓
pré-visualização
↓
confirmação
↓
persistência
↓
geração de códigos temporários
```

Rejeitar linhas inválidas com mensagem clara.

---

# 135. CSV duplicado

Se uma matrícula já existir naquela turma:

não criar duplicata.

Informar:

```text
12345 já existe nesta turma.
```

O comportamento deve ser previsível e documentado.

---

# 136. IDs

Banco deve utilizar IDs internos próprios.

Conteúdo importado pode utilizar IDs legíveis no campo `ExternalId`.

Exemplo:

```text
database Id:
UUID

ExternalId:
dns
```

Isso facilita:

- JSON;
- pré-requisitos;
- edição;
- importação/exportação.

---

# 137. Datas

Persistir datas no backend de forma consistente.

Preferir UTC no banco.

Conversão de horário ocorre na camada de apresentação quando necessário.

---

# 138. Logs

Backend deverá possuir logging para:

- erros;
- falhas de importação;
- autenticação;
- exceções;
- operações críticas.

Nunca registrar:

- senhas;
- códigos temporários em texto puro;
- informações sensíveis desnecessárias.

---

# 139. Tratamento global de erros

Criar middleware de exceções.

Retornar erros da API em formato consistente.

Exemplo:

```json
{
  "code": "MODULE_VALIDATION_ERROR",
  "message": "Não foi possível publicar o módulo.",
  "details": []
}
```

---

# 140. DTOs

Não utilizar diretamente Models/Entities do banco como contratos da API.

Criar DTOs específicos.

Exemplo:

```text
CreateModuleRequest
ModuleResponse
CreateConceptRequest
StudentLoginRequest
SubmitActivityRequest
```

---

# 141. Validações

Validações importantes devem ocorrer no backend mesmo que já existam no frontend.

Frontend melhora UX.

Backend garante integridade.

---

# 142. README

O README deve explicar:

- objetivo;
- stack;
- requisitos;
- configuração;
- Docker;
- PostgreSQL;
- migrations;
- execução do backend;
- execução do frontend;
- execução dos testes;
- estrutura das pastas;
- credenciais/testes iniciais quando aplicável.

---

# 143. Seed de desenvolvimento

Criar dados de desenvolvimento opcionais.

Exemplo:

```text
Professor demo
Turma demo
Aluno demo
Módulo básico de Redes
```

Seeds não devem ser utilizados automaticamente em produção.

---

# 144. Critérios de aceitação — autenticação

O MVP é aceito quando:

- professor consegue criar conta;
- professor consegue entrar;
- professor consegue criar turma;
- professor consegue cadastrar matrícula;
- código temporário é gerado;
- aluno consegue ativar conta;
- código temporário deixa de funcionar depois da ativação;
- aluno consegue entrar com senha;
- professor consegue redefinir acesso.

---

# 145. Critérios de aceitação — módulos

O MVP é aceito quando:

- professor cria módulo manualmente;
- adiciona conceitos;
- define pré-requisitos;
- adiciona pistas;
- adiciona verdadeiro/falso;
- adiciona lacunas;
- adiciona ordenação;
- consegue duplicar conceito;
- consegue duplicar módulo;
- consegue publicar;
- consegue arquivar;
- consegue editar módulo publicado.

---

# 146. Critérios de aceitação — importação

O MVP é aceito quando:

- JSON válido é importado;
- JSON inválido é rejeitado;
- referência inexistente é detectada;
- ciclos são detectados;
- importação inválida não persiste dados parciais;
- módulo pode ser exportado novamente.

---

# 147. Critérios de aceitação — turmas

O MVP é aceito quando:

- professor cria várias turmas;
- associa vários módulos;
- mesmo módulo pode ser utilizado em várias turmas;
- alunos enxergam apenas módulos da própria turma.

---

# 148. Critérios de aceitação — CSV

O MVP é aceito quando:

- professor importa CSV;
- matrículas são criadas;
- nomes opcionais funcionam;
- duplicatas são detectadas;
- códigos temporários são gerados;
- professor consegue exportar credenciais.

---

# 149. Critérios de aceitação — aprendizagem

O MVP é aceito quando:

- conceito novo começa em exposição;
- exposição leva a reconhecimento;
- reconhecimento correto leva a recuperação guiada;
- recuperação guiada correta leva a evocação livre;
- dois acertos de evocação em revisões diferentes levam a domínio;
- erros provocam regressões corretas;
- conceito dominado continua sendo revisado;
- erro em dominado retorna a evocação livre.

---

# 150. Critérios de aceitação — pré-requisitos

O MVP é aceito quando:

- conceitos sem pré-requisitos são liberados;
- conceitos bloqueados não aparecem;
- conceito é liberado quando todos os pré-requisitos ficam `MASTERED`;
- dependências circulares não podem ser publicadas.

---

# 151. Critérios de aceitação — sessões

O MVP é aceito quando:

- aluno escolhe módulo;
- sessão possui 10 atividades;
- atividades são escolhidas automaticamente;
- revisões vencidas possuem prioridade;
- atividades não se repetem desnecessariamente;
- cada resposta é salva imediatamente;
- sessão interrompida preserva respostas anteriores.

---

# 152. Critérios de aceitação — minigames

Todos devem funcionar com:

- mouse;
- teclado quando aplicável;
- toque em telas móveis.

Devem existir:

```text
Exposure
True/False
Fill Blank
Guess Concept
Ordering
```

---

# 153. Critérios de aceitação — professor

Professor deve visualizar tabela contendo:

```text
Matrícula
Nome
Progresso
Revisões pendentes
Último acesso
```

---

# 154. Critérios de aceitação — interface

O sistema deve:

- funcionar em desktop;
- funcionar em celular;
- utilizar tema escuro;
- possuir feedback visual;
- não apresentar erros de layout importantes;
- possuir navegação consistente;
- apresentar loading;
- apresentar estados vazios;
- tratar falhas da API.

---

# 155. Princípios de implementação

Ao gerar código, seguir estes princípios:

1. evitar arquivos excessivamente grandes;
2. separar responsabilidades;
3. utilizar tipagem forte;
4. evitar valores mágicos;
5. centralizar enums;
6. centralizar regras pedagógicas no backend;
7. não duplicar lógica;
8. não misturar UI e persistência;
9. não implementar funcionalidades fora do escopo sem necessidade;
10. escrever código legível para estudo posterior;
11. utilizar nomes claros;
12. adicionar comentários apenas quando explicarem decisões não óbvias.

---

# 156. Regra para o Codex

Antes de criar uma funcionalidade nova, verificar se ela pertence a:

```text
UI
Controller
Service
Repository
Domain
Minigame
```

A lógica não deve ser colocada no arquivo mais conveniente apenas para acelerar a implementação.

---

# 157. Regra de extensibilidade dos minigames

Adicionar futuramente um minigame como:

```text
Matching
Crossword
Packet Routing
Network Diagram
```

não deverá exigir mudanças profundas em:

- autenticação;
- FSRS;
- banco de usuários;
- turmas;
- progresso geral.

A arquitetura precisa considerar minigames como componentes substituíveis e extensíveis.

---

# 158. Pós-MVP

Possíveis expansões:

```text
Cruzadinha
Matching
Simulação de pacotes
Montagem de topologia
Diagramas interativos
Ranking opcional
Gamificação
Estatísticas avançadas
Dashboard visual
Notificações
Modo offline
PWA
Compartilhamento de módulos
Biblioteca pública
IA para auxiliar professor
```

Essas funcionalidades não devem influenciar a complexidade do MVP além do necessário para manter extensibilidade razoável.

---

# 159. Princípio central do produto

A complexidade deve permanecer principalmente no sistema.

Para o aluno:

```text
entrar
↓
escolher módulo
↓
iniciar sessão
↓
resolver desafios
↓
terminar
```

O aluno não precisa saber:

```text
FSRS
stability
difficulty
learning state
prerequisite graph
activity selection
```

Ele simplesmente recebe atividades adequadas ao seu estado de aprendizagem.

---

# 160. Resultado esperado do MVP

Ao final do desenvolvimento deverá existir uma aplicação web funcional em que:

1. um professor cria uma conta;
2. cria um módulo de Redes;
3. cadastra conceitos e atividades;
4. publica o módulo;
5. cria uma turma;
6. importa alunos;
7. associa o módulo;
8. fornece credenciais aos alunos;
9. aluno ativa sua conta;
10. escolhe o módulo;
11. realiza sessões;
12. conceitos evoluem individualmente;
13. revisões são agendadas pelo FSRS;
14. pré-requisitos controlam novos conteúdos;
15. professor acompanha o progresso básico.

O sistema deve funcionar como uma base sólida para evolução futura e, ao mesmo tempo, possuir código suficientemente organizado e compreensível para ser utilizado como projeto de portfólio e estudo.