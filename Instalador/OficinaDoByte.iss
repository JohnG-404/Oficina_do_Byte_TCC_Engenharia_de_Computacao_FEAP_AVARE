; Instalador do Oficina do Byte (Inno Setup 6)
; Como usar: gere o build no Unity (Tools > Build > Gerar executável (Windows)),
; abra este arquivo no Inno Setup e clique em Build > Compile (ou Ctrl+F9).
; O instalador sai em testTCC\Instalador\Saida\OficinaDoByte_Instalador_<versão>.exe

#define Nome "Oficina do Byte"
#define Versao "1.0"
#define Empresa "FEAP Avare"
#define Exe "Oficina do Byte.exe"
#define PastaBuild "..\Builds\Oficina do Byte"

[Setup]
AppId={{6F1C2B7E-4D8A-4B6E-9E1F-0B7D3C5A2E91}
AppName={#Nome}
AppVersion={#Versao}
AppPublisher={#Empresa}
DefaultDirName={autopf}\{#Nome}
DefaultGroupName={#Nome}
DisableProgramGroupPage=yes
; Instala sem pedir senha de administrador (na pasta do usuário).
; Na tela inicial dá para escolher instalar para todos os usuários.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Saida
OutputBaseFilename=OficinaDoByte_Instalador_{#Versao}
SetupIconFile=icone.ico
UninstallDisplayIcon={app}\{#Exe}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "atalho"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"

[Files]
; Copia o build inteiro, menos a pasta de depuração que o Unity marca como "não enviar".
Source: "{#PastaBuild}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*_DoNotShip\*,*_DoNotShip"

[Icons]
Name: "{group}\{#Nome}"; Filename: "{app}\{#Exe}"
Name: "{group}\Desinstalar {#Nome}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#Nome}"; Filename: "{app}\{#Exe}"; Tasks: atalho

[Run]
Filename: "{app}\{#Exe}"; Description: "Abrir o {#Nome} agora"; Flags: nowait postinstall skipifsilent

; Os saves e os CSVs ficam em AppData\LocalLow\FEAP Avare\Oficina do Byte.
; A desinstalação não apaga essa pasta, então os registros dos alunos ficam guardados.
