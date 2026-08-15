const translations = {
  en: {
    pageTitle: "CS2 Migrate — copy CS2 settings between Steam accounts",
    pageDescription: "A single Windows executable that copies Counter-Strike 2 settings between the Steam accounts on your PC, with backups and verification.",
    languageLabel: "Passer en français",
    themeLabel: "Switch between the light and dark theme",
    skip: "Skip to content",

    navWhat: "What it copies",
    navHow: "How it works",
    navSafety: "Safety",
    navInstall: "Install",

    kicker: "Free · open source · runs offline",
    heroTitle: "Copy your CS2 settings to another Steam account",
    heroLead: "Crosshair, sensitivity, keybinds, video options, and autoexec — moved between the Steam accounts already on your PC. One executable, no installer, and a backup of everything it replaces.",
    downloadWindows: "Download for Windows",
    viewSource: "View the source",
    latestBuild: "Latest build from main",
    selfContained: "Self-contained x64 executable",
    buildPending: "No published build yet — use the workflow artifact",
    built: "Built",

    mockSafe: "Files are safe to copy",
    mockSafeDetail: "Steam and CS2 are closed. Every replaced file is backed up first.",
    mockAccounts: "Accounts",
    mockFrom: "Copy from",
    mockTo: "Copy to",
    mockWhat: "Settings to copy",
    mockStatus: "4 files · 2 replaced · 2 new",
    migrate: "Migrate settings",
    gameplay: "Gameplay",
    keybinds: "Keybinds",
    video: "Video",

    glanceExe: "One .exe",
    glanceExeDetail: "No installer, no runtime to add",
    glanceOffline: "Fully offline",
    glanceOfflineDetail: "No account, API key, or telemetry",
    glanceBackup: "Backs up first",
    glanceBackupDetail: "Every file it replaces is saved",
    glanceOpen: "MIT licensed",
    glanceOpenDetail: "Every line is on GitHub",

    copiesTitle: "What it copies",
    copiesLead: "Only files you authored through the game are eligible. Everything else in the folder is left where it is.",
    colCategory: "Category",
    colFiles: "Files",
    colContents: "Contents",
    rowGameplay: "Sensitivity, crosshair, HUD, gameplay cvars",
    rowKeybinds: "Keyboard and mouse bindings",
    rowVideo: "Resolution and graphics choices",
    rowAutoexec: "Your own startup commands",
    copiesExcluded: "Machine-generated settings, unknown files, <code>steam_autocloud.vdf</code>, and <code>remotecache.vdf</code> are deliberately excluded.",

    howTitle: "How it works",
    howLead: "Three steps, and nothing is written until you confirm the last one.",
    step1: "Pick the two accounts",
    step1Detail: "Steam accounts already used on this PC are detected automatically, with their persona name, account ID, and locally cached avatar.",
    step2: "Choose what to copy",
    step2Detail: "Turn gameplay, keybinds, video, or autoexec on and off. The preview lists each file by name and says whether it is new or replaces an existing one.",
    step3: "Close Steam and migrate",
    step3Detail: "The app stages the files, backs up what it will overwrite, copies, and verifies. If anything fails, it rolls back.",

    safetyTitle: "What it does to protect your files",
    safetyLead: "Config folders are easy to break and annoying to rebuild, so the app is deliberately conservative.",
    safe1: "Steam and CS2 must both be closed before any write begins.",
    safe2: "Steam is asked to exit with <code>steam.exe -shutdown</code>; no process is ever force-killed.",
    safe3: "Files are staged on the destination drive and SHA-256 verified before and after copying.",
    safe4: "Replaced files are backed up to <code>%LOCALAPPDATA%\\CS2Migrate\\Backups</code> with a readable JSON manifest.",
    safe5: "Only the selected, recognised files are touched. The destination folder is never mirrored or emptied.",
    safe6: "If a write or a checksum fails, every changed file is restored from the backup.",
    cloudTitle: "About Steam Cloud",
    cloudDetail: "Steam Auto-Cloud syncs configured files when a game starts and stops, so a fully closed Steam client — not deleted metadata — is the safe boundary for a migration. The app keeps a sealed copy of what it wrote; if Cloud later restores older settings, it detects the checksum mismatch and offers to reapply that copy.",
    cloudLink: "Steam Cloud documentation ↗",

    installTitle: "Install and run",
    install1: "Download <code>CS2Migrate.exe</code>.",
    install2: "Double-click it. There is nothing to install and no administrator prompt.",
    install3: "Close Steam when the app asks, review the preview, then migrate.",
    installNote: "Requires Windows 10 or 11, 64-bit. Builds from <code>main</code> are not code-signed, so SmartScreen may warn on first run — compare the checksum if you want to be sure.",
    downloadExe: "Download CS2Migrate.exe",
    copy: "Copy",
    copied: "Copied",
    checksumPending: "Available after the first build",
    requirements: "Windows 10/11 · x64 · self-contained · unsigned development build",

    faqTitle: "Questions",
    q1: "Does this touch my friend's settings permanently?",
    a1: "Only if you want it to. Turn on the temporary session switch before migrating onto a borrowed account: the app protects the exact original state and its main button becomes “Restore friend's settings”, which puts everything back and removes the files it added.",
    q2: "Where do the backups go?",
    a2: "Into <code>%LOCALAPPDATA%\\CS2Migrate\\Backups</code>, outside every Steam folder, with a JSON manifest listing each file and its checksum. The app has a button that opens that folder.",
    q3: "Can I use it between two PCs?",
    a3: "No. It reads the Steam <code>userdata</code> folder of the machine it runs on, so both accounts have to have been signed in on that PC at least once.",
    q4: "Why does Windows warn me about the download?",
    a4: "The executable is built by GitHub Actions and is not code-signed, which is enough for SmartScreen to flag it as unrecognised. The SHA-256 above matches the build published here, and the workflow that produced it is in the repository.",

    disclaimer: "Not affiliated with Valve or Steam. Counter-Strike and Steam are trademarks of Valve Corporation.",
    license: "MIT license"
  },

  fr: {
    pageTitle: "CS2 Migrate — copier ses réglages CS2 entre comptes Steam",
    pageDescription: "Un seul exécutable Windows qui copie les réglages Counter-Strike 2 entre les comptes Steam de votre PC, avec sauvegarde et vérification.",
    languageLabel: "Switch to English",
    themeLabel: "Basculer entre le thème clair et le thème sombre",
    skip: "Aller au contenu",

    navWhat: "Ce qui est copié",
    navHow: "Fonctionnement",
    navSafety: "Sécurité",
    navInstall: "Installation",

    kicker: "Gratuit · open source · fonctionne hors ligne",
    heroTitle: "Copiez vos réglages CS2 vers un autre compte Steam",
    heroLead: "Viseur, sensibilité, touches, options vidéo et autoexec : transférés entre les comptes Steam déjà présents sur votre PC. Un seul exécutable, aucune installation, et une sauvegarde de tout ce qui est remplacé.",
    downloadWindows: "Télécharger pour Windows",
    viewSource: "Voir le code source",
    latestBuild: "Dernier build de la branche main",
    selfContained: "Exécutable x64 autonome",
    buildPending: "Aucun build publié — utilisez l’artifact du workflow",
    built: "Compilé le",

    mockSafe: "Les fichiers peuvent être copiés",
    mockSafeDetail: "Steam et CS2 sont fermés. Chaque fichier remplacé est d’abord sauvegardé.",
    mockAccounts: "Comptes",
    mockFrom: "Copier depuis",
    mockTo: "Copier vers",
    mockWhat: "Réglages à copier",
    mockStatus: "4 fichiers · 2 remplacés · 2 nouveaux",
    migrate: "Migrer les réglages",
    gameplay: "Gameplay",
    keybinds: "Touches",
    video: "Vidéo",

    glanceExe: "Un seul .exe",
    glanceExeDetail: "Aucun installeur, aucun runtime à ajouter",
    glanceOffline: "Totalement hors ligne",
    glanceOfflineDetail: "Aucun compte, aucune clé API, aucun pistage",
    glanceBackup: "Sauvegarde d’abord",
    glanceBackupDetail: "Chaque fichier remplacé est conservé",
    glanceOpen: "Licence MIT",
    glanceOpenDetail: "Tout le code est sur GitHub",

    copiesTitle: "Ce qui est copié",
    copiesLead: "Seuls les fichiers que vous avez produits en jouant sont éligibles. Tout le reste du dossier est laissé en place.",
    colCategory: "Catégorie",
    colFiles: "Fichiers",
    colContents: "Contenu",
    rowGameplay: "Sensibilité, viseur, ATH, cvars de jeu",
    rowKeybinds: "Raccourcis clavier et souris",
    rowVideo: "Résolution et options graphiques",
    rowAutoexec: "Vos propres commandes de démarrage",
    copiesExcluded: "Les réglages générés par la machine, les fichiers inconnus, <code>steam_autocloud.vdf</code> et <code>remotecache.vdf</code> sont volontairement exclus.",

    howTitle: "Fonctionnement",
    howLead: "Trois étapes, et rien n’est écrit tant que vous n’avez pas confirmé la dernière.",
    step1: "Choisissez les deux comptes",
    step1Detail: "Les comptes Steam déjà utilisés sur ce PC sont détectés automatiquement, avec leur pseudo, leur identifiant et leur avatar en cache local.",
    step2: "Choisissez ce qui est copié",
    step2Detail: "Activez ou désactivez gameplay, touches, vidéo et autoexec. L’aperçu liste chaque fichier par son nom et indique s’il est nouveau ou s’il en remplace un existant.",
    step3: "Fermez Steam et migrez",
    step3Detail: "L’application prépare les fichiers, sauvegarde ce qu’elle va écraser, copie, puis vérifie. En cas d’échec, tout est annulé.",

    safetyTitle: "Ce qui protège vos fichiers",
    safetyLead: "Un dossier de configuration se casse vite et se reconstruit lentement : l’application est donc volontairement prudente.",
    safe1: "Steam et CS2 doivent tous les deux être fermés avant la moindre écriture.",
    safe2: "Steam reçoit la commande d’arrêt <code>steam.exe -shutdown</code> ; aucun processus n’est jamais tué de force.",
    safe3: "Les fichiers sont préparés sur le disque de destination et vérifiés en SHA-256 avant et après la copie.",
    safe4: "Les fichiers remplacés sont sauvegardés dans <code>%LOCALAPPDATA%\\CS2Migrate\\Backups</code> avec un manifeste JSON lisible.",
    safe5: "Seuls les fichiers sélectionnés et reconnus sont touchés. Le dossier de destination n’est jamais vidé ni synchronisé à l’identique.",
    safe6: "Si une écriture ou une somme de contrôle échoue, chaque fichier modifié est restauré depuis la sauvegarde.",
    cloudTitle: "À propos de Steam Cloud",
    cloudDetail: "Steam Auto-Cloud synchronise les fichiers configurés au lancement et à la fermeture d’un jeu : la limite sûre pour une migration est donc un client Steam complètement fermé, et non des métadonnées supprimées. L’application conserve une copie scellée de ce qu’elle a écrit ; si le Cloud restaure ensuite d’anciens réglages, elle détecte la différence de somme de contrôle et propose de réappliquer cette copie.",
    cloudLink: "Documentation Steam Cloud ↗",

    installTitle: "Installation et lancement",
    install1: "Téléchargez <code>CS2Migrate.exe</code>.",
    install2: "Double-cliquez dessus. Il n’y a rien à installer et aucune demande de droits administrateur.",
    install3: "Fermez Steam quand l’application le demande, relisez l’aperçu, puis migrez.",
    installNote: "Nécessite Windows 10 ou 11, 64 bits. Les builds de <code>main</code> ne sont pas signés : SmartScreen peut donc afficher un avertissement au premier lancement — comparez la somme de contrôle en cas de doute.",
    downloadExe: "Télécharger CS2Migrate.exe",
    copy: "Copier",
    copied: "Copié",
    checksumPending: "Disponible après le premier build",
    requirements: "Windows 10/11 · x64 · autonome · build de développement non signé",

    faqTitle: "Questions",
    q1: "Est-ce que cela modifie définitivement les réglages de mon ami ?",
    a1: "Seulement si vous le voulez. Activez l’interrupteur de session temporaire avant de migrer sur un compte emprunté : l’application protège l’état d’origine exact et son bouton principal devient « Restaurer les réglages de l’ami », ce qui remet tout en place et supprime les fichiers ajoutés.",
    q2: "Où sont stockées les sauvegardes ?",
    a2: "Dans <code>%LOCALAPPDATA%\\CS2Migrate\\Backups</code>, en dehors de tout dossier Steam, avec un manifeste JSON listant chaque fichier et sa somme de contrôle. Un bouton de l’application ouvre ce dossier.",
    q3: "Puis-je l’utiliser entre deux PC ?",
    a3: "Non. L’application lit le dossier <code>userdata</code> de Steam de la machine sur laquelle elle tourne : les deux comptes doivent s’y être connectés au moins une fois.",
    q4: "Pourquoi Windows m’avertit-il au téléchargement ?",
    a4: "L’exécutable est compilé par GitHub Actions et n’est pas signé, ce qui suffit à ce que SmartScreen le signale comme inconnu. Le SHA-256 ci-dessus correspond au build publié ici, et le workflow qui l’a produit se trouve dans le dépôt.",

    disclaimer: "Non affilié à Valve ni à Steam. Counter-Strike et Steam sont des marques de Valve Corporation.",
    license: "Licence MIT"
  }
};

