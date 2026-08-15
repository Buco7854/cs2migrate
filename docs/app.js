const translations = {
  en: {
    pageTitle: "CS2 Migrate — move your setup, keep your edge",
    pageDescription: "Safely move Counter-Strike 2 settings between Steam accounts with one portable Windows app.",
    languageLabel: "Passer en français",
    navSafety: "Safety", navHow: "How it works",
    heroEyebrow: "Open source · local-first", heroLineOne: "Move your setup.", heroLineTwo: "Keep your edge.",
    heroLead: "Sensitivity, crosshair, keybinds, video settings, and autoexec—moved safely between local Steam accounts in a few clicks.",
    downloadWindows: "Download for Windows", viewSource: "View source", latestBuild: "Latest successful main build",
    selfContained: "Self-contained x64 executable", mockAccounts: "3 local accounts", mockChoose: "CHOOSE YOUR ACCOUNTS",
    source: "SOURCE", target: "TARGET", mockFiles: "4 portable settings files", mockReady: "Ready for migration",
    gameplay: "Gameplay", gameplayShort: "Sensitivity, crosshair", keybinds: "Keybinds", keybindsShort: "Keyboard and mouse",
    video: "Video", videoShort: "Resolution, graphics", autoexecShort: "Custom commands", mockSafe: "Files are safe to migrate",
    mockSafeDetail: "Steam is closed · backup and verification enabled", migrate: "Migrate settings",
    trustExe: "One .exe", trustExeDetail: "No installer or runtime", trustOffline: "Offline by default",
    trustOfflineDetail: "No API key or tracking", trustBackup: "Automatic backups", trustBackupDetail: "Every replaced file",
    trustOpen: "Open source", trustOpenDetail: "Inspect every line", safetyEyebrow: "Designed around your files",
    safetyTitle: "Careful where it matters.",
    safetyLead: "The original script was a useful proof of concept. The app turns it into a previewable transaction with guardrails at every write.",
    featureSteam: "Steam-aware", featureSteamDetail: "Migration stays locked while Steam or CS2 is running. The app uses Steam’s normal shutdown command and never force-kills it.",
    featureBackup: "Backup first", featureBackupDetail: "Every matching target file and a sealed recovery copy are saved outside Steam’s folders with a readable JSON manifest.",
    featureVerify: "Verified twice", featureVerifyDetail: "Files are SHA-256 checked after staging and after commit. A failed transaction rolls back what it changed.",
    featureCleanup: "Friend mode", featureCleanupDetail: "Borrow an account without leaving your setup behind. One click later restores the friend’s exact original configuration.",
    stepsEyebrow: "Three calm steps", stepsTitle: "From fragging to fragging.",
    stepsLead: "Account discovery and local profile images make it hard to pick the wrong player. A precise preview makes the final action obvious.",
    stepOne: "Pick the source and target", stepOneDetail: "Steam accounts already used on this PC appear automatically, with persona names, account IDs, and cached avatars.",
    stepTwo: "Choose exactly what moves", stepTwoDetail: "Toggle gameplay, keybinds, video, or autoexec. See which target files are new and which will be replaced.",
    stepThree: "Close Steam and migrate", stepThreeDetail: "The app stages, backs up, commits, and verifies. If Steam Cloud restores old settings later, the sealed copy can be reapplied.",
    downloadEyebrow: "Built fresh from main", downloadTitle: "Your config deserves a better moving day.",
    downloadLead: "No install wizard. Download one executable, close Steam, and run it.", downloadExe: "Download CS2Migrate.exe",
    copy: "Copy", copied: "Copied", checksumPending: "Available after first build",
    requirements: "Windows 10/11 · x64 · no administrator access · unsigned development build",
    disclaimer: "Not affiliated with Valve or Steam. Counter-Strike and Steam are trademarks of Valve Corporation.",
    license: "MIT licensed ↗", buildPending: "Build pending — use the workflow artifact", built: "Built"
  },
  fr: {
    pageTitle: "CS2 Migrate — transférez vos réglages, gardez votre niveau",
    pageDescription: "Transférez vos réglages Counter-Strike 2 entre comptes Steam avec une application Windows portable.",
    languageLabel: "Switch to English",
    navSafety: "Sécurité", navHow: "Fonctionnement",
    heroEyebrow: "Open source · local d’abord", heroLineOne: "Transférez vos réglages.", heroLineTwo: "Gardez votre niveau.",
    heroLead: "Sensibilité, viseur, touches, vidéo et autoexec : transférez tout proprement entre vos comptes Steam locaux en quelques clics.",
    downloadWindows: "Télécharger pour Windows", viewSource: "Voir le code", latestBuild: "Dernier build main réussi",
    selfContained: "Exécutable x64 autonome", mockAccounts: "3 comptes locaux", mockChoose: "CHOISISSEZ VOS COMPTES",
    source: "SOURCE", target: "CIBLE", mockFiles: "4 fichiers de réglages portables", mockReady: "Prêt pour la migration",
    gameplay: "Gameplay", gameplayShort: "Sensibilité, viseur", keybinds: "Touches", keybindsShort: "Clavier et souris",
    video: "Vidéo", videoShort: "Résolution, graphismes", autoexecShort: "Commandes personnalisées", mockSafe: "Les fichiers peuvent être migrés",
    mockSafeDetail: "Steam est fermé · sauvegarde et vérification activées", migrate: "Migrer les réglages",
    trustExe: "Un seul .exe", trustExeDetail: "Sans installation ni runtime", trustOffline: "Hors ligne par défaut",
    trustOfflineDetail: "Sans clé API ni pistage", trustBackup: "Sauvegardes automatiques", trustBackupDetail: "Chaque fichier remplacé",
    trustOpen: "Open source", trustOpenDetail: "Inspectez chaque ligne", safetyEyebrow: "Pensé autour de vos fichiers",
    safetyTitle: "Prudent là où ça compte.",
    safetyLead: "Le script initial était une bonne preuve de concept. L’application en fait une transaction prévisible avec des protections à chaque écriture.",
    featureSteam: "Compatible avec Steam", featureSteamDetail: "La migration reste verrouillée tant que Steam ou CS2 tourne. L’application utilise la commande d’arrêt normale de Steam et ne force aucun processus.",
    featureBackup: "Sauvegarde d’abord", featureBackupDetail: "Chaque fichier cible et une copie de récupération scellée sont conservés hors de Steam avec un manifeste JSON lisible.",
    featureVerify: "Double vérification", featureVerifyDetail: "Les fichiers sont contrôlés en SHA-256 après la préparation et après l’écriture. Une transaction échouée est annulée.",
    featureCleanup: "Mode ami", featureCleanupDetail: "Utilisez un compte emprunté sans y laisser vos réglages. Un clic restaure ensuite sa configuration d’origine exacte.",
    stepsEyebrow: "Trois étapes tranquilles", stepsTitle: "Du jeu au jeu.",
    stepsLead: "La détection des comptes et les images de profil locales évitent de choisir le mauvais joueur. L’aperçu précis rend l’action finale évidente.",
    stepOne: "Choisissez la source et la cible", stepOneDetail: "Les comptes Steam déjà utilisés sur ce PC apparaissent automatiquement avec leur pseudo, leur ID et leur avatar en cache.",
    stepTwo: "Choisissez exactement quoi transférer", stepTwoDetail: "Activez gameplay, touches, vidéo ou autoexec. Voyez les fichiers cibles qui seront créés ou remplacés.",
    stepThree: "Fermez Steam et migrez", stepThreeDetail: "L’application prépare, sauvegarde, applique et vérifie. Si Steam Cloud restaure ensuite les anciens réglages, la copie scellée peut être réappliquée.",
    downloadEyebrow: "Fraîchement compilé depuis main", downloadTitle: "Vos réglages méritent un meilleur déménagement.",
    downloadLead: "Aucun assistant d’installation. Téléchargez un exécutable, fermez Steam et lancez-le.", downloadExe: "Télécharger CS2Migrate.exe",
    copy: "Copier", copied: "Copié", checksumPending: "Disponible après le premier build",
    requirements: "Windows 10/11 · x64 · sans droits administrateur · build de développement non signé",
    disclaimer: "Non affilié à Valve ou Steam. Counter-Strike et Steam sont des marques de Valve Corporation.",
    license: "Licence MIT ↗", buildPending: "Build en attente — utilisez l’artifact du workflow", built: "Compilé le"
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
  const toggle = document.querySelector("#language-toggle");
  toggle.textContent = language === "en" ? "FR" : "EN";
  toggle.setAttribute("aria-label", text.languageLabel);
  if (!buildInfo) document.querySelector("#checksum").textContent = text.checksumPending;
  renderBuild();
};

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
