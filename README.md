# Oficina do Byte

Jogo educativo 2D de plataforma feito na Unity, em que o jogador aprende os fundamentos de criação de jogos (física, interação, animação e inteligência artificial) montando, peça por peça, o código e os componentes que fazem a fase funcionar. O guia ao longo das fases é o **Prof Byte**.

Trabalho de Conclusão de Curso de Engenharia de Computação da **FEAP Avaré** (2026).

| | |
|---|---|
| **Desenvolvimento** | Guilherme Monteiro da Luz, João Gabriel Pereira Cardozo |
| **Orientação** | Nicolas Antonio Messias dos Santos Colloca |
| **Instituição** | FEAP Avaré |
| **Versão** | 1.2 |
| **Plataforma** | Windows 64 bits |

## Sumário

- [Como o jogo funciona](#como-o-jogo-funciona)
- [Fases e conceitos ensinados](#fases-e-conceitos-ensinados)
- [Controles](#controles)
- [Como abrir o projeto na Unity](#como-abrir-o-projeto-na-unity)
- [Como gerar o executável e o instalador](#como-gerar-o-executável-e-o-instalador)
- [Onde ficam os dados (saves e registros)](#onde-ficam-os-dados-saves-e-registros)
- [Modo professor](#modo-professor)
- [Estrutura do repositório](#estrutura-do-repositório)
- [Créditos de arte e áudio](#créditos-de-arte-e-áudio)
- [Licença](#licença)

## Como o jogo funciona

1. No **Menu**, o aluno escolhe quem vai jogar: com um apelido (2 a 12 caracteres) ou como anônimo ("Jogador 007"). Vários alunos podem usar o mesmo computador, cada um com seu próprio progresso.
2. Antes da primeira fase, o aluno responde ao **questionário inicial** (pré-teste): 12 perguntas de múltipla escolha sobre física e movimento, interação e vida, animação e IA. Nenhuma resposta é corrigida nesse momento, e a ordem das alternativas é sorteada a cada pergunta, para ninguém decorar a letra que marcou.
3. As **8 fases** são jogadas em ordem. Cada fase só libera depois que a anterior é concluída. A maioria tem um puzzle de "montar o código" ou configurar componentes, seguido de um trecho de plataforma em que o aluno vê o resultado.
4. Depois da última fase vem o **questionário final** (pós-teste), com as mesmas perguntas. A tela de resultado compara as duas notas por bloco de conteúdo.
5. Durante todo o jogo, cada evento importante (entrada e saída de fase, erros de puzzle, quedas, pausas, respostas) é gravado em CSV para a análise do TCC.

Ordem das cenas no build: `Menu → Questionario (pré) → Fisica → Aplicacao_Fisica → Interacao → Interacao_Vida → Tutorial_Anim → Animacao → IA → IA_Aplicacao → Questionario (pós) → Menu`.

## Fases e conceitos ensinados

| # | Cena | Título no jogo | O que o aluno aprende |
|---|---|---|---|
| 1 | `Fisica` | Física | Movimento, Rigidbody2D, Collider2D, pulo e câmera (5 puzzles de encaixe de código) |
| 2 | `Aplicacao_Fisica` | Aplicação da Física | Usar esses componentes no cenário, aplicando colisão e Rigidbody2D nos blocos |
| 3 | `Interacao` | Interação | Ação e consequência: triggers e colisões, quem ativa (tag Player), "uma vez" ou "sempre" |
| 4 | `Interacao_Vida` | Interação e Vida | Dano, cura, HUD (dado → evento → interface) e Game Over |
| 5 | `Tutorial_Anim` | Animação quadro a quadro | Ordem dos quadros, FPS e loop |
| 6 | `Animacao` | Animator | Estados e transições do Animator (parâmetros como velocidade e "no chão") e animação de objetos da fase |
| 7 | `IA` | IA: o cérebro do guarda | Máquina de estados, transições e depuração com Raycast |
| 8 | `IA_Aplicacao` | IA: guarda e aliado | Configurar um inimigo e um aliado que distrai o guarda |

A lista oficial das fases (nome, título e conceito) fica em `Assets/Scripts/Save/ProgressoJogo.cs`. Se a ordem mudar no Build Profiles, ela precisa mudar lá também.

A cena `Assets/Scenes/Sprites.unity` existe no projeto, mas não entra no build.

## Controles

Teclas padrão. Todas as ações de jogo podem ser trocadas em **Configurações → Controles**, e a troca fica salva.

| Ação | Teclado | Controle (gamepad) |
|---|---|---|
| Mover | A / D ou ← / → | Analógico esquerdo ou direcional |
| Pular | Espaço | Botão de baixo (A / ✕) |
| Agachar | C | Botão da direita (B / ◯) |
| Interagir | E | Botão de cima (Y / △) |
| Reiniciar a fase | Segurar R por 1 segundo | Select |
| Pausa (Continuar, Configurações, Menu) | Esc | — |
| Continuar após "Fase concluída" | Enter ou Espaço | — |
| Puzzles de código e painéis | Mouse (arrastar e soltar as peças, clicar nas opções) | — |

A ação **Correr** (Shift esquerdo) aparece na tela de Controles, mas nenhum script de jogador lê essa ação hoje.

O tempo em que o jogo fica pausado ou com a janela fora de foco não conta como tempo de fase nos registros.

## Como abrir o projeto na Unity

**Requisitos**

- **Unity 6000.5.7f1** (a mesma versão de `ProjectSettings/ProjectVersion.txt`). Outras versões podem converter o projeto e causar diferenças.
- Módulo **Windows Build Support (Mono ou IL2CPP)** para gerar o executável.
- Visual Studio ou Rider para editar os scripts (opcional).

**Passos**

1. Clone o repositório:
   ```bash
   git clone https://github.com/JohnG-404/Oficina_do_Byte_TCC_Engenharia_de_Computa-o_FEAP_AVAR-.git
   ```
2. No Unity Hub, clique em **Add → Add project from disk** e escolha a pasta clonada.
3. Abra com a versão 6000.5.7f1. Na primeira abertura a Unity recria a pasta `Library/`, o que leva alguns minutos.
4. Abra a cena `Assets/Scenes/Menu.unity` e aperte **Play** para jogar desde o início.

Ao jogar pelo Editor, os saves e registros vão para uma pasta separada (veja abaixo), para não se misturarem com os dados dos alunos.

**Principais pacotes** (lista completa em `Packages/manifest.json`): Universal Render Pipeline 2D (17.5.0), Input System (1.20.0), 2D Animation, 2D Tilemap e Tilemap Extras, uGUI com TextMesh Pro, Timeline.

## Como gerar o executável e o instalador

### Executável (Windows 64 bits)

1. Com o projeto aberto, use o menu **Tools → Build → Gerar executável (Windows)**.
2. O build usa as cenas marcadas em Build Profiles e sai em `Builds/Oficina do Byte/Oficina do Byte.exe`, na raiz do projeto. Rodar de novo substitui o build anterior.

A pasta `Builds/` não vai para o repositório (está no `.gitignore`).

### Instalador

O instalador é feito com o **Inno Setup 6** a partir do script `Instalador/OficinaDoByte.iss`.

1. Gere o executável (passo anterior).
2. Abra `Instalador/OficinaDoByte.iss` no Inno Setup e use **Build → Compile** (Ctrl+F9).
3. O instalador sai em `Instalador/Saida/OficinaDoByte_Instalador_1.2.exe`.

O instalador está em português, não pede senha de administrador (instala na pasta do usuário, com opção de instalar para todos), cria atalho no menu Iniciar e, se o usuário quiser, na Área de Trabalho. Ao desinstalar, os saves e registros dos alunos **não** são apagados.

Quando a versão mudar, atualize `bundleVersion` nas Player Settings e `#define Versao` no `.iss`.

## Onde ficam os dados (saves e registros)

No jogo instalado, tudo fica em:

```
C:\Users\<usuário>\AppData\LocalLow\FEAP Avare\Oficina do Byte\
├── Jogadores\jogadores.json              lista de jogadores deste PC
├── Save\Jogadores\<id>\progresso.json    progresso de cada jogador
└── Registros\sessao_<jogador>_<data>.csv um CSV por sessão de jogo
```

Ao jogar pelo Editor, a pasta é a vizinha `Oficina do Byte (Editor)`.

**Progresso (`progresso.json`)**: fases concluídas, tentativas, tempo (sem pausas), erros e quedas por fase, notas do pré e do pós-teste. É gravado de forma segura: escreve num `.tmp` e guarda o anterior como `.bak`.

**Registros (CSV)**: separados por `;` e com acentos corretos ao abrir no Excel em português. Colunas:

```
sessao; data_hora; segundos; cena; evento; detalhe; jogador
```

Cada linha é gravada na hora, então nada se perde se o jogo fechar no meio. Se o CSV estiver aberto no Excel enquanto o jogo roda, as linhas esperam e entram depois; se ainda estiver travado ao fechar, o restante vai para `<mesmo nome>_continuacao.csv`.

Jogadores anônimos aparecem no CSV como `anonimo-007-K3`, em que `K3` é um código sorteado uma vez por computador, para que os dados de vários PCs possam ser juntados sem confusão.

Na tela de resultado do questionário final, a tecla **P** abre a pasta dos registros.

## Modo professor

Atalhos sem botão na tela, para que os alunos não os usem sem querer:

| Atalho | Onde | O que faz |
|---|---|---|
| Ctrl+Shift+U | Seleção de fases | Liga ou desliga o modo professor, que libera todas as fases |
| Ctrl+Shift+E | Seleção de fases, com o modo professor ligado | Exporta os registros deste PC para uma pasta nova na Área de Trabalho: cópia dos CSVs de sessão e um `resumo_jogadores.csv` com uma linha por jogador (pré e pós-teste e, por fase, conclusão, tempo, erros e quedas) |
| Ctrl+Shift+P | Questionário | Pula o questionário |

## Estrutura do repositório

```
Assets/
├── Scenes/        cenas do jogo (Menu, Questionario e as 8 fases)
├── Scripts/       código do jogo, separado por área
│   ├── Inicio/        jogador, diálogos e puzzles de encaixe (base da Física)
│   ├── Aplicacao/     fase Aplicação da Física
│   ├── Interacao/     fases Interação e Interação e Vida
│   ├── TutorialAnim/  fase Animação quadro a quadro
│   ├── Animacao/      fase Animator
│   ├── IA/            fases de IA
│   ├── Questionario/  pré e pós-teste
│   ├── Registro/      CSVs de pesquisa e exportação
│   ├── Save/          jogadores, progresso e seletor de fases
│   ├── Menu/, UI/     menu, pausa, configurações, créditos e tema visual
│   └── Input/         Input System e troca de teclas
├── Editor/        ferramentas de Editor (menu Tools) que montam cenas e painéis
├── Resources/     prefabs carregados por código, áudio e Input Actions
├── Art/, Images/  sprites e tilesets
├── Anim/          clipes e Animator Controllers
└── Prefabs/, Settings/, UI Toolkit/, TextMesh Pro/
Instalador/        script do Inno Setup e ícone
Packages/          manifesto de pacotes da Unity
ProjectSettings/   configurações do projeto
```

Várias telas e cenas são montadas por ferramentas do menu **Tools** no Editor (por exemplo, *Tools → IA → Criar Cena IA*). Algumas dessas ferramentas sobrescrevem a cena aberta, então use-as com o projeto salvo no git.

## Créditos de arte e áudio

- **Músicas e efeitos sonoros**: gerados por síntese em código feito para o projeto, sem direitos autorais de terceiros (OGG Vorbis, 44,1 kHz). O som de digitação dos diálogos (`Assets/Resources/Audio/SFX/typewriter_*.wav`): *a preencher — origem e licença.*
- **Sprites e tilesets** (`Assets/Images/Packs`, `Assets/Art`): *a preencher — origem e licença dos pacotes de arte.*
- **Fontes**: *a preencher — fontes usadas no TextMesh Pro e suas licenças.*

## Licença

*A definir.* Até que uma licença seja escolhida, todos os direitos ficam reservados aos autores.