const readLanguage = () => {
  try {
    const saved = localStorage.getItem("cs2migrate-language");
    if (saved === "en" || saved === "fr") return saved;
  } catch { /* Browser storage may be disabled. */ }
  return navigator.language.toLowerCase().startsWith("fr") ? "fr" : "en";
};

let language = readLanguage();
let buildInfo = null;
let currentChecksum = "";

const formatBytes = (bytes) => {
  if (!bytes) return translations[language].selfContained;
  const units = ["B", "KB", "MB", "GB"];
  const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  return `${(bytes / (1024 ** index)).toFixed(index > 1 ? 1 : 0)} ${units[index]}`;
};

const formatDate = (value) => {
  if (!value) return translations[language].latestBuild;
  const locale = language === "fr" ? "fr-FR" : "en-US";
  const date = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
  return `${translations[language].built} ${date}`;
};

const renderBuild = () => {
  if (!buildInfo) return;
  document.querySelector("#build-label").textContent = `${buildInfo.version} · ${formatDate(buildInfo.builtAt)}`;
  document.querySelector("#build-size").textContent = formatBytes(buildInfo.bytes);
  document.querySelector("#checksum").textContent = buildInfo.sha256;
  currentChecksum = buildInfo.sha256;
  for (const link of [document.querySelector("#download-button"), document.querySelector("#download-button-bottom")]) {
    link.href = buildInfo.download;
  }
};

