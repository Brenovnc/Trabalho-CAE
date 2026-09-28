# Checklist de validação manual do MVP

Este roteiro complementa os testes xUnit/Vitest. Marque cada caixa ao executar com um navegador. Use um ambiente/banco descartável de validação para criar e alterar dados; nunca execute testes destrutivos no banco trabalho_cae. O endpoint /health verifica processo/configuração, não conectividade PostgreSQL.

## Preparação

- [ ] Iniciar PostgreSQL, aplicar migrations e iniciar API e frontend conforme o README.
- [ ] Confirmar /health, abrir DevTools sem erros e confirmar que a UI acessa a API na origem esperada.
- [ ] Usar 1440×900 e 390×844; repetir também em largura mínima de 360 px.
- [ ] Criar um professor de teste e usar somente conteúdo de teste.

## Professor, módulos e JSON

- [ ] Registrar professor, entrar, navegar por Dashboard, Módulos, Turmas e Conta, e sair.
- [ ] Criar módulo e conceitos com definição, keywords, três pistas, três itens de verdadeiro/falso e três lacunas; incluir ordenação opcional e pré-requisito.
- [ ] Salvar, editar, duplicar conceito/módulo, publicar conteúdo válido e arquivar uma cópia.
- [ ] Tentar publicar conceito incompleto e um grafo cíclico; conferir mensagens compreensíveis e ausência de publicação.
- [ ] Exportar módulo, importar o JSON válido como draft e reexportar.
- [ ] Importar JSON com schema inválido, referência ausente e ciclo; conferir rejeição sem módulo parcial.
- [ ] Editar um módulo publicado e confirmar o comportamento de atualização descrito no README.

## Turmas, CSV e autenticação do aluno

- [ ] Criar duas turmas; conferir normalização e conflito de código sem diferenciar maiúsculas/minúsculas.
- [ ] Associar o mesmo módulo a duas turmas e removê-lo de uma sem perder histórico.
- [ ] Cadastrar aluno manualmente e importar CSV com BOM/aspas, nome em branco, matrícula com zeros à esquerda, duplicata, linha inválida e uma linha válida.
- [ ] Conferir preview sem gravação; confirmar e verificar importação parcial, arquivo de credenciais e mensagens de linhas ignoradas.
- [ ] Ativar um aluno com o código temporário; tentar reutilizar o código e entrar com senha; redefinir acesso e verificar que o código/senha anterior deixa de funcionar.
- [ ] Conferir logout, CSRF nas mutações e que papel/aluno não acessa telas ou dados de outro papel/turma.

## Estudo e persistência

- [ ] Entrar como aluno e confirmar que aparecem somente módulos publicados associados à própria turma.
- [ ] Iniciar módulo, abrir sessão, revelar cada keyword da exposição, atualizar a página e confirmar que as revelações permanecem; concluir exposição.
- [ ] Interagir com True/False, Fill Blank, Guess Concept e Ordering; testar mouse, teclado quando aplicável e toque/clique em celular.
- [ ] Confirmar feedback após resposta, envio único, ausência da resposta correta antes da submissão e atualização feita pela API.
- [ ] Completar respostas, interromper sessão, retornar e conferir estado da sessão e tentativas persistidas; confirmar que módulo/turma diferente permanece isolado.
- [ ] Percorrer regressões de estado e revisão FSRS com dados controlados/testes, sem esperar intervalos reais nem editar estados manualmente no banco.
- [ ] Confirmar pré-requisito bloqueado antes de dominar todos os requisitos e elegível após cumprir todos.

## Acompanhamento e layout

- [ ] Conferir cards do dashboard, módulos, turmas, estado vazio, loading, erro da API e feedback de ações.
- [ ] Conferir tabela de progresso: matrícula, nome (incluindo ausência), porcentagem, revisões pendentes e último acesso.
- [ ] Abrir detalhes do aluno e comparar agregados com os estados persistidos.
- [ ] Entrar como outro professor e tentar consultar IDs da turma/aluno anterior; confirmar resposta sem acesso aos dados. Repetir como STUDENT.
- [ ] Em 1440×900 e 390×844, visitar login, dashboard, módulos/editor/importação JSON, turmas/CSV, tabela/detalhes, lista/resumo do aluno, sessão e cinco minigames.
- [ ] Verificar tema escuro, rolagem horizontal acessível da tabela/CSV, foco visível, labels, mensagens de erro, botões desabilitados durante operações e ausência de quebra importante de layout.