const applyLanguage = () => {
  const text = translations[language];
  document.documentElement.lang = language;
  document.title = text.pageTitle;
  document.querySelector('meta[name="description"]').content = text.pageDescription;

  for (const element of document.querySelectorAll("[data-i18n]")) {
    const value = text[element.dataset.i18n];
    if (value) element.textContent = value;
  }
  // A few strings contain inline <code> markup; they come from this file, never from user input.
  for (const element of document.querySelectorAll("[data-i18n-html]")) {
    const value = text[element.dataset.i18nHtml];
    if (value) element.innerHTML = value;
  }

  const toggle = document.querySelector("#language-toggle");
  toggle.textContent = language === "en" ? "FR" : "EN";
  toggle.setAttribute("aria-label", text.languageLabel);
  document.querySelector("#theme-toggle").setAttribute("aria-label", text.themeLabel);

  if (!buildInfo) document.querySelector("#checksum").textContent = text.checksumPending;
  renderBuild();
};

document.querySelector("#theme-toggle").addEventListener("click", () => {
  const theme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
  document.documentElement.dataset.theme = theme;
  try { localStorage.setItem("cs2migrate-theme", theme); } catch { /* Keep the session theme. */ }
});

document.querySelector("#language-toggle").addEventListener("click", () => {
  language = language === "en" ? "fr" : "en";
  try { localStorage.setItem("cs2migrate-language", language); } catch { /* Keep the session language. */ }
  applyLanguage();
});

applyLanguage();

fetch("build-info.json", { cache: "no-store" })
  .then((response) => {
    if (!response.ok) throw new Error("Build metadata unavailable");
    return response.json();
  })
  .then((build) => {
    buildInfo = build;
    renderBuild();
  })
  .catch(() => {
    document.querySelector("#build-label").textContent = translations[language].buildPending;
  });

document.querySelector("#copy-checksum").addEventListener("click", async (event) => {
  if (!currentChecksum) return;
  await navigator.clipboard.writeText(currentChecksum);
  const button = event.currentTarget;
  button.textContent = translations[language].copied;
  window.setTimeout(() => { button.textContent = translations[language].copy; }, 1600);
});
